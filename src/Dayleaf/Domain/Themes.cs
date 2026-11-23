namespace Dayleaf.Domain;

/// <param name="Group">"classic", "modern" or "cities" — how the theme picker groups them.</param>
/// <param name="Preview">CSS background for the picker card (defaults to <paramref name="Paper"/>).</param>
public sealed record ThemeInfo(string Id, string Name, string Paper, string Ink, string Accent, string Accent2, string TitleFont, string TitleStyle, string Blurb,
    string Group = "classic", string? Preview = null)
{
    public string PreviewBackground => Preview ?? Paper;
}

public sealed record ThemeGroup(string Id, string Name);

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

        // ── Modern ──────────────────────────────────────────────────────────
        new("metro", "Metro", "#FFFFFF", "#1D1D1D", "#0050EF", "#D80073", "'Segoe UI Variable Display', 'Segoe UI', 'Outfit', sans-serif", "light",
            "Windows 8 tiles: flat, bold colour, light type", "modern",
            "linear-gradient(#0050EF 0 46%, #fff 46%) , #fff"),
        new("fluent", "Fluent", "#F3F6FB", "#1B1B1F", "#0067C0", "#8764B8", "'Segoe UI Variable Display', 'Segoe UI', 'Outfit', sans-serif", "bold",
            "Windows 11 bloom, soft acrylic cards", "modern",
            "radial-gradient(120% 90% at 100% 0%, #5AA0FF 0, rgba(90,160,255,0) 60%), radial-gradient(90% 80% at 0% 100%, #C2A8FF 0, rgba(194,168,255,0) 60%), #F3F6FB"),
        new("glass", "Liquid Glass", "#EDE7FF", "#1A1530", "#6B3DF5", "#FF4FA3", "-apple-system, 'SF Pro Display', 'Outfit', sans-serif", "bold",
            "Frosted glass on a vivid gradient, Apple style", "modern",
            "radial-gradient(at 15% 15%, #FF9ACB 0, transparent 50%), radial-gradient(at 90% 20%, #8EC5FF 0, transparent 50%), radial-gradient(at 70% 90%, #B79CFF 0, transparent 55%), radial-gradient(at 5% 95%, #7FF0D2 0, transparent 50%), #EDE7FF"),
        new("glassnight", "Glass Night", "#0B0B1E", "#F4F2FF", "#9D7BFF", "#2EE6FF", "-apple-system, 'SF Pro Display', 'Outfit', sans-serif", "bold",
            "Dark liquid glass with violet and cyan glow", "modern",
            "radial-gradient(at 20% 20%, #5B2EFF 0, transparent 50%), radial-gradient(at 85% 30%, #FF2E93 0, transparent 45%), radial-gradient(at 60% 95%, #00D1FF 0, transparent 50%), #0B0B1E"),
        new("pop", "Pop", "#FFFDF5", "#111111", "#2D7DD2", "#FF4F79", "'Outfit', sans-serif", "bold",
            "Memphis shapes and neo-brutal outlines", "modern",
            "radial-gradient(circle at 80% 25%, #FFD23F 0 14%, transparent 14.5%), radial-gradient(circle at 20% 75%, #3BCEAC 0 12%, transparent 12.5%), linear-gradient(135deg, transparent 60%, #FF4F79 60% 68%, transparent 68%), #FFFDF5"),
        new("aurora", "Aurora", "#EEF6FB", "#102A43", "#0E9F8E", "#7B4DDB", "'Outfit', sans-serif", "bold",
            "Northern lights in teal and violet", "modern",
            "radial-gradient(80% 50% at 20% 10%, #7FE7D2 0, transparent 70%), radial-gradient(70% 50% at 85% 25%, #B9A2FF 0, transparent 70%), #EEF6FB"),

        // ── Cities ──────────────────────────────────────────────────────────
        new("newyork", "New York", "#F7F5EF", "#111111", "#0039A6", "#F7B500", "'Anton', 'Impact', sans-serif", "bold",
            "Taxi yellow, subway lines, asphalt black", "cities",
            "linear-gradient(#111 0 44%, #F7B500 44% 52%, #F7F5EF 52%)"),
        new("tokyo", "Tokyo", "#0D0B1A", "#F2F0FF", "#FF2BD6", "#00E5FF", "'Orbitron', sans-serif", "bold",
            "Neon nights on a glowing grid", "cities",
            "linear-gradient(rgba(255,43,214,.25) 1px, transparent 1px) 0 0 / 12px 12px, linear-gradient(90deg, rgba(0,229,255,.25) 1px, transparent 1px) 0 0 / 12px 12px, #0D0B1A"),
        new("paris", "Paris", "#FDF6F0", "#1F2A44", "#B23A48", "#C9A24B", "'Playfair Display', serif", "italic",
            "Breton stripes, bordeaux and gold", "cities",
            "repeating-linear-gradient(#1F2A44 0 3px, #FDF6F0 3px 8px) top / 100% 30% no-repeat, #FDF6F0"),
        new("miami", "Miami", "#FFF1EC", "#2D1E4A", "#FF4F9A", "#00B3B8", "'Pacifico', cursive", "hand",
            "Retro sunset, pink and teal", "cities",
            "radial-gradient(circle at 75% 35%, #FFC24B 0 18%, transparent 18.5%), linear-gradient(#FFD6E7, #FFF4E6 60%, #CFF7F5)"),
        new("dubai", "Dubai", "#FBF4E8", "#2A1E10", "#A8740A", "#0F6E6E", "'Playfair Display', serif", "normal",
            "Desert gold, geometric stars, skyline", "cities",
            "linear-gradient(transparent 70%, rgba(168,116,10,.35) 70%), linear-gradient(#FBF4E8, #F1DEC0)"),
    ];

    public static readonly IReadOnlyList<ThemeGroup> Groups =
    [
        new("modern", "Modern"),
        new("cities", "Cities"),
        new("classic", "Classic"),
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
