using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services;

public interface ITimelineService
{
    Task<PrevNext> GetPrevNextAsync(Guid spaceId, DateOnly date);
    Task<int[]> GetMonthCountsAsync(Guid spaceId, int year);
    Task<List<int>> GetYearsAsync(Guid spaceId);
    Task<int> GetPageNumberAsync(Guid pageId);
    Task<List<PageRef>> ListPagesAsync(Guid spaceId, int? year = null, int? month = null);
    Task<List<PageRef>> OnThisDayAsync(DateOnly today);
    Task<List<PageRef>> GetLatestAsync(int n);
    Task<List<PageRef>> GetBacklinksAsync(Guid pageId);
    Task<List<PageRef>> GetSharedAsync();
    Task<List<PageRef>> FindPagesAsync(string? query, int take = 30, Guid? excludePageId = null);
}

public sealed class TimelineService(IDbContextFactory<DayleafDbContext> dbf) : ITimelineService
{
    public async Task<PrevNext> GetPrevNextAsync(Guid spaceId, DateOnly date)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var prevId = await db.Pages.Where(p => p.SpaceId == spaceId && p.Date < date).OrderByDescending(p => p.Date).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
        var nextId = await db.Pages.Where(p => p.SpaceId == spaceId && p.Date > date).OrderBy(p => p.Date).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
        var ids = new[] { prevId, nextId }.Where(i => i != null).Select(i => i!.Value).ToList();
        var refs = await RefsAsync(db, db.Pages.Where(p => ids.Contains(p.Id)));
        return new PrevNext(refs.FirstOrDefault(r => r.Id == prevId), refs.FirstOrDefault(r => r.Id == nextId));
    }

    public async Task<int[]> GetMonthCountsAsync(Guid spaceId, int year)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var dates = await db.Pages.Where(p => p.SpaceId == spaceId && p.Date >= from && p.Date <= to).Select(p => p.Date).ToListAsync();
        var counts = new int[12];
        foreach (var d in dates) counts[d.Month - 1]++;
        return counts;
    }

    public async Task<List<int>> GetYearsAsync(Guid spaceId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var dates = await db.Pages.Where(p => p.SpaceId == spaceId).Select(p => p.Date).ToListAsync();
        return dates.Select(d => d.Year).Distinct().OrderBy(y => y).ToList();
    }

    public async Task<int> GetPageNumberAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pageId);
        return page is null ? 0 : await db.Pages.CountAsync(p => p.SpaceId == page.SpaceId && p.Date <= page.Date);
    }

    public async Task<List<PageRef>> ListPagesAsync(Guid spaceId, int? year = null, int? month = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var q = db.Pages.Where(p => p.SpaceId == spaceId);
        if (year is { } y)
        {
            var from = new DateOnly(y, month ?? 1, 1);
            var to = month is { } m ? from.AddMonths(1).AddDays(-1) : new DateOnly(y, 12, 31);
            q = q.Where(p => p.Date >= from && p.Date <= to);
        }
        return (await RefsAsync(db, q)).OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<PageRef>> OnThisDayAsync(DateOnly today)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var all = await db.Pages.Select(p => new { p.Id, p.Date }).ToListAsync();
        var ids = all.Where(p => p.Date.Month == today.Month && p.Date.Day == today.Day && p.Date.Year < today.Year).Select(p => p.Id).ToList();
        return (await RefsAsync(db, db.Pages.Where(p => ids.Contains(p.Id)))).OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<PageRef>> GetLatestAsync(int n)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var ids = await db.Pages.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).Take(n).Select(p => p.Id).ToListAsync();
        return (await RefsAsync(db, db.Pages.Where(p => ids.Contains(p.Id)))).OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<PageRef>> GetBacklinksAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var ids = await db.Elements.Where(e => e.LinkTargetPageId == pageId && e.PageId != pageId).Select(e => e.PageId).Distinct().ToListAsync();
        return (await RefsAsync(db, db.Pages.Where(p => ids.Contains(p.Id)))).OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<PageRef>> GetSharedAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        return (await RefsAsync(db, db.Pages.Where(p => p.ShareToken != null))).OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<PageRef>> FindPagesAsync(string? query, int take = 30, Guid? excludePageId = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var q = db.Pages.Where(p => p.Id != excludePageId);
        var all = await RefsAsync(db, q);
        query = query?.Trim();
        IEnumerable<PageRef> filtered = all;
        if (!string.IsNullOrEmpty(query))
        {
            var dq = DateQuery.TryParse(query);
            filtered = all.Where(r =>
                r.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                r.SpaceName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                r.Date.ToString("MMM d, yyyy").Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                (dq is not null && dq.Matches(r.Date)));
        }
        return filtered.OrderByDescending(r => r.Date).Take(take).ToList();
    }

    /// <summary>Project pages into <see cref="PageRef"/>s with computed page numbers and photo info.</summary>
    internal static async Task<List<PageRef>> RefsAsync(DayleafDbContext db, IQueryable<Page> query)
    {
        var pages = await query.AsNoTracking().Include(p => p.Space).ToListAsync();
        if (pages.Count == 0) return [];
        var ids = pages.Select(p => p.Id).ToList();
        var els = await db.Elements.AsNoTracking()
            .Where(e => ids.Contains(e.PageId))
            .Select(e => new { e.PageId, e.Kind, e.MediaId, e.Sheet, e.Y, e.PlainText })
            .ToListAsync();
        var spaceIds = pages.Select(p => p.SpaceId).Distinct().ToList();
        var allDates = await db.Pages.AsNoTracking().Where(p => spaceIds.Contains(p.SpaceId)).Select(p => new { p.SpaceId, p.Date }).ToListAsync();
        var bySpace = allDates.GroupBy(x => x.SpaceId).ToDictionary(g => g.Key, g => g.Select(x => x.Date).OrderBy(d => d).ToList());

        return pages.Select(p =>
        {
            var mine = els.Where(e => e.PageId == p.Id).OrderBy(e => e.Sheet).ThenBy(e => e.Y).ToList();
            var photos = mine.Where(e => e.Kind == ElementKind.Photo && e.MediaId != null).Select(e => e.MediaId!.Value).ToList();
            var excerpt = mine.Where(e => e.Kind is ElementKind.Text or ElementKind.Note && !string.IsNullOrWhiteSpace(e.PlainText))
                .Select(e => e.PlainText).FirstOrDefault();
            var dates = bySpace[p.SpaceId];
            var number = UpperBound(dates, p.Date);
            return new PageRef(p.Id, p.SpaceId, p.Space!.Slug, p.Space.Name, p.Space.Emoji, p.Space.Color, p.Date, p.Title, p.Theme,
                number, photos.Count, mine.Count(e => e.Kind == ElementKind.Emoji), photos, excerpt, p.ShareToken != null);
        }).ToList();
    }

    private static int UpperBound(List<DateOnly> sorted, DateOnly date)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (sorted[mid] <= date) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
