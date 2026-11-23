namespace Dayleaf.Domain;

/// <summary>
/// Every user-pickable colour, font and style id in one place. The editor renders its pickers from these
/// lists and <c>PageService.Normalize</c> validates against them, so the two can never drift apart.
/// CSS classes for each id live in sheet.css.
/// </summary>
public static class Palette
{
    public sealed record Swatch(string Id, string Name, string Hex);

    /// <summary>Theme-aware inks: their colour comes from the page theme.</summary>
    public static readonly IReadOnlyList<string> ThemeInks = ["ink", "accent", "accent2", "muted"];

    /// <summary>Fixed inks that look the same on every theme.</summary>
    public static readonly IReadOnlyList<Swatch> FixedInks =
    [
        new("red", "Tomato", "#C8382B"),
        new("orange", "Pumpkin", "#D9622B"),
        new("gold", "Mustard", "#B98A12"),
        new("green", "Leaf", "#2E8B57"),
        new("teal", "Teal", "#138A8A"),
        new("blue", "Cobalt", "#2F6FDE"),
        new("indigo", "Indigo", "#3F4FB5"),
        new("purple", "Grape", "#7B4FC0"),
        new("pink", "Rose", "#D24D8F"),
        new("brown", "Cocoa", "#7A4E2D"),
        new("gray", "Pebble", "#6B6F72"),
        new("white", "Chalk", "#FFFFFF"),
    ];

    public static readonly string[] Inks = [.. ThemeInks, .. FixedInks.Select(s => s.Id)];

    /// <summary>Theme-aware fills for shapes, plus every fixed ink.</summary>
    public static readonly string[] ShapeColors = ["accent", "accent2", "chip", "note", "ink", .. FixedInks.Select(s => s.Id)];

    /// <summary>Sticky note and checklist papers.</summary>
    public static readonly IReadOnlyList<Swatch> Papers =
    [
        new("yellow", "Butter", "#F6E7B8"),
        new("lemon", "Lemon", "#FBF1A0"),
        new("peach", "Peach", "#FBDCC4"),
        new("coral", "Coral", "#F7C6BC"),
        new("pink", "Blush", "#F9D7DF"),
        new("lilac", "Lilac", "#E6E0FA"),
        new("sky", "Sky", "#D6E8F5"),
        new("mint", "Mint", "#D5EBDC"),
        new("sage", "Sage", "#DDE5CF"),
        new("kraft", "Kraft", "#D9BF97"),
        new("cloud", "Cloud", "#E6E8EB"),
        new("white", "White", "#FFFFFF"),
        new("ink", "Blackboard", "#2B2F33"),
    ];

    public static readonly string[] NoteColors = [.. Papers.Select(p => p.Id)];
    public static readonly string[] ChecklistColors = [.. NoteColors, "paper"];

    public static readonly IReadOnlyList<Swatch> Tapes =
    [
        new("theme", "Theme", "#ECD698"),
        new("yellow", "Butter", "#ECD698"),
        new("peach", "Peach", "#F6C49E"),
        new("coral", "Coral", "#F2A08F"),
        new("pink", "Pink", "#F2A3B5"),
        new("lilac", "Lilac", "#BEAFF0"),
        new("sky", "Sky", "#96C3E6"),
        new("mint", "Mint", "#A0D2B4"),
        new("sage", "Sage", "#B9C99E"),
        new("kraft", "Kraft", "#C8A577"),
        new("white", "Paper", "#FFFFFF"),
        new("black", "Ink", "#2B2F33"),
    ];

    public static readonly string[] TapeColors = [.. Tapes.Select(t => t.Id)];
    public static readonly string[] TapePatterns = ["plain", "stripes", "dots", "grid", "gingham", "zigzag", "hearts"];

    public static readonly (string Id, string Name)[] TextFonts =
    [
        ("body", "Body"), ("display", "Display"), ("hand", "Hand"), ("print", "Print"), ("classic", "Classic"), ("mono", "Mono"),
    ];

    public static readonly string[] TextFontIds = [.. TextFonts.Select(f => f.Id)];

    public static readonly string[] PhotoFilters = ["none", "warm", "golden", "cool", "dream", "mono", "noir", "fade", "vivid", "sepia"];
    public static readonly string[] DividerStyles = ["line", "double", "dashed", "dots", "wave", "stars", "hearts", "leaves"];
    public static readonly string[] Shapes = ["blob", "circle", "rect", "arch", "star", "heart", "cloud", "burst"];

    public static string Title(string id) => id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..];
}
