using Microsoft.Playwright;

namespace Dayleaf.Services.Export;

public sealed class PdfUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Server-side PDF: prints Dayleaf's own HTML with a headless Chromium (installed Edge or Chrome first,
/// then Playwright's bundled Chromium). Unlike the browser's print dialog, the result is always exactly
/// one full-bleed A4 page per sheet — no margins, no shrink-to-fit.
/// </summary>
public sealed class PdfRenderer(ILogger<PdfRenderer> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var browser = await GetBrowserAsync();
            // Offline: the document is self-contained, so nothing may be fetched from anywhere.
            var context = await browser.NewContextAsync(new() { Offline = true });
            try
            {
                var page = await context.NewPageAsync();
                await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
                await page.EvaluateAsync("() => document.fonts.ready");
                return await page.PdfAsync(new()
                {
                    PreferCSSPageSize = true, // @page { size: A4; margin: 0 }
                    PrintBackground = true,
                    Tagged = true,
                    Outline = false,
                });
            }
            finally
            {
                await context.CloseAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true }) return _browser;
        _playwright ??= await Playwright.CreateAsync();
        var errors = new List<string>();
        foreach (var channel in new[] { "msedge", "chrome", null })
        {
            try
            {
                _browser = await _playwright.Chromium.LaunchAsync(new() { Channel = channel, Headless = true });
                logger.LogInformation("PDF renderer using {Browser}.", channel ?? "bundled Chromium");
                return _browser;
            }
            catch (PlaywrightException ex)
            {
                errors.Add($"{channel ?? "chromium"}: {ex.Message.Split('\n')[0]}");
            }
        }
        logger.LogWarning("No browser for PDF rendering: {Errors}", string.Join(" | ", errors));
        throw new PdfUnavailableException(
            "Dayleaf couldn't start Edge or Chrome to make the PDF. Install one of them, or use Print and choose Save as PDF with margins set to None.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
