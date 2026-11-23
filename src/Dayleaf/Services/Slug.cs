using System.Globalization;
using System.Text;

namespace Dayleaf.Services;

public static class Slug
{
    public static string From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "space";
        // Arabic letters are kept (browsers show them as-is in the address bar); hamza forms and
        // tashkeel are folded so "أحمد" and "احمد" give the same slug. Arabic-Indic digits become 0-9.
        var normalized = ArabicText.Normalize(text).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var lastDash = false;
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c) && (c < 128 || c is >= '\u0621' and <= '\u064A' or >= '\u0671' and <= '\u06D3'))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastDash = false;
            }
            else if (!lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > 60) slug = slug[..60].Trim('-');
        return slug.Length == 0 ? "space" : slug;
    }
}
