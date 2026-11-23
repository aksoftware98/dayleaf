using System.Globalization;
using System.Text.RegularExpressions;
using Dayleaf.Data;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services;

/// <summary>Parses "2025", "2025-06", "2025-06-11", "june 2025", "jun 11 2025", "11 june 2025".</summary>
public sealed partial record DateQuery(int Year, int? Month, int? Day)
{
    public bool Matches(DateOnly d) => d.Year == Year && (Month is null || d.Month == Month) && (Day is null || d.Day == Day);

    public static DateQuery? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().ToLowerInvariant().Replace(",", " ");

        var iso = Iso().Match(text);
        if (iso.Success)
        {
            var y = int.Parse(iso.Groups[1].Value);
            int? m = iso.Groups[2].Success ? int.Parse(iso.Groups[2].Value) : null;
            int? d = iso.Groups[3].Success ? int.Parse(iso.Groups[3].Value) : null;
            if (m is < 1 or > 12 || d is < 1 or > 31) return null;
            return new DateQuery(y, m, d);
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int? year = null, month = null, day = null;
        foreach (var p in parts)
        {
            if (p.Length == 4 && int.TryParse(p, out var yy) && yy is > 1900 and < 2200) year = yy;
            else if (int.TryParse(p.TrimEnd('s', 't', 'n', 'd', 'r', 'h'), out var dd) && dd is >= 1 and <= 31) day = dd;
            else if (MonthOf(p) is { } mm) month = mm;
            else return null;
        }
        if (year is null) return null;
        if (day is not null && month is null) return null;
        return new DateQuery(year.Value, month, day);
    }

    private static int? MonthOf(string s)
    {
        if (s.Length < 3) return null;
        var names = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;
        for (var i = 0; i < 12; i++)
            if (names[i].StartsWith(s.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return null;
    }

    [GeneratedRegex(@"^(\d{4})(?:-(\d{1,2})(?:-(\d{1,2}))?)?$")]
    private static partial Regex Iso();
}

public interface ISearchService
{
    Task<List<SearchHit>> SearchAsync(string query, int take = 60);
}

public sealed class SearchService(IDbContextFactory<DayleafDbContext> dbf) : ISearchService
{
    public async Task<List<SearchHit>> SearchAsync(string query, int take = 60)
    {
        query = (query ?? "").Trim();
        if (query.Length == 0) return [];
        await using var db = await dbf.CreateDbContextAsync();

        var dq = DateQuery.TryParse(query);
        var terms = dq is null
            ? query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(t => t.Length > 0).Distinct().ToList()
            : [];

        var pages = await db.Pages.AsNoTracking().Select(p => new { p.Id, p.Title, p.Date, p.Tags }).ToListAsync();
        var texts = await db.Elements.AsNoTracking().Where(e => e.PlainText != null)
            .Select(e => new { e.PageId, e.PlainText }).ToListAsync();
        var textByPage = texts.GroupBy(t => t.PageId).ToDictionary(g => g.Key, g => string.Join("  ", g.Select(x => x.PlainText)));

        var matches = new List<(Guid Id, string Snippet, bool TitleMatch, int Score)>();
        foreach (var p in pages)
        {
            textByPage.TryGetValue(p.Id, out var body);
            body ??= "";
            if (dq is not null)
            {
                if (dq.Matches(p.Date)) matches.Add((p.Id, Snip(body, null), false, 1));
                continue;
            }
            var hay = $"{p.Title} {string.Join(' ', p.Tags)} {body}";
            if (!terms.All(t => hay.Contains(t, StringComparison.CurrentCultureIgnoreCase))) continue;
            var titleMatch = terms.Any(t => p.Title.Contains(t, StringComparison.CurrentCultureIgnoreCase));
            var first = terms.Select(t => body.IndexOf(t, StringComparison.CurrentCultureIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
            matches.Add((p.Id, Snip(body, first < 0 ? null : first), titleMatch, titleMatch ? 2 : 1));
        }

        var ids = matches.Select(m => m.Id).ToList();
        var refs = await TimelineService.RefsAsync(db, db.Pages.Where(p => ids.Contains(p.Id)));
        return matches
            .Join(refs, m => m.Id, r => r.Id, (m, r) => new SearchHit(r, m.Snippet, terms, m.TitleMatch))
            .OrderByDescending(h => h.TitleMatch).ThenByDescending(h => h.Page.Date)
            .Take(take)
            .ToList();
    }

    private static string Snip(string body, int? at)
    {
        if (string.IsNullOrEmpty(body)) return "";
        var start = Math.Max(0, (at ?? 0) - 60);
        var len = Math.Min(220, body.Length - start);
        var s = body.Substring(start, len);
        return (start > 0 ? "…" : "") + s + (start + len < body.Length ? "…" : "");
    }
}
