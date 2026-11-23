using Dayleaf.Domain;
using Dayleaf.Services;

namespace Dayleaf.Tests;

public class TimelineServiceTests : IDisposable
{
    private readonly TestHost _h = new();
    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Page_numbers_follow_dates_and_backfill_renumbers()
    {
        var space = await _h.Spaces.CreateAsync("Farm", "🌳", "forest", null, null);
        var a = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 1, 10));
        var b = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 3, 1));
        Assert.Equal(1, await _h.Timeline.GetPageNumberAsync(a.Id));
        Assert.Equal(2, await _h.Timeline.GetPageNumberAsync(b.Id));

        var older = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2024, 8, 28)); // backfill
        Assert.Equal(1, await _h.Timeline.GetPageNumberAsync(older.Id));
        Assert.Equal(2, await _h.Timeline.GetPageNumberAsync(a.Id));
        Assert.Equal(3, await _h.Timeline.GetPageNumberAsync(b.Id));

        var refs = await _h.Timeline.ListPagesAsync(space.Id);
        Assert.Equal([3, 2, 1], refs.Select(r => r.Number));
    }

    [Fact]
    public async Task Prev_next_move_one_page_by_date()
    {
        var space = await _h.Spaces.CreateAsync("Farm", "🌳", "forest", null, null);
        var d1 = new DateOnly(2025, 1, 1);
        var d2 = new DateOnly(2025, 2, 1);
        var d3 = new DateOnly(2025, 5, 1);
        foreach (var d in new[] { d3, d1, d2 }) await _h.Pages.GetOrCreatePageAsync(space.Id, d);

        var pn = await _h.Timeline.GetPrevNextAsync(space.Id, d2);
        Assert.Equal(d1, pn.Prev!.Date);
        Assert.Equal(d3, pn.Next!.Date);
        var first = await _h.Timeline.GetPrevNextAsync(space.Id, d1);
        Assert.Null(first.Prev);
        // An empty date between pages still finds neighbours.
        var gap = await _h.Timeline.GetPrevNextAsync(space.Id, new DateOnly(2025, 3, 15));
        Assert.Equal(d2, gap.Prev!.Date);
        Assert.Equal(d3, gap.Next!.Date);
    }

    [Fact]
    public async Task Month_counts_years_and_on_this_day()
    {
        var space = await _h.Spaces.CreateAsync("Farm", "🌳", "forest", null, null);
        await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2025, 11, 23));
        await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 11, 1));
        await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 11, 2));
        await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 11, 23));

        var counts = await _h.Timeline.GetMonthCountsAsync(space.Id, 2026);
        Assert.Equal(3, counts[10]);
        Assert.Equal([2025, 2026], await _h.Timeline.GetYearsAsync(space.Id));
        var otd = await _h.Timeline.OnThisDayAsync(new DateOnly(2026, 11, 23));
        Assert.Single(otd);
        Assert.Equal(2025, otd[0].Date.Year);
    }

    [Fact]
    public async Task Backlinks_find_pages_linking_here()
    {
        var space = await _h.Spaces.CreateAsync("Farm", "🌳", "forest", null, null);
        var target = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2025, 1, 1));
        var source = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2025, 2, 1));
        await _h.Pages.AddElementAsync(source.Id, ElementKind.PageLink, new PageLinkData(), linkTarget: target.Id);
        var back = await _h.Timeline.GetBacklinksAsync(target.Id);
        Assert.Equal(source.Id, Assert.Single(back).Id);
    }

    [Fact]
    public async Task Link_to_deleted_page_renders_as_missing()
    {
        var space = await _h.Spaces.CreateAsync("Farm", "🌳", "forest", null, null);
        var target = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2025, 1, 1));
        var source = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2025, 2, 1));
        await _h.Pages.AddElementAsync(source.Id, ElementKind.PageLink, new PageLinkData(), linkTarget: target.Id);
        await _h.Pages.DeletePageAsync(target.Id);
        var model = await _h.Pages.GetSheetModelAsync(source.Id);
        Assert.Null(model!.LinkCards[target.Id]);
    }
}

public class SearchAndLayoutTests : IDisposable
{
    private readonly TestHost _h = new();
    public void Dispose() => _h.Dispose();

    [Theory]
    [InlineData("2025-06", 2025, 6, null)]
    [InlineData("2025", 2025, null, null)]
    [InlineData("june 2025", 2025, 6, null)]
    [InlineData("Jun 11, 2025", 2025, 6, 11)]
    [InlineData("11 june 2025", 2025, 6, 11)]
    [InlineData("2025-06-11", 2025, 6, 11)]
    public void Date_queries_parse(string q, int y, int? m, int? d)
    {
        var dq = DateQuery.TryParse(q);
        Assert.NotNull(dq);
        Assert.Equal(new DateQuery(y, m, d), dq);
    }

    [Theory]
    [InlineData("tractor")]
    [InlineData("hello world")]
    public void Words_are_not_dates(string q) => Assert.Null(DateQuery.TryParse(q));

    [Fact]
    public async Task Search_matches_text_and_dates()
    {
        var (space, page) = await _h.NewPageAsync(date: new DateOnly(2025, 6, 11));
        await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "<p>Our red <b>tractor</b> works hard.</p>" });
        var hits = await _h.Search.SearchAsync("TRACTOR");
        Assert.Single(hits);
        Assert.Contains("tractor", hits[0].Snippet);
        Assert.Single(await _h.Search.SearchAsync("june 2025"));
        Assert.Empty(await _h.Search.SearchAsync("2024-06"));
        _ = space;
    }

    [Fact]
    public void Layout_places_under_lowest_element_then_overflows()
    {
        var layout = new LayoutService();
        var existing = new List<PageElement> { new() { Sheet = 0, Y = 400, H = 200 } };
        var p = layout.Place(existing, 1, 440, 120);
        Assert.Equal((0, 56d, 616d), (p.Sheet, p.X, p.Y));

        existing.Add(new PageElement { Sheet = 0, Y = 900, H = 100 });
        var overflow = layout.Place(existing, 1, 440, 120);
        Assert.True(overflow.AddsSheet);
        Assert.Equal((1, 100d), (overflow.Sheet, overflow.Y));

        var dropped = layout.Place(existing, 1, 100, 100, (0, 5000, -900));
        Assert.Equal(794 - 24, dropped.X);
        Assert.Equal(24 - 100, dropped.Y);
    }

    [Fact]
    public void Photo_size_uses_aspect_ratio()
    {
        Assert.Equal((460d, 307d), LayoutService.PhotoSize(1536, 1024));
        Assert.Equal((220d, 220d), LayoutService.PhotoSize(1536, 1024, "round"));
    }

    [Fact]
    public void Image_sniffer_rejects_non_images()
    {
        Assert.Null(Dayleaf.Services.ImageSniffer.Detect("<html>"u8));
        Assert.NotNull(Dayleaf.Services.ImageSniffer.Detect(TestHost.TinyPng()));
    }
}
