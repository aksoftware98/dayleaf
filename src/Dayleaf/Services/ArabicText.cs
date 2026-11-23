using System.Text;

namespace Dayleaf.Services;

/// <summary>
/// Arabic-aware text matching. Folds the spellings people type interchangeably so search finds them:
/// أ إ آ ٱ → ا, ة → ه, ى → ي, ؤ → و, ئ → ي; drops tashkeel (harakat) and tatweel; Arabic-Indic digits → 0-9;
/// and lower-cases Latin text. <see cref="Normalize(string, out int[])"/> also returns where each output
/// character came from, so matches can be highlighted in the original text.
/// </summary>
public static class ArabicText
{
    public static string Normalize(string? s) => Normalize(s ?? "", out _);

    public static string Normalize(string s, out int[] map)
    {
        var sb = new StringBuilder(s.Length);
        var m = new List<int>(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (IsDiacritic(c) || c == 'ـ') continue; // harakat, superscript alef, tatweel
            var f = Fold(c);
            sb.Append(f);
            m.Add(i);
        }
        map = [.. m];
        return sb.ToString();
    }

    public static bool ContainsArabic(string? s) => s is not null && s.Any(c => c is >= '؀' and <= 'ۿ');

    private static bool IsDiacritic(char c) => c is >= 'ً' and <= 'ٟ' or 'ٰ' or >= 'ۖ' and <= 'ۭ';

    private static char Fold(char c) => c switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
        'ة' => 'ه',
        'ى' => 'ي',
        'ؤ' => 'و',
        'ئ' => 'ي',
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')), // Arabic-Indic digits
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')), // Eastern Arabic-Indic digits
        _ => char.ToLowerInvariant(c),
    };

    /// <summary>Find every match of <paramref name="terms"/> in <paramref name="text"/>, as (start, length) in the original text.</summary>
    public static List<(int Start, int Length)> FindMatches(string text, IEnumerable<string> terms)
    {
        var norm = Normalize(text, out var map);
        var found = new List<(int, int)>();
        foreach (var t in terms)
        {
            var nt = Normalize(t);
            if (nt.Length == 0) continue;
            var at = 0;
            while ((at = norm.IndexOf(nt, at, StringComparison.Ordinal)) >= 0)
            {
                var start = map[at];
                var endIdx = at + nt.Length - 1;
                var end = endIdx + 1 < map.Length ? map[endIdx + 1] : text.Length;
                found.Add((start, end - start));
                at += nt.Length;
            }
        }
        // Merge overlaps, keep order.
        var merged = new List<(int Start, int Length)>();
        foreach (var (s, l) in found.OrderBy(x => x.Item1))
        {
            if (merged.Count > 0 && s <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                merged[^1] = (last.Start, Math.Max(last.Start + last.Length, s + l) - last.Start);
            }
            else merged.Add((s, l));
        }
        return merged;
    }
}
