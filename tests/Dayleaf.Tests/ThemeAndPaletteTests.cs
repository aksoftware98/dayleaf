using Dayleaf.Domain;
using Dayleaf.Services;

namespace Dayleaf.Tests;

/// <summary>Every theme and palette id the editor offers must have CSS behind it and survive validation.</summary>
public class ThemeAndPaletteTests
{
    private static readonly string SheetCss = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Dayleaf", "wwwroot", "sheet.css"));

    // Start from this source file, not the binaries: builds may live outside the repo (--artifacts-path).
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dayleaf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    public static TheoryData<string> ThemeIds() => [.. Themes.All.Where(t => t.Id != Themes.Default).Select(t => t.Id)];

    [Theory]
    [MemberData(nameof(ThemeIds))]
    public void Every_theme_has_css(string id) => Assert.Contains($".theme-{id} {{", SheetCss);

    [Fact]
    public void Theme_and_space_color_ids_are_unique()
    {
        Assert.Equal(Themes.All.Count, Themes.All.Select(t => t.Id).Distinct().Count());
        Assert.Equal(SpaceColors.All.Count, SpaceColors.All.Select(c => c.Id).Distinct().Count());
        Assert.True(Themes.All.Count >= 26);
        Assert.All(Themes.All, t => Assert.Contains(Themes.Groups, g => g.Id == t.Group));
        Assert.True(SpaceColors.All.Count >= 16);
    }

    [Fact]
    public void No_rule_on_an_element_wrapper_class_moves_it()
    {
        // The wrapper of each element carries "el-{kind}". A left/right/inset rule on that class fights the inline
        // left/top — and on right-to-left pages CSS then ignores "left", pinning the element to the right edge.
        foreach (var kind in Enum.GetValues<ElementKind>())
        {
            var cls = $"el-{kind.ToString().ToLowerInvariant()}";
            var bad = System.Text.RegularExpressions.Regex.Matches(SheetCss, $@"\.{cls}\b[^{{,]*\{{[^}}]*\b(inset|left|right)\s*:");
            Assert.True(bad.Count == 0, $".{cls} must not set inset/left/right");
        }
    }

    [Fact]
    public void Every_palette_id_has_a_css_class()
    {
        foreach (var ink in Palette.FixedInks) { Assert.Contains($".tx-color-{ink.Id}", SheetCss); Assert.Contains($".sh-c-{ink.Id}", SheetCss); }
        foreach (var p in Palette.Papers) Assert.Contains($".note-{p.Id}", SheetCss);
        foreach (var t in Palette.Tapes.Where(t => t.Id != "theme")) Assert.Contains($".tp-{t.Id}", SheetCss);
        foreach (var pat in Palette.TapePatterns.Where(p => p != "plain")) Assert.Contains($".tp-pat-{pat}", SheetCss);
        foreach (var f in Palette.PhotoFilters.Where(f => f != "none")) Assert.Contains($".ph-fx-{f}", SheetCss);
        foreach (var (id, _) in Palette.TextFonts.Where(f => f.Id != "body")) Assert.Contains($".tx-font-{id}", SheetCss);
    }

    [Fact]
    public void New_palette_values_survive_normalization_and_unknown_ones_fall_back()
    {
        var text = new TextData { Color = "teal", Font = "classic" };
        var note = new NoteData { Color = "ink" };
        var tape = new TapeData { Color = "kraft", Pattern = "gingham" };
        var shape = new ShapeData { Shape = "heart", Color = "pink" };
        var photo = new PhotoData { Filter = "golden" };
        foreach (ElementData d in new ElementData[] { text, note, tape, shape, photo }) PageService.Normalize(d);
        Assert.Equal(("teal", "classic"), (text.Color, text.Font));
        Assert.Equal("ink", note.Color);
        Assert.Equal(("kraft", "gingham"), (tape.Color, tape.Pattern));
        Assert.Equal(("heart", "pink"), (shape.Shape, shape.Color));
        Assert.Equal("golden", photo.Filter);

        var bad = new TextData { Color = "url(javascript:x)", Font = "comic" };
        PageService.Normalize(bad);
        Assert.Equal(("ink", "body"), (bad.Color, bad.Font));
    }
}
