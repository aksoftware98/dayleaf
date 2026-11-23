using System.Net;
using Dayleaf.Components.Sheet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Dayleaf.Services.Export;

/// <summary>
/// Renders pages to one self-contained HTML document: CSS and fonts inlined, images as data: URIs.
/// Used for the HTML download and as the input of the server-side PDF.
/// </summary>
public sealed class HtmlPageExporter(
    IServiceProvider services,
    ILoggerFactory loggerFactory,
    IPageService pages,
    ITimelineService timeline,
    ISpaceService spaces,
    IMediaService media,
    IWebHostEnvironment env)
{
    public async Task<(string Html, string FileName)?> RenderAsync(Guid pageId)
    {
        var model = await pages.GetSheetModelAsync(pageId);
        if (model is null) return null;
        var title = $"{model.SpaceName} · {model.Date:MMM d, yyyy}" + (string.IsNullOrWhiteSpace(model.Title) ? "" : $" · {model.Title}");
        var html = await RenderDocumentAsync([model], title);
        return (html, $"{model.SpaceSlug}-{model.Date:yyyy-MM-dd}.html");
    }

    /// <summary>A whole space (optionally a date range) as one document: the "book".</summary>
    public async Task<(string Html, string FileName, int Pages)?> RenderSpaceAsync(Guid spaceId, DateOnly? from, DateOnly? to)
    {
        var space = await spaces.GetAsync(spaceId);
        if (space is null) return null;
        var refs = (await timeline.ListPagesAsync(spaceId))
            .Where(p => (from is null || p.Date >= from) && (to is null || p.Date <= to))
            .OrderBy(p => p.Date);
        var models = new List<SheetModel>();
        foreach (var r in refs)
            if (await pages.GetSheetModelAsync(r.Id) is { } m) models.Add(m);
        var html = await RenderDocumentAsync(models, $"{space.Name} · book");
        var range = from is null && to is null ? "book" : $"{from:yyyy-MM-dd}_{to:yyyy-MM-dd}".Trim('_');
        return (html, $"{space.Slug}-{range}.html", models.Count);
    }

    public async Task<string> RenderDocumentAsync(IReadOnlyList<SheetModel> models, string title)
    {
        // Each image file is read once and shared by every place that uses it.
        var uris = new Dictionary<Guid, string>();
        foreach (var model in models)
        {
            var ids = model.Elements.Where(e => e.Element.MediaId != null).Select(e => e.Element.MediaId!.Value)
                .Concat(model.LinkCards.Values.Where(c => c?.PhotoId != null).Select(c => c!.PhotoId!.Value));
            foreach (var id in ids.Distinct())
            {
                if (uris.ContainsKey(id)) continue;
                var file = await media.GetAsync(id);
                if (file is null) continue;
                var path = media.PathOf(file);
                if (!File.Exists(path)) continue;
                uris[id] = $"data:{file.ContentType};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(path))}";
            }
            model.MediaUrl = id => uris.TryGetValue(id, out var u) ? u : "";
        }

        var body = new System.Text.StringBuilder();
        foreach (var model in models) body.Append(await RenderSheetAsync(model, SheetMode.Print));

        var css = await File.ReadAllTextAsync(Path.Combine(env.WebRootPath, "sheet.css"));
        var fonts = await InlineFontsAsync();
        var safeTitle = WebUtility.HtmlEncode(title);
        var lang = Dayleaf.Localization.Lang.Normalize(models.FirstOrDefault()?.Language);
        var dir = Dayleaf.Localization.Lang.Dir(lang);

        return $$$"""
            <!doctype html>
            <html lang="{{{lang}}}" dir="{{{dir}}}">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{{safeTitle}}}</title>
            <meta name="generator" content="Dayleaf">
            <style>
            {{{fonts}}}
            {{{css}}}
            html,body{margin:0;background:#E9E2D3}
            .export-wrap{display:flex;flex-direction:column;align-items:center;gap:32px;padding:32px 12px}
            @media print{html,body{background:none}.export-wrap{display:block;padding:0}}
            </style>
            </head>
            <body>
            <main class="export-wrap">
            {{{body}}}
            </main>
            </body>
            </html>
            """;
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
