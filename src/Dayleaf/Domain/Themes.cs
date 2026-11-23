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
        new("blossom", "Blossom", "#FFF5F6", "#4A2030", "#C94A74", "#7FA889", "'Playfair Display', serif", "italic", "Cherry-blossom pink with soft petals"),
        new("matcha", "Matcha", "#F3F5EA", "#223020", "#557A35", "#C08F4F", "'Fraunces', serif", "normal", "Tea-green dot grid, calm and earthy"),
        new("noir", "Noir", "#FFFFFF", "#111111", "#111111", "#E03A2C", "'Playfair Display', serif", "bold", "Black & white editorial, one red accent"),
        new("retro", "Retro", "#F6EAD6", "#2B2622", "#D25B26", "#2D7A75", "'Space Grotesk', sans-serif", "bold", "70s stripes in orange, mustard and teal"),
        new("lavender", "Lavender", "#F7F4FC", "#2F2545", "#7655BC", "#D98DB7", "'Caveat', cursive", "hand", "Lilac ruled paper with a hand-written title"),
        new("ocean", "Ocean", "#0F2433", "#EAF4F7", "#5FC3D6", "#F2C14E", "'Fraunces', serif", "italic", "Deep navy night sea with sunny gold"),
        new("vintage", "Vintage", "#EFE3CB", "#3B2E22", "#8C3B2A", "#4F6B5A", "'Playfair Display', serif", "normal", "Aged sepia paper with a soft vignette"),
        new("citrus", "Citrus", "#FFFBEA", "#2A2A1E", "#E2721A", "#3C9A49", "'Patrick Hand', cursive", "hand", "Sunny lemon paper, orange and lime"),
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
        new("teal", "Teal", "#1B8A86", "#D3EEEC", "#0F5552"),
        new("moss", "Moss", "#5F7A2E", "#E5EDD3", "#3B4D1B"),
        new("mustard", "Mustard", "#B98A12", "#F6E9C2", "#6A4E07"),
        new("coral", "Coral", "#E0664F", "#FBE0D9", "#8A3020"),
        new("sky", "Sky", "#3B82C4", "#DCEAF7", "#1E4E7C"),
        new("berry", "Berry", "#9B2D5B", "#F3DCE6", "#5E1636"),
        new("sand", "Sand", "#A88A62", "#EFE6D8", "#5C4630"),
        new("slate", "Slate", "#4A5A6A", "#E1E6EB", "#26313C"),
    ];

    public static SpaceColor Get(string? id) => All.FirstOrDefault(c => c.Id == id) ?? All[1];
}
