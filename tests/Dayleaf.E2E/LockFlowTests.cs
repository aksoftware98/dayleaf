using System.Net;
using System.Net.Sockets;
using Dayleaf.Domain;
using Dayleaf.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dayleaf.E2E;

/// <summary>
/// Phase 9 end-to-end test: create a space → page → photo → submit → advance the clock 6h → locked.
/// Runs the real app on Kestrel with a fake TimeProvider and drives it with Playwright.
/// Uses the installed Edge or Chrome; if no browser can be launched the test reports why and returns.
/// </summary>
public sealed class LockFlowTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "dayleaf-e2e", Guid.NewGuid().ToString("N"));
    private DayleafApp? _app;
    private IPlaywright? _pw;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        _app = new DayleafApp(_clock, _dataDir);
        _app.UseKestrel(FreePort());
        _app.StartServer();

        _pw = await Playwright.CreateAsync();
        foreach (var channel in new[] { "msedge", "chrome", null })
        {
            try
            {
                _browser = await _pw.Chromium.LaunchAsync(new() { Channel = channel, Headless = true });
                output.WriteLine($"Browser: {channel ?? "bundled chromium"}");
                break;
            }
            catch (PlaywrightException ex)
            {
                output.WriteLine($"Couldn't launch {channel ?? "bundled chromium"}: {ex.Message.Split('\n')[0]}");
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _pw?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* sqlite may still hold the file briefly */ }
    }

    [Fact]
    public async Task Space_page_photo_submit_then_six_hours_later_it_is_locked()
    {
        if (_browser is null)
        {
            output.WriteLine("SKIPPED: no Chromium-based browser available. Install Edge/Chrome or run `pwsh tests/Dayleaf.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`.");
            return;
        }

        var baseUrl = _app!.ClientOptions.BaseAddress.ToString().TrimEnd('/');
        var page = await _browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        page.SetDefaultTimeout(20_000);

        // 1. Empty app → create a space.
        await page.GotoAsync(baseUrl + "/");
        // The button is prerendered before the interactive circuit connects; keep clicking until the dialog opens.
        await ClickUntilAsync(page.GetByRole(AriaRole.Button, new() { Name = "Create a space" }), page.GetByLabel("Name"));
        await page.GetByLabel("Name").FillAsync("E2E Garden");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create space" }).ClickAsync();
        await page.WaitForURLAsync("**/s/e2e-garden");

        // 2. Today's page opens straight in the editor.
        await page.GetByRole(AriaRole.Link, new() { Name = "Today's page" }).First.ClickAsync();
        await page.Locator(".sheets.mode-edit").WaitForAsync();

        // 3. Add a photo through the hidden file input (same path as drag & drop).
        await page.Locator("input[type=file]").First.SetInputFilesAsync(new FilePayload
        {
            Name = "garden.png",
            MimeType = "image/png",
            Buffer = TinyPng(),
        });
        var photo = page.Locator(".el[data-kind=photo]");
        await photo.WaitForAsync();
        Assert.Equal("true", await photo.GetAttributeAsync("data-editable"));

        // 4. Submit the section: it enters its 6-hour grace window.
        await page.Keyboard.PressAsync("Escape");
        await page.GetByRole(AriaRole.Button, new() { Name = "Submit section" }).ClickAsync();
        await page.Locator(".badge-grace").WaitForAsync();

        // 5. Six hours (and a minute) later, it's locked — in the UI and on the server.
        _clock.Advance(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(1));
        await page.ReloadAsync();
        await page.GetByText("This page is locked").WaitForAsync();
        Assert.Null(await page.Locator(".el[data-kind=photo]").GetAttributeAsync("data-editable"));
        await page.Locator(".badge-locked").First.WaitForAsync();

        using var scope = _app.Services.CreateScope();
        var pages = scope.ServiceProvider.GetRequiredService<IPageService>();
        var spaces = scope.ServiceProvider.GetRequiredService<ISpaceService>();
        var space = await spaces.GetBySlugAsync("e2e-garden");
        var today = DateOnly.FromDateTime(_clock.GetLocalNow().AddHours(-6.1).DateTime);
        var saved = await pages.GetByDateAsync(space!.Id, today);
        var element = Assert.Single(saved!.Elements);
        await Assert.ThrowsAsync<SectionLockedException>(() => pages.DeleteElementAsync(element.Id));
    }

    [Fact]
    public async Task Page_pdf_is_exactly_one_full_bleed_A4_page()
    {
        if (_browser is null) { output.WriteLine("SKIPPED: no Chromium-based browser available."); return; }

        Guid pageId;
        using (var scope = _app!.Services.CreateScope())
        {
            var spaces = scope.ServiceProvider.GetRequiredService<ISpaceService>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageService>();
            var space = await spaces.CreateAsync("PDF Test", "📄", "sea", null, "ocean");
            var page = await pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 1, 2), "A full-bleed page");
            await pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "<p>hello</p>" });
            pageId = page.Id;
        }

        using var http = new HttpClient { BaseAddress = _app.ClientOptions.BaseAddress };
        var res = await http.GetAsync($"/export/page/{pageId}.pdf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        var pdf = System.Text.Encoding.Latin1.GetString(await res.Content.ReadAsByteArrayAsync());

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type\s*/Page[^s]"));
        var box = System.Text.RegularExpressions.Regex.Match(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)");
        Assert.InRange(double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 594, 596); // 210 mm
        Assert.InRange(double.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 841, 843); // 297 mm
    }

    private static async Task ClickUntilAsync(ILocator target, ILocator appears, int attempts = 40)
    {
        for (var i = 0; i < attempts; i++)
        {
            await target.ClickAsync();
            try
            {
                await appears.WaitForAsync(new() { Timeout = 500 });
                return;
            }
            catch (TimeoutException) { }
        }
        throw new TimeoutException("The app never became interactive.");
    }

    private static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }

    private static byte[] TinyPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private sealed class DayleafApp(FakeTimeProvider clock, string dataDir) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Dayleaf:DataDirectory", dataDir);
            builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock));
        }
    }
}
