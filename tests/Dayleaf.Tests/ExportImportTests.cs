using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dayleaf.Domain;
using Dayleaf.Services;
using Dayleaf.Services.Export;

namespace Dayleaf.Tests;

public class ExportImportRoundTripTests
{
    private static async Task BuildLibraryAsync(TestHost h)
    {
        var farm = await h.Spaces.CreateAsync("Our Farm", "🌳", "marigold", "orchard", "field");
        var typing = await h.Spaces.CreateAsync("Typing 365", "⌨️", "lilac", null, "pastel");
        var photo = await h.Media.SaveAsync(new MemoryStream(TestHost.TinyPng(1)), "tree.png");
        await h.Spaces.SetCoverAsync(farm.Id, photo.Id);

        var p1 = await h.Pages.GetOrCreatePageAsync(farm.Id, new DateOnly(2026, 11, 16), "Starting over, from the roots.");
        await h.Pages.UpdatePageMetaAsync(p1.Id, p1.Title, "field", ["🍂 Late autumn", "📍 Serghaya"], null);
        await h.Pages.AddElementAsync(p1.Id, ElementKind.Photo, new PhotoData { Style = "taped", Caption = "old row" }, photo.Id, at: (0, 50, 384), size: (460, 310), rotation: -2);
        await h.Pages.AddElementAsync(p1.Id, ElementKind.Text, new TextData { Html = "<p>Today we <b>finally</b> started.</p>" }, rotation: 0);
        await h.Pages.AddElementAsync(p1.Id, ElementKind.Checklist, new ChecklistData { Title = "This winter", Items = [new() { Text = "Mark trees", Done = true }] }, rotation: 1.5);
        await h.Pages.SubmitSectionAsync(p1.Sections.Single().Id);

        var p2 = await h.Pages.GetOrCreatePageAsync(typing.Id, new DateOnly(2025, 6, 11), "They said yes this time.");
        await h.Pages.AddElementAsync(p2.Id, ElementKind.Emoji, new EmojiData { Char = "🎉" }, rotation: 14);
        await h.Pages.AddElementAsync(p2.Id, ElementKind.PageLink, new PageLinkData { Note = "see the farm" }, linkTarget: p1.Id, rotation: 0);
        await h.Pages.AddElementAsync(p2.Id, ElementKind.Photo, new PhotoData { Style = "round" }, photo.Id, rotation: 3); // same media → dedup
        await h.Pages.EnableShareAsync(p2.Id);
    }

    private static async Task<string> NormalizedAsync(TestHost h)
    {
        await using var db = await h.Db.CreateDbContextAsync();
        var data = await DayleafExporter.BuildDataAsync(db, ExportScope.Library, null, default);
        var sha = data.Media.ToDictionary(m => m.Id, m => m.Sha256);
        var pageDates = data.Spaces.SelectMany(s => s.Pages).ToDictionary(p => p.Id, p => p.Date);
        var shape = data.Spaces.OrderBy(s => s.Name).Select(s => new
        {
            s.Name, s.Emoji, s.Color, s.Description, s.DefaultTheme, Cover = s.CoverMediaId is { } c ? sha[c] : null, s.CreatedAt,
            Pages = s.Pages.OrderBy(p => p.Date).Select(p => new
            {
                p.Date, p.Title, p.Theme, p.Tags, p.SheetCount, p.CreatedAt,
                Sections = p.Sections.Select(x => new { x.Seq, x.Label, x.CreatedAt, x.SubmittedAt, Elements = p.Elements.Count(e => e.SectionId == x.Id) }),
                Elements = p.Elements.OrderBy(e => e.Sheet).ThenBy(e => e.Z).Select(e => new
                {
                    e.Kind, e.Sheet, e.X, e.Y, e.W, e.H, e.Rotation, e.Z, Data = e.Data.GetRawText(),
                    Media = e.MediaId is { } m ? sha[m] : null,
                    Link = e.LinkTargetPageId is { } l && pageDates.TryGetValue(l, out var d) ? d.ToString("O") : null,
                    e.CreatedAt, e.UpdatedAt,
                }),
            }),
        });
        return JsonSerializer.Serialize(shape, new JsonSerializerOptions { WriteIndented = true });
    }

    [Fact]
    public async Task Library_round_trip_is_identical_apart_from_ids()
    {
        using var src = new TestHost();
        await BuildLibraryAsync(src);
        var file = Path.Combine(src.Paths.DataDirectory, "lib.dayleaf");
        await using (var fs = File.Create(file))
            await src.Exporter().WriteAsync(fs, ExportScope.Library);

        using var dst = new TestHost();
        var summary = await dst.Importer().ImportAsync(file);
        Assert.Equal(2, summary.Spaces);
        Assert.Equal(2, summary.Pages);
        Assert.Equal(2, summary.Photos);

        Assert.Equal(await NormalizedAsync(src), await NormalizedAsync(dst));

        // Ids are new, media deduplicated, share tokens never travel.
        await using var db = await dst.Db.CreateDbContextAsync();
        Assert.Single(db.Media);
        Assert.All(db.Pages, p => Assert.Null(p.ShareToken));
        await using var sdb = await src.Db.CreateDbContextAsync();
        Assert.Empty(db.Pages.Select(p => p.Id).ToList().Intersect(sdb.Pages.Select(p => p.Id).ToList()));
    }

    [Fact]
    public async Task Locks_survive_the_round_trip()
    {
        using var src = new TestHost();
        await BuildLibraryAsync(src);
        src.Clock.Advance(TimeSpan.FromHours(7));
        var file = Path.Combine(src.Paths.DataDirectory, "lib.dayleaf");
        await using (var fs = File.Create(file))
            await src.Exporter().WriteAsync(fs, ExportScope.Library);

        using var dst = new TestHost();
        dst.Clock.Advance(TimeSpan.FromHours(7));
        await dst.Importer().ImportAsync(file);
        await using var db = await dst.Db.CreateDbContextAsync();
        var farmPage = db.Pages.Single(p => p.Title.StartsWith("Starting"));
        var text = db.Elements.Single(e => e.PageId == farmPage.Id && e.Kind == ElementKind.Text);
        await Assert.ThrowsAsync<SectionLockedException>(() => dst.Pages.UpdateElementDataAsync(text.Id, new TextData { Html = "changed" }));
    }

    [Fact]
    public async Task Importing_twice_renames_spaces_and_page_import_refuses_date_clash()
    {
        using var h = new TestHost();
        await BuildLibraryAsync(h);
        var file = Path.Combine(h.Paths.DataDirectory, "lib.dayleaf");
        await using (var fs = File.Create(file))
            await h.Exporter().WriteAsync(fs, ExportScope.Library);
        await h.Importer().ImportAsync(file);
        var spaces = await h.Spaces.ListAsync();
        Assert.Contains(spaces, s => s.Space.Name.StartsWith("Our Farm (imported "));
        Assert.Equal(spaces.Count, spaces.Select(s => s.Space.Slug).Distinct().Count());

        await using var db = await h.Db.CreateDbContextAsync();
        var page = db.Pages.First(p => p.Title.StartsWith("Starting"));
        var pageFile = Path.Combine(h.Paths.DataDirectory, "page.dayleaf");
        await using (var fs = File.Create(pageFile))
            await h.Exporter().WriteAsync(fs, ExportScope.Page, page.Id);
        var ex = await Assert.ThrowsAsync<ImportRejectedException>(() => h.Importer().ImportAsync(pageFile, page.SpaceId));
        Assert.Contains("already has a page", ex.Message);

        var empty = await h.Spaces.CreateAsync("Empty", "📓", "ink", null, null);
        var ok = await h.Importer().ImportAsync(pageFile, empty.Id);
        Assert.Equal(1, ok.Pages);
    }
}

public class ImporterSecurityTests : IDisposable
{
    private readonly TestHost _h = new();
    public void Dispose() => _h.Dispose();

    private string Archive(Action<ZipArchive> build)
    {
        var path = Path.Combine(_h.Paths.DataDirectory, $"{Guid.NewGuid():N}.dayleaf");
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        build(zip);
        return path;
    }

    private static void Text(ZipArchive zip, string name, string content, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var w = new StreamWriter(zip.CreateEntry(name, level).Open(), Encoding.UTF8);
        w.Write(content);
    }

    private const string Manifest = """{"format":"dayleaf","version":1,"kind":"library","exportedAt":"2026-11-23T00:00:00Z"}""";

    private static string Data(string mediaFile, string html) => $$"""
        {"spaces":[{"id":"11111111-1111-1111-1111-111111111111","name":"Evil","slug":"evil","emoji":"😈","color":"clay","defaultTheme":"field","createdAt":"2026-01-01T00:00:00Z",
          "pages":[{"id":"22222222-2222-2222-2222-222222222222","date":"2026-01-01","title":"t","theme":"field","tags":[],"sheetCount":1,"createdAt":"2026-01-01T00:00:00Z",
            "sections":[{"id":"33333333-3333-3333-3333-333333333333","seq":1,"label":"Morning","createdAt":"2026-01-01T00:00:00Z","submittedAt":null}],
            "elements":[
              {"id":"44444444-4444-4444-4444-444444444444","sectionId":"33333333-3333-3333-3333-333333333333","kind":"text","sheet":0,"x":99999,"y":-99999,"w":440,"h":120,"rotation":0,"z":1,
               "data":{"html":"{{html}}"},"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"},
              {"id":"55555555-5555-5555-5555-555555555555","sectionId":"33333333-3333-3333-3333-333333333333","kind":"photo","sheet":0,"x":10,"y":10,"w":100,"h":100,"rotation":0,"z":2,
               "mediaId":"66666666-6666-6666-6666-666666666666","data":{"style":"sticker"},"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}
            ]}]}],
         "media":[{"id":"66666666-6666-6666-6666-666666666666","file":"{{mediaFile}}","contentType":"image/png","sha256":"x","size":1,"originalName":"a.png"}]}
        """;

    [Fact]
    public async Task Zip_slip_entries_are_never_extracted()
    {
        var outside = Path.Combine(_h.Paths.DataDirectory, "..", "evil.png");
        var path = Archive(zip =>
        {
            Text(zip, "manifest.json", Manifest);
            Text(zip, "data.json", Data("../evil.png", "<p>hi</p>"));
            using (var s = zip.CreateEntry("../evil.png").Open()) s.Write(TestHost.TinyPng());
            using (var s = zip.CreateEntry("../../../../Windows/evil.txt").Open()) s.Write("x"u8);
        });

        var summary = await _h.Importer().ImportAsync(path);
        Assert.Equal(0, summary.Photos); // the photo referencing a bad path was dropped
        Assert.False(File.Exists(outside));
        Assert.Empty(Directory.GetFiles(_h.Paths.MediaDirectory));
    }

    [Fact]
    public async Task Zip_bomb_is_rejected()
    {
        var path = Archive(zip =>
        {
            Text(zip, "manifest.json", Manifest);
            Text(zip, "data.json", Data("media/00000000000000000000000000000000.png", "x"));
            Text(zip, "padding.bin", new string('\0', 5_000_000), CompressionLevel.SmallestSize);
        });
        var ex = await Assert.ThrowsAsync<ImportRejectedException>(() => _h.Importer().ImportAsync(path));
        Assert.Contains("unsafe", ex.Message);
    }

    [Fact]
    public async Task Unknown_version_is_rejected()
    {
        var path = Archive(zip =>
        {
            Text(zip, "manifest.json", """{"format":"dayleaf","version":2,"kind":"library"}""");
            Text(zip, "data.json", """{"spaces":[],"media":[]}""");
        });
        var ex = await Assert.ThrowsAsync<ImportRejectedException>(() => _h.Importer().ImportAsync(path));
        Assert.Contains("newer Dayleaf", ex.Message);
    }

    [Fact]
    public async Task Not_a_zip_is_rejected()
    {
        var path = Path.Combine(_h.Paths.DataDirectory, "fake.dayleaf");
        await File.WriteAllTextAsync(path, "hello");
        await Assert.ThrowsAsync<ImportRejectedException>(() => _h.Importer().ImportAsync(path));
    }

    [Fact]
    public async Task Script_in_data_json_is_stripped_and_coordinates_clamped()
    {
        var path = Archive(zip =>
        {
            Text(zip, "manifest.json", Manifest);
            Text(zip, "data.json", Data("media/66666666666666666666666666666666.png",
                "<p>ok<script>alert(1)<\\/script><img src=x onerror=alert(1)><a href='javascript:alert(1)'>x</a></p>"));
            using var s = zip.CreateEntry("media/66666666666666666666666666666666.png").Open();
            s.Write(TestHost.TinyPng(7));
        });
        var summary = await _h.Importer().ImportAsync(path);
        Assert.Equal(1, summary.Photos);
        await using var db = await _h.Db.CreateDbContextAsync();
        var text = db.Elements.Single(e => e.Kind == ElementKind.Text);
        Assert.DoesNotContain("script", text.DataJson);
        Assert.DoesNotContain("onerror", text.DataJson);
        Assert.DoesNotContain("javascript", text.DataJson);
        Assert.Equal(794 - 24, text.X);
        Assert.Equal(24 - 120, text.Y);
        Assert.Single(Directory.GetFiles(_h.Paths.MediaDirectory));
    }
}
