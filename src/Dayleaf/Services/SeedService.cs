using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services;

/// <summary>Creates the sample notebook from the mockups (Our Farm page, Typing 365 page, a welcome page).</summary>
public sealed class SeedService(
    IDbContextFactory<DayleafDbContext> dbf,
    IMediaService media,
    TimeProvider clock,
    IWebHostEnvironment env)
{
    public async Task<bool> HasSampleAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Spaces.AnyAsync(s => s.Slug == "our-farm");
    }

    public async Task<bool> SeedAsync()
    {
        if (await HasSampleAsync()) return false;

        var farmImg = await LoadImageAsync("serghaya.png");
        var typingImg = await LoadImageAsync("typing365.png");

        await using var db = await dbf.CreateDbContextAsync();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        var farm = NewSpace(db, "Our Farm", "our-farm", "🌳", "marigold", "the family orchard in Serghaya", "field", farmImg, now.AddYears(-3));
        var typing = NewSpace(db, "Typing 365", "typing-365", "⌨️", "lilac", "building a typing app, one release at a time", "pastel", typingImg, now.AddYears(-2));
        var life = NewSpace(db, "Life", "life", "🌿", "sea", "your life, one date at a time", "sea", null, now.AddDays(-1));
        NewSpace(db, "Events", "events", "🎈", "clay", "weddings, trips, firsts", "sunset", null, now.AddDays(-1));

        // ── Our Farm · the hero page (Nov 16, 2026) ───────────────────────────
        var hero = NewPage(db, farm, new DateOnly(2026, 11, 16), "Starting over, from the roots.", "field",
            ["🍂 Late autumn", "🌱 Fresh start", "📍 Serghaya"]);
        var heroSec = Locked(db, hero, "Morning", At(2026, 11, 16, 8, 10));
        var b = new ElementBuilder(db, hero, heroSec);
        b.Emoji("🌱", 628, 238, 80, 12);
        b.Photo(farmImg, "taped", 50, 384, 460, 310, -2, focusY: 62, alt: "The orchard road in autumn, lined with old trees");
        b.Emoji("🍂", 20, 652, 56, -16);
        b.Photo(farmImg, "round", 544, 360, 190, 190, 6, zoom: 3.4, focusX: 50, focusY: 70, alt: "Our red tractor between the trees");
        b.Label("our red tractor, still the hardest worker 🚜", 538, 560, 212, 56, -3);
        b.Checklist("This winter", [("Mark the dead trees", true), ("Clear the first rows", true), ("Rest & feed the soil", false), ("Pick spring saplings", false)],
            540, 630, 206, 176, 1.5, "yellow");
        b.Quote("THE DECISION", "Replace, don't repair.", 56, 728, 440, 72);
        b.Text("<p>Today we finally started what we've talked about for years. The oldest trees — the ones that stopped giving fruit and stood half-dry for seasons — came out, one row at a time.</p><p>It's strange to see the land so open. But that's the plan: clear everything tired, let the soil rest through winter, and plant young trees in spring. A new first page for the farm.</p>",
            56, 808, 440, 220);
        b.Photo(farmImg, "polaroid", 552, 836, 188, 172, 3, zoom: 2.4, focusX: 10, focusY: 50, caption: "the old row, one last look", alt: "The old row of trees");
        b.Emoji("🌳", 708, 808, 50, 10);

        // ── Our Farm · history ────────────────────────────────────────────────
        FarmEntry(db, farm, new(2026, 10, 28), "First rain of the season", "🌧️", "<p>The first real rain of autumn. The dust finally settled and the whole valley smelled like wet earth. The old trees drank it up — maybe for the last time.</p>", "sky");
        var harvest = FarmEntry(db, farm, new(2026, 10, 9), "Last harvest from the old rows", "🍎", "<p>We picked what the old rows could still give. Small apples, a little sour, but ours. Packed eleven crates and gave three away to the neighbours.</p>", "pink", farmImg);
        FarmEntry(db, farm, new(2026, 9, 21), "Deciding: repair or replant?", "🤔", "<p>Long evening talking it through. Repairing means another five years of half-dry trees. Replanting means three hard years with nothing. We'll sleep on it.</p>", "mint");
        FarmEntry(db, farm, new(2026, 8, 30), "Irrigation lines, again", "💧", "<p>Fixed the main line for the third time this summer. Time to stop patching.</p>", "sky");
        FarmEntry(db, farm, new(2026, 7, 14), "Cherry week", "🍒", "<p>Cherries everywhere — on our hands, our shirts, the dog. Best week of the year.</p>", "pink", farmImg);
        FarmEntry(db, farm, new(2026, 6, 2), "Bees are back", "🐝", "<p>Counted four hives' worth of bees on the blossoms. Called the beekeeper to say thanks.</p>", "yellow");
        FarmEntry(db, farm, new(2026, 4, 18), "Blossom day", "🌸", "<p>The whole orchard turned white overnight.</p>", "pink", farmImg);
        FarmEntry(db, farm, new(2026, 3, 3), "Pruning the north rows", "✂️", "<p>Cold hands, sharp shears, good coffee.</p>", "mint");
        FarmEntry(db, farm, new(2025, 11, 23), "A year ago: the long walk", "🍂", "<p>Walked every row with Dad and wrote down which trees were still alive. The list was shorter than we hoped.</p>", "yellow", farmImg);
        FarmEntry(db, farm, new(2025, 9, 12), "Fig season", "🌿", "<p>The figs by the gate never disappoint.</p>", "mint");

        // ── Typing 365 · the pastel page (Jun 11, 2025) ──────────────────────
        var store = NewPage(db, typing, new DateOnly(2025, 6, 4), "Live on the Microsoft Store 🚀", "pastel", ["🚀 Launch", "🏪 Store"]);
        var storeSec = Locked(db, store, "Evening", At(2025, 6, 4, 21, 0));
        var sb = new ElementBuilder(db, store, storeSec);
        sb.Emoji("🚀", 620, 120, 96, 12);
        sb.Text("<p>It's live. After months of evenings, Typing 365 is on the Microsoft Store. Seven downloads in the first hour — four of them were me checking it works.</p>", 56, 400, 440, 140);
        sb.Photo(typingImg, "sticker", 480, 420, 260, 200, 4, focusX: 50, focusY: 40);
        sb.Note("Next", "<ul><li>Fix the leaderboard bug</li><li>Write the Reddit post</li></ul>", 56, 580, 230, 180, -2, "lilac");

        var pastel = NewPage(db, typing, new DateOnly(2025, 6, 11), "They said yes this time.", "pastel", []);
        var pastelSec = Locked(db, pastel, "Morning", At(2025, 6, 11, 9, 30));
        var t = new ElementBuilder(db, pastel, pastelSec);
        t.Photo(typingImg, "sticker", 476, 104, 256, 296, 5, focusX: 50, focusY: 45, alt: "Typing 365 leaderboard");
        t.Emoji("🎉", 690, 70, 74, 14);
        t.Text("<p>After rejecting it multiple times, Microsoft finally accepted my Reddit post about Typing 365. One week after the Store release, the app gets its first real public stage.</p><p>Lesson for future me: keep the post honest, keep improving it, keep knocking.</p>",
            56, 470, 400, 210);
        t.Checklist("how it went", [("Wrote the post", true), ("Rejected, more than once", true), ("Accepted · Jun 11", true)], 496, 462, 242, 170, 0, "lilac");
        t.Photo(typingImg, "plain", 56, 704, 682, 232, -1.5, focusX: 50, focusY: 45, alt: "Leaderboard artwork for Typing 365");
        t.Tape(340, 690, 110, 28, -4, "pink");
        t.Emoji("⭐", 700, 914, 56, -12);
        t.Link(store.Id, 56, 966, 460, 66);

        TypingEntry(db, typing, new(2024, 8, 28), "Day one: the first line of code", "⌨️", "<p>Opened a blank project and wrote <b>one</b> line. Starting is the hardest part.</p>");
        TypingEntry(db, typing, new(2024, 10, 2), "The first 100 WPM test", "⚡", "<p>Built the test screen. Hit 100 WPM on my own app, which feels like cheating.</p>");
        TypingEntry(db, typing, new(2024, 11, 23), "Leaderboards!", "🏆", "<p>Friends can now compete. Already three arguments about who's fastest.</p>", typingImg);
        TypingEntry(db, typing, new(2025, 1, 15), "Rewrote the engine", "🛠️", "<p>Threw away the first engine. The new one is twice as fast and half the code.</p>");
        TypingEntry(db, typing, new(2025, 3, 8), "Beta testers", "🧪", "<p>Twelve people tried it this week. Lots of notes, mostly kind.</p>");

        // ── Life · an editable welcome page for today ──────────────────────
        var welcome = NewPage(db, life, today, "Welcome to your notebook", "sea", ["👋 Hello", "✨ First page"]);
        var draft = new Section { PageId = welcome.Id, Seq = 1, Label = "Getting started", CreatedAt = now };
        db.Sections.Add(draft);
        var w = new ElementBuilder(db, welcome, draft);
        w.Shape("blob", 520, 360, 260, 240, 0, "accent2", 0.25);
        w.Emoji("👋", 630, 250, 90, -10);
        w.Text("<p>This is a <b>draft section</b>, so everything on this page is yours to play with. Drag things around, pull the corners to resize, and grab the round handle to tilt.</p><p>Double-click any text to edit it. Drop photos from your desktop straight onto the paper.</p>",
            56, 384, 430, 200);
        w.Checklist("First steps", [("Drag this note somewhere", false), ("Double-click to edit text", false), ("Drop a photo on the page", false), ("Try another theme →", false), ("Submit the section", false)],
            540, 400, 220, 220, 2, "mint");
        w.Quote("THE RULE", "You can always add. Once submitted, you can't change.", 56, 620, 440, 90);
        w.Note("How locking works", "<p>Press <b>Submit section</b> when you're done. You get 6 hours to fix typos, then it locks forever — like ink.</p>", 56, 740, 250, 200, -2, "yellow");
        w.Label("one page per day, as many as you like ✍️", 340, 780, 260, 60, -3);
        w.Divider("wave", 56, 980, 682, 24);

        await db.SaveChangesAsync();
        return true;

        // local helpers
        static DateTimeOffset At(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, TimeSpan.Zero);
    }

    private static Space NewSpace(DayleafDbContext db, string name, string slug, string emoji, string color, string desc, string theme, MediaFile? cover, DateTimeOffset created)
    {
        var s = new Space { Name = name, Slug = slug, Emoji = emoji, Color = color, Description = desc, DefaultTheme = theme, CoverMediaId = cover?.Id, CreatedAt = created };
        db.Spaces.Add(s);
        return s;
    }

    private static Page NewPage(DayleafDbContext db, Space space, DateOnly date, string title, string theme, List<string> tags)
    {
        var p = new Page
        {
            SpaceId = space.Id, Date = date, Title = title, Theme = theme, Tags = tags,
            CreatedAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero),
        };
        db.Pages.Add(p);
        return p;
    }

    private static Section Locked(DayleafDbContext db, Page page, string label, DateTimeOffset created)
    {
        var s = new Section { PageId = page.Id, Seq = 1, Label = label, CreatedAt = created, SubmittedAt = created.AddHours(1) };
        db.Sections.Add(s);
        return s;
    }

    private static Page FarmEntry(DayleafDbContext db, Space farm, DateOnly date, string title, string emoji, string html, string noteColor, MediaFile? photo = null)
        => SimpleEntry(db, farm, date, title, "field", emoji, html, noteColor, photo);

    private static Page TypingEntry(DayleafDbContext db, Space typing, DateOnly date, string title, string emoji, string html, MediaFile? photo = null)
        => SimpleEntry(db, typing, date, title, "pastel", emoji, html, "lilac", photo);

    private static Page SimpleEntry(DayleafDbContext db, Space space, DateOnly date, string title, string theme, string emoji, string html, string noteColor, MediaFile? photo)
    {
        var p = NewPage(db, space, date, title, theme, []);
        var sec = Locked(db, p, "Evening", new DateTimeOffset(date.ToDateTime(new TimeOnly(19, 0)), TimeSpan.Zero));
        var b = new ElementBuilder(db, p, sec);
        b.Emoji(emoji, 640, 250, 84, 10);
        b.Text(html, 56, 390, photo is null ? 460 : 400, 160);
        if (photo is not null)
            b.Photo(photo, "polaroid", 480, 380, 250, 230, 4, zoom: 1.8, focusX: Random.Shared.Next(10, 90), focusY: Random.Shared.Next(30, 80));
        b.Note(null, "<p>" + System.Net.WebUtility.HtmlEncode(title) + "</p>", 70, 600, 200, 150, -2.5, noteColor);
        return p;
    }

    private async Task<MediaFile> LoadImageAsync(string file)
    {
        var path = Path.Combine(env.ContentRootPath, "SeedData", file);
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "SeedData", file);
        await using var fs = File.OpenRead(path);
        return await media.SaveAsync(fs, file);
    }

    private sealed class ElementBuilder(DayleafDbContext db, Page page, Section section)
    {
        private int _z;

        private PageElement Add(ElementKind kind, ElementData data, double x, double y, double w, double h, double rot, Guid? media = null, Guid? link = null)
        {
            PageService.Normalize(data);
            var el = new PageElement
            {
                PageId = page.Id, SectionId = section.Id, Kind = kind, Sheet = 0,
                X = x, Y = y, W = w, H = h, Rotation = rot, Z = ++_z,
                DataJson = ElementDataJson.Serialize(data), PlainText = PageService.PlainTextOf(data),
                MediaId = media, LinkTargetPageId = link, CreatedAt = section.CreatedAt, UpdatedAt = section.CreatedAt,
            };
            db.Elements.Add(el);
            return el;
        }

        public void Emoji(string c, double x, double y, double size, double rot) => Add(ElementKind.Emoji, new EmojiData { Char = c }, x, y, size, size, rot);
        public void Text(string html, double x, double y, double w, double h) => Add(ElementKind.Text, new TextData { Html = html }, x, y, w, h, 0);
        public void Label(string text, double x, double y, double w, double h, double rot) => Add(ElementKind.Label, new LabelData { Text = text }, x, y, w, h, rot);
        public void Quote(string label, string text, double x, double y, double w, double h) => Add(ElementKind.Quote, new QuoteData { Label = label, Text = text }, x, y, w, h, 0);
        public void Divider(string style, double x, double y, double w, double h) => Add(ElementKind.Divider, new DividerData { Style = style }, x, y, w, h, 0);
        public void Tape(double x, double y, double w, double h, double rot, string color) => Add(ElementKind.Tape, new TapeData { Color = color }, x, y, w, h, rot);
        public void Shape(string shape, double x, double y, double w, double h, double rot, string color, double opacity) =>
            Add(ElementKind.Shape, new ShapeData { Shape = shape, Color = color, Opacity = opacity }, x, y, w, h, rot);
        public void Link(Guid target, double x, double y, double w, double h) => Add(ElementKind.PageLink, new PageLinkData(), x, y, w, h, 0, link: target);
        public void Note(string? title, string html, double x, double y, double w, double h, double rot, string color) =>
            Add(ElementKind.Note, new NoteData { Title = title, Html = html, Color = color }, x, y, w, h, rot);

        public void Checklist(string title, (string Text, bool Done)[] items, double x, double y, double w, double h, double rot, string color) =>
            Add(ElementKind.Checklist, new ChecklistData { Title = title, Color = color, Items = items.Select(i => new ChecklistItem { Text = i.Text, Done = i.Done }).ToList() }, x, y, w, h, rot);

        public void Photo(MediaFile m, string style, double x, double y, double w, double h, double rot,
            double zoom = 1, double focusX = 50, double focusY = 50, string? caption = null, string? alt = null) =>
            Add(ElementKind.Photo, new PhotoData { Style = style, Zoom = zoom, FocusX = focusX, FocusY = focusY, Caption = caption, Alt = alt }, x, y, w, h, rot, m.Id);
    }
}
