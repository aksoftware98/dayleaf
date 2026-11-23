using System.Net;
using Dayleaf.Components.Sheet;
using Dayleaf.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Dayleaf.Services.Export;

/// <summary>Renders one page to a single self-contained HTML file: CSS inlined, images as data: URIs.</summary>
public sealed class HtmlPageExporter(
    IServiceProvider services,
    ILoggerFactory loggerFactory,
    IPageService pages,
    IMediaService media,
    IWebHostEnvironment env)
{
    public async Task<(string Html, string FileName)?> RenderAsync(Guid pageId)
    {
        var model = await pages.GetSheetModelAsync(pageId);
        if (model is null) return null;

        var ids = model.Elements.Where(e => e.Element.MediaId != null).Select(e => e.Element.MediaId!.Value)
            .Concat(model.LinkCards.Values.Where(c => c?.PhotoId != null).Select(c => c!.PhotoId!.Value))
            .Distinct();
        var uris = new Dictionary<Guid, string>();
        foreach (var id in ids)
        {
            var file = await media.GetAsync(id);
            if (file is null) continue;
            var path = media.PathOf(file);
            if (!File.Exists(path)) continue;
            uris[id] = $"data:{file.ContentType};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(path))}";
        }
        model.MediaUrl = id => uris.TryGetValue(id, out var u) ? u : "";

        var body = await RenderSheetAsync(model, SheetMode.Print);
        var css = await File.ReadAllTextAsync(Path.Combine(env.WebRootPath, "sheet.css"));
        var fonts = await InlineFontsAsync();
        var title = WebUtility.HtmlEncode($"{model.SpaceName} · {model.Date:MMM d, yyyy}" + (string.IsNullOrWhiteSpace(model.Title) ? "" : $" · {model.Title}"));

        var html = $$$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{{title}}}</title>
            <meta name="generator" content="Dayleaf">
            <style>
            {{{fonts}}}
            {{{css}}}
            html,body{margin:0;background:#E9E2D3}
            .export-wrap{display:flex;flex-direction:column;align-items:center;gap:32px;padding:32px 12px}
            @media print{html,body{background:none}.export-wrap{padding:0;gap:0}}
            </style>
            </head>
            <body>
            <main class="export-wrap">
            {{{body}}}
            </main>
            </body>
            </html>
            """;
        var name = $"{model.SpaceSlug}-{model.Date:yyyy-MM-dd}.html";
        return (html, name);
    }

    /// <summary>The self-hosted fonts.css with every woff2 embedded as a data: URI, so exported files render offline.</summary>
    private async Task<string> InlineFontsAsync()
    {
        var dir = Path.Combine(env.WebRootPath, "fonts");
        var path = Path.Combine(dir, "fonts.css");
        if (!File.Exists(path)) return "";
        var css = await File.ReadAllTextAsync(path);
        foreach (var file in Directory.GetFiles(dir, "*.woff2"))
        {
            var name = Path.GetFileName(file);
            if (!css.Contains(name)) continue;
            var uri = $"data:font/woff2;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(file))}";
            css = css.Replace($"url({name})", $"url({uri})");
        }
        return css;
    }

    public async Task<string> RenderSheetAsync(SheetModel model, SheetMode mode)
    {
        await using var renderer = new HtmlRenderer(services, loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<A4Sheet>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(A4Sheet.Model)] = model,
                [nameof(A4Sheet.Mode)] = mode,
            }));
            return output.ToHtmlString();
        });
    }
}
