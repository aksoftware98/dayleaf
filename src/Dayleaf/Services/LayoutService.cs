using Dayleaf.Domain;

namespace Dayleaf.Services;

/// <summary>Decides where new elements go and when a page needs another sheet (plan §8).</summary>
public sealed class LayoutService
{
    public const double SheetW = 794;
    public const double SheetH = 1123;
    public const double SideMargin = 56;
    public const double EdgeMargin = 40;
    public const double FirstSheetContentTop = 380;
    public const double LaterSheetContentTop = 100;
    public const double BottomLimit = SheetH - 90; // keep clear of the footer
    public const double Gap = 16;

    public static (double W, double H) DefaultSize(ElementKind kind) => kind switch
    {
        ElementKind.Text => (440, 120),
        ElementKind.Heading => (560, 70),
        ElementKind.Photo => (460, 310),
        ElementKind.Emoji => (64, 64),
        ElementKind.Note => (210, 190),
        ElementKind.Checklist => (220, 200),
        ElementKind.PageLink => (440, 150),
        ElementKind.Quote => (440, 80),
        ElementKind.Label => (200, 60),
        ElementKind.Divider => (682, 24),
        ElementKind.Tape => (120, 30),
        ElementKind.Shape => (180, 180),
        _ => (200, 100),
    };

    /// <summary>Photo size from the image's aspect ratio, max 460 wide (or 190 square for round).</summary>
    public static (double W, double H) PhotoSize(int? width, int? height, string style = "sticker")
    {
        if (style == "round") return (220, 220);
        if (width is not > 0 || height is not > 0) return DefaultSize(ElementKind.Photo);
        var ratio = (double)height.Value / width.Value;
        double w = ratio > 1.2 ? 300 : 460;
        var h = Math.Round(w * ratio);
        if (h > 520) { h = 520; w = Math.Round(h / ratio); }
        return (w, h);
    }

    public static double ContentTop(int sheet) => sheet == 0 ? FirstSheetContentTop : LaterSheetContentTop;

    /// <param name="rtl">Right-to-left pages (Arabic) align new elements to the right margin.</param>
    public Placement Place(IReadOnlyCollection<PageElement> existing, int sheetCount, double w, double h, (int Sheet, double X, double Y)? at = null, bool rtl = false)
    {
        sheetCount = Math.Max(1, sheetCount);
        if (at is { } p)
        {
            var sheet = Math.Clamp(p.Sheet, 0, sheetCount - 1);
            var (cx, cy) = Clamp(p.X, p.Y, w, h);
            return new Placement(sheet, cx, cy, w, h, false);
        }

        var x = rtl ? Math.Max(SideMargin, SheetW - SideMargin - w) : SideMargin;
        var last = sheetCount - 1;
        var onLast = existing.Where(e => e.Sheet == last).ToList();
        var y = onLast.Count == 0
            ? ContentTop(last)
            : Math.Max(ContentTop(last), onLast.Max(e => e.Y + e.H) + Gap);

        if (y + h <= BottomLimit)
            return new Placement(last, x, Math.Round(y), w, h, false);

        return new Placement(last + 1, x, LaterSheetContentTop, w, h, true);
    }

    /// <summary>Clamp so at least 24px of the element stays on the sheet.</summary>
    public static (double X, double Y) Clamp(double x, double y, double w, double h)
    {
        const double keep = 24;
        x = Math.Clamp(x, keep - w, SheetW - keep);
        y = Math.Clamp(y, keep - h, SheetH - keep);
        return (Math.Round(x, 1), Math.Round(y, 1));
    }

    public static (double W, double H) ClampSize(double w, double h) =>
        (Math.Clamp(Math.Round(w, 1), 12, SheetW * 1.5), Math.Clamp(Math.Round(h, 1), 8, SheetH * 1.5));
}
