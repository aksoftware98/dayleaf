using Dayleaf.Domain;
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
    }

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
