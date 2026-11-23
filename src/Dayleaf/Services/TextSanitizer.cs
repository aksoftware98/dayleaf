using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Dayleaf.Services;

/// <summary>Sanitizes rich text: only b i u s br p ul ol li a[href] (plus a few harmless aliases) survive.</summary>
public static partial class TextSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in new[] { "b", "strong", "i", "em", "u", "s", "strike", "br", "p", "div", "ul", "ol", "li", "a", "mark" })
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");
        s.AllowedCssProperties.Clear();
        s.AllowedAtRules.Clear();
        s.AllowedClasses.Clear();
        s.KeepChildNodes = true;
        s.UriAttributes.Clear();
        s.UriAttributes.Add("href");
        return s;
    }

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        // Containers whose *content* is never text: drop them whole before sanitizing (KeepChildNodes would keep their text).
        html = DangerousBlocks().Replace(html, "");
        var clean = Sanitizer.Sanitize(html);
        // Collapse empty trailing breaks produced by contenteditable.
        clean = TrailingBreaks().Replace(clean, "");
        return clean.Trim();
    }

    /// <summary>Plain text for search and snippets.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var withSpaces = BlockTags().Replace(html, " ");
        var noTags = AnyTag().Replace(withSpaces, "");
        return Whitespace().Replace(WebUtility.HtmlDecode(noTags), " ").Trim();
    }

    /// <summary>Escape plain text and turn newlines into &lt;br&gt;.</summary>
    public static string FromPlainText(string? text) =>
        string.IsNullOrEmpty(text) ? "" : WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>");

    [GeneratedRegex(@"<\s*(script|style|iframe|object|embed|noscript|template|svg|math|textarea|select)\b[\s\S]*?(<\s*/\s*\1\s*>|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousBlocks();

    [GeneratedRegex(@"(<br\s*/?>\s*)+$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingBreaks();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|p|div|li)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTags();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
