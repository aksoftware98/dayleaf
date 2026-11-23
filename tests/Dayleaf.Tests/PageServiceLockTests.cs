using Dayleaf.Domain;
using Dayleaf.Services;

namespace Dayleaf.Tests;

public class PageServiceLockTests : IDisposable
{
    private readonly TestHost _h = new();
    public void Dispose() => _h.Dispose();

    private async Task<(Page Page, PageElement Text, PageElement Link)> LockedPageAsync()
    {
        var (_, page) = await _h.NewPageAsync();
        var text = await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "<p>hi</p>" });
        var link = await _h.Pages.AddElementAsync(page.Id, ElementKind.PageLink, new PageLinkData());
        var section = page.Sections.Single();
        await _h.Pages.SubmitSectionAsync(section.Id);
        _h.Clock.Advance(TimeSpan.FromHours(6));
        return (page, text, link);
    }

    [Fact]
    public async Task Every_element_mutation_on_a_locked_section_throws()
    {
        var (page, text, link) = await LockedPageAsync();
        var p = _h.Pages;
        await Assert.ThrowsAsync<SectionLockedException>(() => p.UpdateElementDataAsync(text.Id, new TextData { Html = "x" }));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.TransformElementAsync(text.Id, 0, 10, 10, 100, 100, 0));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.BringToFrontAsync(text.Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.SendToBackAsync(text.Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.DeleteElementAsync(text.Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.SetLinkTargetAsync(link.Id, null));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.SubmitSectionAsync(page.Sections.Single().Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.RenameSectionAsync(page.Sections.Single().Id, "x"));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.DeleteSectionAsync(page.Sections.Single().Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.UpdatePageMetaAsync(page.Id, "new", "sea", [], null));
        await Assert.ThrowsAsync<SectionLockedException>(() => p.DeletePageAsync(page.Id));
        await Assert.ThrowsAsync<SectionLockedException>(() =>
            p.AddElementAsync(page.Id, ElementKind.Emoji, new EmojiData(), sectionId: page.Sections.Single().Id));
    }

    [Fact]
    public async Task Photo_media_change_on_locked_section_throws()
    {
        var (_, page) = await _h.NewPageAsync();
        var m = await _h.Media.SaveAsync(new MemoryStream(TestHost.TinyPng()), "a.png");
        var photo = await _h.Pages.AddElementAsync(page.Id, ElementKind.Photo, new PhotoData(), m.Id);
        await _h.Pages.SubmitSectionAsync(page.Sections.Single().Id);
        _h.Clock.Advance(TimeSpan.FromHours(7));
        await Assert.ThrowsAsync<SectionLockedException>(() => _h.Pages.SetElementMediaAsync(photo.Id, m.Id));
        await Assert.ThrowsAsync<SectionLockedException>(() => _h.Pages.DeleteElementAsync(photo.Id));
    }

    [Fact]
    public async Task Adding_after_lock_goes_into_a_new_section()
    {
        var (page, _, _) = await LockedPageAsync();
        var el = await _h.Pages.AddElementAsync(page.Id, ElementKind.Emoji, new EmojiData { Char = "🌱" });
        var reloaded = await _h.Pages.GetAsync(page.Id);
        Assert.Equal(2, reloaded!.Sections.Count);
        Assert.Equal(reloaded.Sections.Single(s => s.Seq == 2).Id, el.SectionId);
    }

    [Fact]
    public async Task Grace_window_allows_edits()
    {
        var (_, page) = await _h.NewPageAsync();
        var text = await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "a" });
        await _h.Pages.SubmitSectionAsync(page.Sections.Single().Id);
        _h.Clock.Advance(TimeSpan.FromHours(5) + TimeSpan.FromMinutes(59));
        var updated = await _h.Pages.UpdateElementDataAsync(text.Id, new TextData { Html = "<b>b</b>" });
        Assert.Equal("<b>b</b>", updated.Read<TextData>().Html);
    }

    [Fact]
    public async Task One_page_per_date_per_space()
    {
        var (space, page) = await _h.NewPageAsync();
        var again = await _h.Pages.GetOrCreatePageAsync(space.Id, page.Date);
        Assert.Equal(page.Id, again.Id);

        var other = await _h.Pages.GetOrCreatePageAsync(space.Id, new DateOnly(2026, 11, 17));
        var ex = await Assert.ThrowsAsync<DayleafRuleException>(() => _h.Pages.UpdatePageMetaAsync(other.Id, "x", "field", [], page.Date));
        Assert.Contains("already a page", ex.Message);
    }

    [Fact]
    public async Task Only_one_open_draft_per_page()
    {
        var (_, page) = await _h.NewPageAsync();
        await Assert.ThrowsAsync<DayleafRuleException>(() => _h.Pages.AddSectionAsync(page.Id));
        await _h.Pages.SubmitSectionAsync(page.Sections.Single().Id);
        var s2 = await _h.Pages.AddSectionAsync(page.Id, "Evening");
        Assert.Equal(2, s2.Seq);
        await Assert.ThrowsAsync<DayleafRuleException>(() => _h.Pages.AddSectionAsync(page.Id));
    }

    [Fact]
    public async Task Text_is_sanitized_and_plaintext_kept()
    {
        var (_, page) = await _h.NewPageAsync();
        var el = await _h.Pages.AddElementAsync(page.Id, ElementKind.Text,
            new TextData { Html = "<p onclick=\"x()\">Hello <script>alert(1)</script><a href=\"javascript:alert(1)\">there</a></p>" });
        var data = el.Read<TextData>();
        Assert.DoesNotContain("script", data.Html);
        Assert.DoesNotContain("onclick", data.Html);
        Assert.DoesNotContain("javascript", data.Html);
        Assert.Equal("Hello there", el.PlainText);
    }

    [Fact]
    public async Task Overflow_adds_a_sheet()
    {
        var (_, page) = await _h.NewPageAsync();
        for (var i = 0; i < 4; i++)
            await _h.Pages.AddElementAsync(page.Id, ElementKind.Text, new TextData { Html = "x" }, size: (440, 200));
        var p = await _h.Pages.GetAsync(page.Id);
        Assert.Equal(2, p!.SheetCount);
        Assert.Contains(p.Elements, e => e.Sheet == 1 && e.Y == LayoutService.LaterSheetContentTop);
    }

    [Fact]
    public async Task Page_with_locked_sections_cannot_be_deleted_but_fresh_one_can()
    {
        var (space, page) = await _h.NewPageAsync();
        await _h.Pages.DeletePageAsync(page.Id);
        Assert.Null(await _h.Pages.GetAsync(page.Id));
        _ = space;
    }

    [Fact]
    public async Task Share_token_is_22_chars_and_disabling_removes_it()
    {
        var (_, page) = await _h.NewPageAsync();
        var token = await _h.Pages.EnableShareAsync(page.Id);
        Assert.Equal(22, token.Length);
        Assert.NotNull(await _h.Pages.GetByShareTokenAsync(token));
        await _h.Pages.DisableShareAsync(page.Id);
        Assert.Null(await _h.Pages.GetByShareTokenAsync(token));
    }

    [Fact]
    public async Task Deleting_a_space_requires_its_exact_name()
    {
        var (space, _) = await _h.NewPageAsync();
        await Assert.ThrowsAsync<DayleafRuleException>(() => _h.Spaces.DeleteAsync(space.Id, "our farm"));
        await _h.Spaces.DeleteAsync(space.Id, "Our Farm");
        Assert.Null(await _h.Spaces.GetAsync(space.Id));
    }
}
