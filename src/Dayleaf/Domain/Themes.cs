namespace Dayleaf.Domain;

public sealed record ThemeInfo(string Id, string Name, string Paper, string Ink, string Accent, string Accent2, string TitleFont, string TitleStyle, string Blurb);

public static class Themes
{
    public const string Default = "field";

    public static readonly IReadOnlyList<ThemeInfo> All =
    [
        new("field", "Field", "#FBF7EF", "#1E2320", "#2E5A43", "#C9861F", "'Fraunces', serif", "normal", "Dot-grid notebook, forest & marigold"),
        new("pastel", "Pastel", "#FFFFFF", "#26215C", "#6E5BD0", "#F2A3B5", "'Space Grotesk', sans-serif", "bold", "Lilac band, soft pink tape"),
        new("sea", "Sea", "#F3F8F8", "#123E46", "#1F6E7A", "#E3B04B", "'Fraunces', serif", "italic", "Cool teal with a sandy accent"),
        new("sunset", "Sunset", "#FBEDE6", "#3A1E16", "#C2513A", "#E8A23A", "'Caveat', cursive", "hand", "Warm clay, handwritten titles"),
        new("night", "Night", "#1F2421", "#F4EEDF", "#E0B45A", "#8FB9A0", "'Fraunces', serif", "normal", "Dark paper, gold ink"),
        new("kraft", "Kraft", "#E6D3B3", "#2D2116", "#7A3E1D", "#3E6B5A", "'Fraunces', serif", "normal", "Brown kraft paper, scrapbook feel"),
        new("graph", "Graph", "#FDFDFB", "#1B2430", "#D2462F", "#2F6FDE", "'Space Grotesk', sans-serif", "bold", "Lab notebook on graph paper"),
    ];

    public static ThemeInfo Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static bool IsValid(string? id) => All.Any(t => t.Id == id);
}

public sealed record SpaceColor(string Id, string Name, string Hex, string Tint, string InkOnTint);

public static class SpaceColors
{
    public static readonly IReadOnlyList<SpaceColor> All =
    [
        new("marigold", "Marigold", "#C9861F", "#F3E3C6", "#6B4510"),
        new("forest", "Forest", "#2E5A43", "#DDE9E0", "#1F4A35"),
        new("lilac", "Lilac", "#6E5BD0", "#ECE8FA", "#44368F"),
        new("sea", "Sea", "#2E7D8C", "#DCEBEC", "#1F5A64"),
        new("clay", "Clay", "#C2513A", "#F4DCD3", "#7A2E1E"),
        new("plum", "Plum", "#6A3F84", "#EBDDF2", "#4A2560"),
        new("rose", "Rose", "#C2456E", "#F8DCE5", "#7A2440"),
        new("ink", "Ink", "#3C433E", "#E4E2DC", "#1E2320"),
    ];

    public static SpaceColor Get(string? id) => All.FirstOrDefault(c => c.Id == id) ?? All[1];
}
