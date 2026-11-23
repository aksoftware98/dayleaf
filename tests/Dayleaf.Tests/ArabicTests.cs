using System.Globalization;
using System.Text.RegularExpressions;
using Dayleaf.Components.Editor;
using Dayleaf.Components.Shared;
using Dayleaf.Domain;
using Dayleaf.Localization;
using Dayleaf.Services;

namespace Dayleaf.Tests;

public class ArabicTranslationTests
{
    private static string SrcRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dayleaf.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Dayleaf");
    }

    /// <summary>Every English key the app can show: literals in code plus names that come from data.</summary>
    public static IEnumerable<string> AllKeys()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        const string lit = "\"((?:[^\"\\\\]|\\\\.)*)\"";
        string[] patterns = [$@"L\[\s*{lit}", $@"Loc\.T\(\s*{lit}", $@"Loc\.TFor\([^,]+,\s*{lit}", $@"\bP\(\s*{lit}"];
        foreach (var file in Directory.EnumerateFiles(SrcRoot(), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".razor") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains("Migrations") && !f.EndsWith("Arabic.Strings.cs")))
        {
            var text = File.ReadAllText(file);
            foreach (var p in patterns)
                foreach (Match m in Regex.Matches(text, p))
                    if (!m.Groups[1].Value.EndsWith('|')) keys.Add(Regex.Unescape(m.Groups[1].Value)); // "font|" + name: added below
        }

        // Names that come from data rather than literals.
        foreach (var t in Themes.All) { keys.Add(t.Name); keys.Add(t.Blurb); }
        foreach (var c in SpaceColors.All) keys.Add(c.Name);
        foreach (var s in Palette.FixedInks.Concat(Palette.Papers).Concat(Palette.Tapes)) keys.Add(s.Name);
        keys.Add("No paper");
        foreach (var (_, l) in Palette.TextFonts) keys.Add("font|" + l);
        foreach (var f in Palette.PhotoFilters.Where(f => f != "none")) keys.Add("filter|" + Palette.Title(f));
        foreach (var v in Palette.DividerStyles.Concat(Palette.TapePatterns).Concat(Palette.Shapes)) keys.Add(Palette.Title(v));
        foreach (var k in Enum.GetValues<ElementKind>()) keys.Add(ElementInspector.NameFor(k));
        foreach (var c in EmojiCatalog.Categories) keys.Add(c.Name);
        foreach (var l in new[] { "Plain", "Die-cut", "Polaroid", "Taped", "Round", "Start", "Center", "End" }) keys.Add(l);
        foreach (var l in new[] { "Morning", "Afternoon", "Evening", "Night" }) keys.Add(l);
        foreach (var l in new[] { "☀️ Sunny", "🌧️ Rainy", "😊 Good day", "😴 Tired", "🎉 Milestone", "💡 Idea", "📍 Home", "✈️ Travel", "🌱 Fresh start", "🍂 Autumn", "❄️ Winter", "🌸 Spring" }) keys.Add(l);
        keys.Add("Only JPEG, PNG, WebP and GIF images can be dropped.");
        return keys;
    }

    [Fact]
    public void Every_UI_string_has_an_Arabic_translation()
    {
        var keys = AllKeys().ToList();
        Assert.True(keys.Count > 400, $"Only found {keys.Count} keys — is the scan broken?");
        var missing = keys.Where(k => !Arabic.Strings.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Missing Arabic for:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Translations_keep_the_same_placeholders()
    {
        var bad = Arabic.Strings
            .Where(kv => !Placeholders(kv.Key).SequenceEqual(Placeholders(kv.Value)))
            .Select(kv => kv.Key).ToList();
        Assert.True(bad.Count == 0, "Placeholder mismatch:\n" + string.Join("\n", bad));

        static IEnumerable<string> Placeholders(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order();
    }

    [Fact]
    public void Every_sticker_has_an_Arabic_name_and_Arabic_search_works()
    {
        Assert.All(EmojiCatalog.All, s => Assert.True(ArabicText.ContainsArabic(s.ArabicName), s.Name));
        Assert.Contains(EmojiCatalog.All, s => s.Char == "🚜" && s.Matches("جرار"));
        Assert.Contains(EmojiCatalog.All, s => s.Char == "🌳" && s.Matches("شجره")); // taa marbuta folded
    }

    [Fact]
    public void Context_keys_show_the_plain_word_in_English() =>
        Assert.Equal("Print", Loc.TFor(Lang.En, "font|Print"));

    [Theory]
    [InlineData(0, "0 صفحة")]
    [InlineData(1, "صفحة واحدة")]
    [InlineData(2, "صفحتان")]
    [InlineData(3, "3 صفحات")]
    [InlineData(10, "10 صفحات")]
    [InlineData(11, "11 صفحة")]
    [InlineData(103, "103 صفحات")]
    [InlineData(444, "444 صفحة")]
    public void Arabic_plurals_follow_the_six_forms(int n, string expected) =>
        Assert.Equal(expected, Loc.Plural(n, Noun.Page, Lang.Ar));

    [Fact]
    public void English_plurals_still_work() => Assert.Equal("1 page · 3 photos",
        $"{Loc.Plural(1, Noun.Page, Lang.En)} · {Loc.Plural(3, Noun.Photo, Lang.En)}");

    [Fact]
    public void Arabic_dates_use_Arabic_names_and_Western_digits()
    {
        var d = new DateOnly(2026, 11, 16);
        Assert.Equal("الاثنين، 16 نوفمبر 2026", Dates.Format(d, DateStyle.Long, Lang.Ar));
        Assert.Equal("نوفمبر", Dates.Format(d, DateStyle.Month, Lang.Ar));
        Assert.Equal("Mon · Nov 16, 2026", Dates.Format(d, DateStyle.Footer, Lang.En));
    }

    [Fact]
    public void Translation_follows_the_current_culture()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Assert.Equal("المساحات", Loc.T("Spaces"));
            Assert.Equal("صفحة 12", Loc.T("Page {0}", 12));
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal("Spaces", Loc.T("Spaces"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}

public class ArabicTextTests : IDisposable
{
    private readonly TestHost _h = new();
    public void Dispose() => _h.Dispose();

    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إسلام", "اسلام")]
    [InlineData("مدرسة", "مدرسه")]
    [InlineData("مُحَمَّد", "محمد")]
    [InlineData("علـــى", "علي")]
    [InlineData("٢٠٢٥", "2025")]
    [InlineData("Tractor", "tractor")]
    public void Normalize_folds_spelling_variants(string input, string expected) =>
        Assert.Equal(expected, ArabicText.Normalize(input));

    [Fact]
    public void FindMatches_points_into_the_original_text()
    {
        var text = "زرعنا شجرةً جديدة";
        var m = Assert.Single(ArabicText.FindMatches(text, ["شجرة"]));
        Assert.Equal("شجرةً", text.Substring(m.Start, m.Length));
    }

    [Fact]
    public async Task Search_finds_Arabic_text_however_it_was_typed()
    {
        var (_, page) = await _h.NewPageAsync(date: new DateOnly(2025, 6, 11));
        await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "<p>قال أحمد: المدرسةُ جميلة</p>" });
        Assert.Single(await _h.Search.SearchAsync("احمد"));
        Assert.Single(await _h.Search.SearchAsync("المدرسه"));
        Assert.Single(await _h.Search.SearchAsync("حزيران 2025"));
        Assert.Single(await _h.Search.SearchAsync("يونيو ٢٠٢٥"));
        Assert.Empty(await _h.Search.SearchAsync("تموز 2025"));
    }

    [Theory]
    [InlineData("يونيو 2025", 2025, 6, null)]
    [InlineData("حزيران 2025", 2025, 6, null)]
    [InlineData("11 حزيران 2025", 2025, 6, 11)]
    [InlineData("تشرين الثاني 2026", 2026, 11, null)]
    [InlineData("١٦ تشرين الثاني ٢٠٢٦", 2026, 11, 16)]
    [InlineData("كانون الثاني 2024", 2024, 1, null)]
    [InlineData("آب 2025", 2025, 8, null)]
    public void Arabic_date_queries_parse(string q, int y, int? m, int? d) =>
        Assert.Equal(new DateQuery(y, m, d), DateQuery.TryParse(q));

    [Fact]
    public async Task Arabic_space_names_get_readable_unique_slugs()
    {
        var a = await _h.Spaces.CreateAsync("مزرعتنا", "🌳", "forest", null, null, Lang.Ar);
        var b = await _h.Spaces.CreateAsync("مزرعتنا", "🌳", "forest", null, null, Lang.Ar);
        Assert.Equal("مزرعتنا", a.Slug);
        Assert.Equal("مزرعتنا-2", b.Slug);
        Assert.Equal("أحلام-2026".Replace("أ", "ا"), Slug.From("أحلام ٢٠٢٦"));
    }

    [Fact]
    public async Task Arabic_spaces_make_Arabic_pages_with_Arabic_sections_placed_on_the_right()
    {
        var space = await _h.Spaces.CreateAsync("يومياتي", "📓", "sea", null, null, Lang.Ar);
        Assert.Equal(Lang.Ar, space.DefaultLanguage);
        var page = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 11, 23));
        Assert.Equal(Lang.Ar, page.Language);
        Assert.True(ArabicText.ContainsArabic(page.Sections.Single().Label));

        var el = await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "<p>مرحبا</p>" });
        Assert.Equal(LayoutService.SheetW - LayoutService.SideMargin - el.W, el.X);

        var model = await _h.Pages.GetSheetModelAsync(page.Id);
        Assert.Equal(Lang.Ar, model!.Language);

        await _h.Pages.UpdatePageMetaAsync(page.Id, "", "field", [], null, Lang.En);
        Assert.Equal(Lang.En, (await _h.Pages.GetAsync(page.Id))!.Language);
    }
}
