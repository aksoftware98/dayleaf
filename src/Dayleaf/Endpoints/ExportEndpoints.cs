using Dayleaf.Domain;
using Dayleaf.Services;
using Dayleaf.Services.Export;

namespace Dayleaf.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        app.MapGet("/export/library.dayleaf", (HttpContext http, DayleafExporter exp, DayleafPaths paths) =>
            SendArchiveAsync(http, exp, paths, ExportScope.Library, null));

        app.MapGet("/export/space/{id:guid}.dayleaf", (Guid id, HttpContext http, DayleafExporter exp, DayleafPaths paths) =>
            SendArchiveAsync(http, exp, paths, ExportScope.Space, id));

        app.MapGet("/export/page/{id:guid}.dayleaf", (Guid id, HttpContext http, DayleafExporter exp, DayleafPaths paths) =>
            SendArchiveAsync(http, exp, paths, ExportScope.Page, id));

        app.MapGet("/export/page/{id:guid}.html", async (Guid id, HtmlPageExporter html) =>
        {
            var result = await html.RenderAsync(id);
            if (result is null) return Results.NotFound();
            return Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value.Html), "text/html; charset=utf-8", result.Value.FileName);
        });

        // ── Server-side PDF: exact A4, one page per sheet, no print-dialog margins ──
        app.MapGet("/export/page/{id:guid}.pdf", async (Guid id, HtmlPageExporter html, PdfRenderer pdf, CancellationToken ct) =>
        {
            var result = await html.RenderAsync(id);
            if (result is null) return Results.NotFound();
            return await PdfAsync(pdf, result.Value.Html, Path.ChangeExtension(result.Value.FileName, ".pdf"), ct);
        });

        app.MapGet("/export/space/{id:guid}.pdf", async (Guid id, string? from, string? to, HtmlPageExporter html, PdfRenderer pdf, CancellationToken ct) =>
        {
            var result = await html.RenderSpaceAsync(id, ParseDate(from), ParseDate(to));
            if (result is null) return Results.NotFound();
            if (result.Value.Pages == 0) return Results.BadRequest("There are no pages in that range.");
            return await PdfAsync(pdf, result.Value.Html, Path.ChangeExtension(result.Value.FileName, ".pdf"), ct);
        });

        // Public PDF for a shared page (same content rules as the /p/{token} page).
        app.MapGet("/p/{token}.pdf", async (string token, IPageService pages, HtmlPageExporter html, PdfRenderer pdf, CancellationToken ct) =>
        {
            var page = await pages.GetByShareTokenAsync(token);
            if (page is null) return Results.NotFound();
            var model = await pages.GetSheetModelAsync(page.Id);
            if (model is null) return Results.NotFound();
            foreach (var key in model.LinkCards.Keys.ToList())
                if (model.LinkCards[key] is { } c) model.LinkCards[key] = c with { Url = "#", PhotoId = null };
            var doc = await html.RenderDocumentAsync([model], $"{model.SpaceName} · {model.Date:MMM d, yyyy}");
            return await PdfAsync(pdf, doc, $"{model.SpaceSlug}-{model.Date:yyyy-MM-dd}.pdf", ct);
        }).AllowAnonymous();
    }

    private static async Task<IResult> PdfAsync(PdfRenderer pdf, string html, string fileName, CancellationToken ct)
    {
        try
        {
            var bytes = await pdf.RenderAsync(html, ct);
            return Results.File(bytes, "application/pdf", fileName);
        }
        catch (PdfUnavailableException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "PDF isn't available");
        }
    }

    private static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>
    /// ZipArchive writes its central directory synchronously, so the archive is streamed to a temp file
    /// (never built in memory) and then copied to the response asynchronously.
    /// </summary>
    private static async Task<IResult> SendArchiveAsync(HttpContext http, DayleafExporter exp, DayleafPaths paths, ExportScope scope, Guid? id)
    {
        Directory.CreateDirectory(paths.TempDirectory);
        var temp = Path.Combine(paths.TempDirectory, $"export-{Guid.NewGuid():N}.dayleaf");
        var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            await exp.WriteAsync(fs, scope, id, http.RequestAborted);
        }
        catch (InvalidOperationException)
        {
            await fs.DisposeAsync();
            return Results.NotFound();
        }
        fs.Position = 0;
        var name = await exp.FileNameAsync(scope, id);
        return Results.File(fs, "application/vnd.dayleaf+zip", name);
    }
}
