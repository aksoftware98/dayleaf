namespace Dayleaf.Localization;

/// <summary>The app's own look (not the page themes): paper (default), light, dark or glass.</summary>
public static class Appearance
{
    public const string CookieName = "dayleaf.look";

    public static readonly (string Id, string Name)[] All =
    [
        ("paper", "Paper"), ("light", "Light"), ("dark", "Dark"), ("glass", "Glass"),
    ];

    public static string Normalize(string? id) => All.Any(a => a.Id == id) ? id! : "paper";
}
