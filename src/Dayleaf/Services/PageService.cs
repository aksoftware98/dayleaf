using Dayleaf.Localization;
using System.Security.Cryptography;
using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services;

public interface IPageService
{
    Task<Page?> GetAsync(Guid pageId);
    Task<Page?> GetByDateAsync(Guid spaceId, DateOnly date);
    Task<Page?> GetByShareTokenAsync(string token);
    Task<Page> GetOrCreatePageAsync(Guid spaceId, DateOnly date, string? title = null);
    Task<Section> AddSectionAsync(Guid pageId, string? label = null);
    Task SubmitSectionAsync(Guid sectionId);
    Task RenameSectionAsync(Guid sectionId, string label);
    Task DeleteSectionAsync(Guid sectionId);
    Task<PageElement> AddElementAsync(Guid pageId, ElementKind kind, ElementData data, Guid? mediaId = null, Guid? linkTarget = null,
        (int Sheet, double X, double Y)? at = null, (double W, double H)? size = null, double? rotation = null, Guid? sectionId = null);
    Task<PageElement> UpdateElementDataAsync(Guid elementId, ElementData data);
    Task<PageElement> TransformElementAsync(Guid elementId, int sheet, double x, double y, double w, double h, double rot);
    Task<PageElement> SetElementMediaAsync(Guid elementId, Guid mediaId);
    Task<PageElement> SetLinkTargetAsync(Guid elementId, Guid? targetPageId);
    Task<PageElement> BringToFrontAsync(Guid elementId);
    Task<PageElement> SendToBackAsync(Guid elementId);
    Task<PageElement> DuplicateElementAsync(Guid elementId);
    Task DeleteElementAsync(Guid elementId);
    Task<Page> UpdatePageMetaAsync(Guid pageId, string title, string theme, List<string> tags, DateOnly? newDate, string? language = null);
    Task<int> AddSheetAsync(Guid pageId);
    Task<int> RemoveEmptyLastSheetAsync(Guid pageId);
    Task<string> EnableShareAsync(Guid pageId);
    Task DisableShareAsync(Guid pageId);
    Task DeletePageAsync(Guid pageId);
    Task<SheetModel?> GetSheetModelAsync(Guid pageId);
    Task<int> GetPageNumberAsync(Guid pageId);
}

public sealed class PageService(
    IDbContextFactory<DayleafDbContext> dbf,
    TimeProvider clock,
    SectionLockPolicy policy,
    LayoutService layout) : IPageService
{
    private DateTimeOffset Now => clock.GetUtcNow();

    // ───────────────────────── reads ─────────────────────────

    public async Task<Page?> GetAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Pages.AsNoTracking()
            .Include(p => p.Space).Include(p => p.Sections).Include(p => p.Elements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == pageId);
    }

    public async Task<Page?> GetByDateAsync(Guid spaceId, DateOnly date)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Pages.AsNoTracking()
            .Include(p => p.Space).Include(p => p.Sections).Include(p => p.Elements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.SpaceId == spaceId && p.Date == date);
    }

    public async Task<Page?> GetByShareTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 22) return null;
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.ShareToken == token);
    }

    public async Task<int> GetPageNumberAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pageId);
        if (page is null) return 0;
        return await db.Pages.CountAsync(p => p.SpaceId == page.SpaceId && p.Date <= page.Date);
    }

    public async Task<SheetModel?> GetSheetModelAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.AsNoTracking()
            .Include(p => p.Space).Include(p => p.Sections).Include(p => p.Elements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == pageId);
        if (page is null) return null;
        return await BuildSheetModelAsync(db, page, policy, Now);
    }

    internal static async Task<SheetModel> BuildSheetModelAsync(DayleafDbContext db, Page page, SectionLockPolicy policy, DateTimeOffset now)
    {
        var number = await db.Pages.CountAsync(p => p.SpaceId == page.SpaceId && p.Date <= page.Date);
        var sections = page.Sections.ToDictionary(s => s.Id);
        var elements = page.Elements
            .OrderBy(e => e.Z).ThenBy(e => e.CreatedAt)
            .Select(e => new SheetElement
            {
                Element = e,
                Data = e.ReadData(),
                Editable = sections.TryGetValue(e.SectionId, out var s) && policy.IsEditable(s, now),
                SectionLabel = sections.TryGetValue(e.SectionId, out var s2) ? s2.Label : "",
            })
            .ToList();

        var targets = page.Elements.Where(e => e.Kind == ElementKind.PageLink && e.LinkTargetPageId != null)
            .Select(e => e.LinkTargetPageId!.Value).Distinct().ToList();
        var cards = new Dictionary<Guid, LinkCard?>();
        if (targets.Count > 0)
        {
            var found = await db.Pages.AsNoTracking().Include(p => p.Space)
                .Where(p => targets.Contains(p.Id)).ToListAsync();
            foreach (var id in targets)
            {
                var t = found.FirstOrDefault(p => p.Id == id);
                if (t is null) { cards[id] = null; continue; }
                var photo = await db.Elements.AsNoTracking()
                    .Where(e => e.PageId == t.Id && e.Kind == ElementKind.Photo && e.MediaId != null)
                    .OrderBy(e => e.Sheet).ThenBy(e => e.Y).Select(e => e.MediaId).FirstOrDefaultAsync();
                var n = await db.Pages.CountAsync(p => p.SpaceId == t.SpaceId && p.Date <= t.Date);
                cards[id] = new LinkCard(t.Id, $"/s/{t.Space!.Slug}/{t.Date:yyyy-MM-dd}", t.Space.Name, t.Space.Emoji, t.Date, t.Title, t.Theme, photo, n);
            }
        }

        return new SheetModel
        {
            PageId = page.Id,
            SpaceId = page.SpaceId,
            SpaceName = page.Space!.Name,
            SpaceSlug = page.Space.Slug,
            SpaceEmoji = page.Space.Emoji,
            Date = page.Date,
            Title = page.Title,
            Theme = page.Theme,
            Language = Lang.Normalize(page.Language),
            Tags = [.. page.Tags],
            SheetCount = Math.Max(1, page.SheetCount),
            PageNumber = number,
            Elements = elements,
            LinkCards = cards,
        };
    }

    // ───────────────────────── pages & sections ─────────────────────────

    public async Task<Page> GetOrCreatePageAsync(Guid spaceId, DateOnly date, string? title = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var existing = await db.Pages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.SpaceId == spaceId && p.Date == date);
        if (existing is not null) return existing;
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.Id == spaceId) ?? throw new DayleafRuleException(Loc.T("That space no longer exists."));
        var now = Now;
        var page = new Page
        {
            SpaceId = spaceId,
            Date = date,
            Title = CleanTitle(title),
            Theme = space.DefaultTheme,
            Language = Lang.Normalize(space.DefaultLanguage),
            CreatedAt = now,
        };
        page.Sections.Add(new Section { PageId = page.Id, Seq = 1, Label = DefaultSectionLabel(space.DefaultLanguage), CreatedAt = now });
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return page;
    }

    public async Task<Section> AddSectionAsync(Guid pageId, string? label = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.Id == pageId)
                   ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        var now = Now;
        if (page.Sections.Any(s => policy.IsDraft(s, now)))
            throw new DayleafRuleException(Loc.T("This page already has an open draft section. Submit it before starting a new one."));
        var section = new Section
        {
            PageId = pageId,
            Seq = page.Sections.Count == 0 ? 1 : page.Sections.Max(s => s.Seq) + 1,
            Label = string.IsNullOrWhiteSpace(label) ? DefaultSectionLabel(page.Language) : label.Trim()[..Math.Min(label.Trim().Length, 60)],
            CreatedAt = now,
        };
        db.Sections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    public async Task SubmitSectionAsync(Guid sectionId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var section = await db.Sections.FirstOrDefaultAsync(s => s.Id == sectionId) ?? throw new DayleafRuleException(Loc.T("That section no longer exists."));
        var now = Now;
        policy.EnsureEditable(section, now);
        if (!policy.IsDraft(section, now)) return; // already submitted
        section.SubmittedAt = now;
        await db.SaveChangesAsync();
    }

    public async Task RenameSectionAsync(Guid sectionId, string label)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var section = await db.Sections.FirstOrDefaultAsync(s => s.Id == sectionId) ?? throw new DayleafRuleException(Loc.T("That section no longer exists."));
        policy.EnsureEditable(section, Now);
        label = (label ?? "").Trim();
        if (label.Length == 0) throw new DayleafRuleException(Loc.T("A section needs a name."));
        section.Label = label[..Math.Min(label.Length, 60)];
        await db.SaveChangesAsync();
    }

    public async Task DeleteSectionAsync(Guid sectionId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var section = await db.Sections.FirstOrDefaultAsync(s => s.Id == sectionId) ?? throw new DayleafRuleException(Loc.T("That section no longer exists."));
        policy.EnsureEditable(section, Now);
        var count = await db.Sections.CountAsync(s => s.PageId == section.PageId);
        if (count <= 1) throw new DayleafRuleException(Loc.T("A page needs at least one section. Delete the page instead."));
        await db.Elements.Where(e => e.SectionId == sectionId).ExecuteDeleteAsync();
        db.Sections.Remove(section);
        await db.SaveChangesAsync();
    }

    // ───────────────────────── elements ─────────────────────────

    public async Task<PageElement> AddElementAsync(Guid pageId, ElementKind kind, ElementData data, Guid? mediaId = null, Guid? linkTarget = null,
        (int Sheet, double X, double Y)? at = null, (double W, double H)? size = null, double? rotation = null, Guid? sectionId = null)
    {
        if (data.Kind != kind) throw new ArgumentException("Data kind mismatch.", nameof(data));
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.Include(p => p.Sections).Include(p => p.Elements)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(p => p.Id == pageId)
                   ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        var now = Now;

        Section target;
        if (sectionId is { } sid)
        {
            target = page.Sections.FirstOrDefault(s => s.Id == sid) ?? throw new DayleafRuleException(Loc.T("That section isn't on this page."));
            policy.EnsureEditable(target, now);
        }
        else
        {
            target = page.Sections.FirstOrDefault(s => policy.IsDraft(s, now)) ?? NewSection(db, page, now);
        }

        if (kind == ElementKind.Photo && mediaId is null) throw new DayleafRuleException(Loc.T("A photo needs an image."));
        if (kind == ElementKind.Photo && !await db.Media.AnyAsync(m => m.Id == mediaId)) throw new DayleafRuleException(Loc.T("That image wasn't found."));

        var (w, h) = size ?? LayoutService.DefaultSize(kind);
        (w, h) = LayoutService.ClampSize(w, h);
        var place = layout.Place(page.Elements, page.SheetCount, w, h, at, rtl: Lang.IsRtl(page.Language));
        if (place.AddsSheet || place.Sheet >= page.SheetCount) page.SheetCount = place.Sheet + 1;

        Normalize(data);
        var el = new PageElement
        {
            PageId = pageId,
            SectionId = target.Id,
            Kind = kind,
            Sheet = place.Sheet,
            X = place.X,
            Y = place.Y,
            W = place.W,
            H = place.H,
            Rotation = Math.Clamp(rotation ?? DefaultRotation(kind), -180, 180),
            Z = page.Elements.Count == 0 ? 1 : page.Elements.Max(e => e.Z) + 1,
            DataJson = ElementDataJson.Serialize(data),
            PlainText = PlainTextOf(data),
            MediaId = mediaId,
            LinkTargetPageId = linkTarget,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Elements.Add(el);
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> UpdateElementDataAsync(Guid elementId, ElementData data)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        if (data.Kind != el.Kind) throw new ArgumentException("Data kind mismatch.", nameof(data));
        Normalize(data);
        el.DataJson = ElementDataJson.Serialize(data);
        el.PlainText = PlainTextOf(data);
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> TransformElementAsync(Guid elementId, int sheet, double x, double y, double w, double h, double rot)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        var page = await db.Pages.FirstAsync(p => p.Id == el.PageId);
        if (double.IsNaN(x + y + w + h + rot) || double.IsInfinity(x + y + w + h + rot))
            throw new DayleafRuleException(Loc.T("Invalid position."));
        el.Sheet = Math.Clamp(sheet, 0, Math.Max(0, page.SheetCount - 1));
        (el.W, el.H) = LayoutService.ClampSize(w, h);
        (el.X, el.Y) = LayoutService.Clamp(x, y, el.W, el.H);
        el.Rotation = Math.Round(Math.Clamp(rot, -180, 180), 1);
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> SetElementMediaAsync(Guid elementId, Guid mediaId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        if (el.Kind != ElementKind.Photo) throw new DayleafRuleException(Loc.T("Only photos have an image."));
        if (!await db.Media.AnyAsync(m => m.Id == mediaId)) throw new DayleafRuleException(Loc.T("That image wasn't found."));
        el.MediaId = mediaId;
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> SetLinkTargetAsync(Guid elementId, Guid? targetPageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        if (el.Kind != ElementKind.PageLink) throw new DayleafRuleException(Loc.T("Only page links point to pages."));
        if (targetPageId == el.PageId) throw new DayleafRuleException(Loc.T("A page can't link to itself."));
        el.LinkTargetPageId = targetPageId;
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> BringToFrontAsync(Guid elementId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        var max = await db.Elements.Where(e => e.PageId == el.PageId).MaxAsync(e => e.Z);
        if (el.Z != max || await db.Elements.CountAsync(e => e.PageId == el.PageId && e.Z == max) > 1) el.Z = max + 1;
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> SendToBackAsync(Guid elementId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        var min = await db.Elements.Where(e => e.PageId == el.PageId).MinAsync(e => e.Z);
        el.Z = min - 1;
        el.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return el;
    }

    public async Task<PageElement> DuplicateElementAsync(Guid elementId)
    {
        PageElement src;
        await using (var db = await dbf.CreateDbContextAsync())
        {
            src = await db.Elements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == elementId) ?? throw new DayleafRuleException(Loc.T("That element no longer exists."));
        }
        return await AddElementAsync(src.PageId, src.Kind, src.ReadData(), src.MediaId, src.LinkTargetPageId,
            (src.Sheet, src.X + 24, src.Y + 24), (src.W, src.H), src.Rotation);
    }

    public async Task DeleteElementAsync(Guid elementId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var el = await LoadEditableAsync(db, elementId);
        db.Elements.Remove(el);
        await db.SaveChangesAsync();
    }

    // ───────────────────────── page meta, sheets, sharing ─────────────────────────

    public async Task<Page> UpdatePageMetaAsync(Guid pageId, string title, string theme, List<string> tags, DateOnly? newDate, string? language = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.Id == pageId)
                   ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        policy.EnsureMetaEditable(page.Sections, Now);

        if (newDate is { } d && d != page.Date)
        {
            var clash = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.SpaceId == page.SpaceId && p.Date == d);
            if (clash is not null)
                throw new DayleafRuleException(string.IsNullOrWhiteSpace(clash.Title)
                    ? Loc.T("There's already a page on {0}.", Dates.Format(d, DateStyle.Short))
                    : Loc.T("There's already a page on {0}: “{1}”.", Dates.Format(d, DateStyle.Short), clash.Title));
            page.Date = d;
        }
        page.Title = CleanTitle(title);
        page.Theme = Themes.Get(theme).Id;
        if (language is not null) page.Language = Lang.Normalize(language);
        page.Tags = (tags ?? [])
            .Select(t => (t ?? "").Trim())
            .Where(t => t.Length > 0)
            .Select(t => t.Length > 40 ? t[..40] : t)
            .Distinct()
            .Take(8)
            .ToList();
        await db.SaveChangesAsync();
        return page;
    }

    public async Task<int> AddSheetAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId) ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        if (page.SheetCount >= 20) throw new DayleafRuleException(Loc.T("A page can have at most 20 sheets."));
        page.SheetCount++;
        await db.SaveChangesAsync();
        return page.SheetCount;
    }

    public async Task<int> RemoveEmptyLastSheetAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId) ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        if (page.SheetCount <= 1) return page.SheetCount;
        var last = page.SheetCount - 1;
        if (await db.Elements.AnyAsync(e => e.PageId == pageId && e.Sheet == last))
            throw new DayleafRuleException(Loc.T("The last sheet isn't empty."));
        page.SheetCount--;
        await db.SaveChangesAsync();
        return page.SheetCount;
    }

    public async Task<string> EnableShareAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId) ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        page.ShareToken ??= NewShareToken();
        await db.SaveChangesAsync();
        return page.ShareToken;
    }

    public async Task DisableShareAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId) ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        page.ShareToken = null;
        await db.SaveChangesAsync();
    }

    public async Task DeletePageAsync(Guid pageId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var page = await db.Pages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.Id == pageId)
                   ?? throw new DayleafRuleException(Loc.T("That page no longer exists."));
        var now = Now;
        if (page.Sections.Any(s => policy.IsLocked(s, now)))
            throw new SectionLockedException(Loc.T("This page has locked sections, so it can't be deleted."));
        db.Pages.Remove(page);
        await db.SaveChangesAsync();
    }

    // ───────────────────────── helpers ─────────────────────────

    private async Task<PageElement> LoadEditableAsync(DayleafDbContext db, Guid elementId)
    {
        var el = await db.Elements.FirstOrDefaultAsync(e => e.Id == elementId) ?? throw new DayleafRuleException(Loc.T("That element no longer exists."));
        var section = await db.Sections.FirstAsync(s => s.Id == el.SectionId);
        policy.EnsureEditable(section, Now);
        return el;
    }

    private Section NewSection(DayleafDbContext db, Page page, DateTimeOffset now)
    {
        var s = new Section
        {
            PageId = page.Id,
            Seq = page.Sections.Count == 0 ? 1 : page.Sections.Max(x => x.Seq) + 1,
            Label = DefaultSectionLabel(page.Language),
            CreatedAt = now,
        };
        page.Sections.Add(s);
        db.Sections.Add(s);
        return s;
    }

    private string DefaultSectionLabel(string? language)
    {
        var h = clock.GetLocalNow().Hour;
        var en = h switch { < 5 => "Night", < 12 => "Morning", < 17 => "Afternoon", < 21 => "Evening", _ => "Night" };
        return Loc.TFor(language ?? Lang.En, en);
    }

    private static double DefaultRotation(ElementKind kind) => kind switch
    {
        ElementKind.Photo => Math.Round(Random.Shared.NextDouble() * 6 - 3, 1),
        ElementKind.Note => 1.5,
        ElementKind.Emoji => Math.Round(Random.Shared.NextDouble() * 24 - 12),
        ElementKind.Tape => Math.Round(Random.Shared.NextDouble() * 20 - 10),
        ElementKind.Label => -2,
        _ => 0,
    };

    private static string CleanTitle(string? title)
    {
        var t = (title ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        return t.Length > 300 ? t[..300] : t;
    }

    public static string NewShareToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Sanitize and bound every user-controlled field.</summary>
    public static void Normalize(ElementData data)
    {
        static string Cap(string? s, int max) => s is null ? "" : s.Length > max ? s[..max] : s;
        switch (data)
        {
            case TextData t:
                t.Html = Cap(TextSanitizer.Sanitize(t.Html), 20000);
                t.Size = OneOf(t.Size, "m", "s", "m", "l", "xl");
                t.Font = OneOf(t.Font, "body", Palette.TextFontIds);
                t.Color = OneOf(t.Color, "ink", Palette.Inks);
                t.Align = OneOf(t.Align, "start", "start", "center", "end", "left", "right");
                t.Highlight = t.Highlight is "marker" or "box" ? t.Highlight : null;
                break;
            case HeadingData h:
                h.Text = Cap(h.Text, 300);
                h.Size = OneOf(h.Size, "l", "m", "l", "xl");
                h.Color = OneOf(h.Color, "ink", Palette.Inks);
                h.Align = OneOf(h.Align, "start", "start", "center", "end", "left", "right");
                break;
            case PhotoData p:
                p.Style = OneOf(p.Style, "sticker", "plain", "sticker", "polaroid", "taped", "round");
                p.Caption = string.IsNullOrWhiteSpace(p.Caption) ? null : Cap(p.Caption.Trim(), 200);
                p.Alt = string.IsNullOrWhiteSpace(p.Alt) ? null : Cap(p.Alt.Trim(), 300);
                p.FocusX = Math.Clamp(p.FocusX, 0, 100);
                p.FocusY = Math.Clamp(p.FocusY, 0, 100);
                p.Zoom = Math.Clamp(p.Zoom, 1, 4);
                p.Filter = OneOf(p.Filter, "none", Palette.PhotoFilters);
                break;
            case EmojiData e:
                e.Char = string.IsNullOrWhiteSpace(e.Char) ? "🌱" : Cap(e.Char.Trim(), 16);
                break;
            case NoteData n:
                n.Html = Cap(TextSanitizer.Sanitize(n.Html), 10000);
                n.Title = string.IsNullOrWhiteSpace(n.Title) ? null : Cap(n.Title.Trim(), 120);
                n.Color = OneOf(n.Color, "yellow", Palette.NoteColors);
                break;
            case ChecklistData c:
                c.Title = string.IsNullOrWhiteSpace(c.Title) ? null : Cap(c.Title.Trim(), 120);
                c.Items = (c.Items ?? []).Take(40).Select(i => new ChecklistItem { Text = Cap(i.Text?.Trim(), 200), Done = i.Done }).ToList();
                c.Color = OneOf(c.Color, "yellow", Palette.ChecklistColors);
                break;
            case PageLinkData l:
                l.Note = string.IsNullOrWhiteSpace(l.Note) ? null : Cap(l.Note.Trim(), 200);
                break;
            case QuoteData q:
                q.Label = Cap(q.Label?.Trim(), 60);
                q.Text = Cap(q.Text?.Trim(), 500);
                break;
            case LabelData lb:
                lb.Text = Cap(lb.Text?.Trim(), 200);
                lb.Color = OneOf(lb.Color, "accent", Palette.Inks);
                lb.Size = OneOf(lb.Size, "m", "s", "m", "l");
                break;
            case DividerData d:
                d.Style = OneOf(d.Style, "line", Palette.DividerStyles);
                break;
            case TapeData tp:
                tp.Color = OneOf(tp.Color, "theme", Palette.TapeColors);
                tp.Pattern = OneOf(tp.Pattern, "plain", Palette.TapePatterns);
                break;
            case ShapeData sh:
                sh.Shape = OneOf(sh.Shape, "blob", Palette.Shapes);
                sh.Color = OneOf(sh.Color, "accent2", Palette.ShapeColors);
                sh.Opacity = Math.Clamp(sh.Opacity, 0.05, 1);
                break;
        }
    }

    private static string OneOf(string? value, string fallback, params string[] allowed) =>
        value is not null && allowed.Contains(value) ? value : fallback;

    public static string? PlainTextOf(ElementData data)
    {
        var raw = data.SearchText();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var plain = TextSanitizer.ToPlainText(raw);
        return plain.Length == 0 ? null : plain;
    }
}
