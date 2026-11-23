using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dayleaf.Domain;

/// <summary>Typed payload stored in <see cref="PageElement.DataJson"/>. One class per <see cref="ElementKind"/>.</summary>
public abstract class ElementData
{
    public abstract ElementKind Kind { get; }

    /// <summary>Text used for search. HTML is stripped by the caller.</summary>
    public virtual string? SearchText() => null;

    public ElementData Copy() => ElementDataJson.Deserialize(Kind, ElementDataJson.Serialize(this));
}

public sealed class TextData : ElementData
{
    public override ElementKind Kind => ElementKind.Text;
    public string Html { get; set; } = "";
    public string Size { get; set; } = "m";       // s, m, l, xl
    public string Font { get; set; } = "body";    // body, display, hand, mono
    public string Color { get; set; } = "ink";    // ink, accent, accent2, muted
    public string Align { get; set; } = "start"; // start, center, end (logical); left/right kept for older pages
    public string? Highlight { get; set; }        // null, marker, box
    public override string? SearchText() => Html;
}

public sealed class HeadingData : ElementData
{
    public override ElementKind Kind => ElementKind.Heading;
    public string Text { get; set; } = "";
    public string Size { get; set; } = "l";       // m, l, xl
    public string Color { get; set; } = "ink";
    public string Align { get; set; } = "start"; // start, center, end (logical); left/right kept for older pages
    public override string? SearchText() => Text;
}

public sealed class PhotoData : ElementData
{
    public override ElementKind Kind => ElementKind.Photo;
    public string Style { get; set; } = "sticker"; // plain, sticker, polaroid, taped, round
    public string? Caption { get; set; }
    public double FocusX { get; set; } = 50;       // object-position %
    public double FocusY { get; set; } = 50;
    public double Zoom { get; set; } = 1;          // 1..4 crop zoom
    public string Filter { get; set; } = "none";   // none, warm, cool, mono, fade, vivid
    public string? Alt { get; set; }
    public override string? SearchText() => string.Join(' ', new[] { Caption, Alt }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed class EmojiData : ElementData
{
    public override ElementKind Kind => ElementKind.Emoji;
    public string Char { get; set; } = "🌱";
}

public sealed class NoteData : ElementData
{
    public override ElementKind Kind => ElementKind.Note;
    public string? Title { get; set; }
    public string Html { get; set; } = "";
    public string Color { get; set; } = "yellow";  // yellow, pink, mint, sky, lilac
    public override string? SearchText() => $"{Title} {Html}";
}

public sealed class ChecklistItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class ChecklistData : ElementData
{
    public override ElementKind Kind => ElementKind.Checklist;
    public string? Title { get; set; }
    public List<ChecklistItem> Items { get; set; } = [];
    public string Color { get; set; } = "yellow";  // yellow, pink, mint, sky, lilac, paper
    public override string? SearchText() => $"{Title} {string.Join(' ', Items.Select(i => i.Text))}";
}

public sealed class PageLinkData : ElementData
{
    public override ElementKind Kind => ElementKind.PageLink;
    public string? Note { get; set; }
    public override string? SearchText() => Note;
}

public sealed class QuoteData : ElementData
{
    public override ElementKind Kind => ElementKind.Quote;
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public override string? SearchText() => $"{Label} {Text}";
}

public sealed class LabelData : ElementData
{
    public override ElementKind Kind => ElementKind.Label;
    public string Text { get; set; } = "";
    public string Color { get; set; } = "accent";
    public string Size { get; set; } = "m";
    public override string? SearchText() => Text;
}

public sealed class DividerData : ElementData
{
    public override ElementKind Kind => ElementKind.Divider;
    public string Style { get; set; } = "line";    // line, dots, wave, stars
}

public sealed class TapeData : ElementData
{
    public override ElementKind Kind => ElementKind.Tape;
    public string Color { get; set; } = "theme";   // theme, yellow, pink, mint, sky, lilac
    public string Pattern { get; set; } = "plain"; // plain, stripes, dots, grid
}

public sealed class ShapeData : ElementData
{
    public override ElementKind Kind => ElementKind.Shape;
    public string Shape { get; set; } = "blob";    // circle, rect, blob, arch, star
    public string Color { get; set; } = "accent2"; // accent, accent2, chip, note, ink
    public double Opacity { get; set; } = 0.35;
}

public static class ElementDataJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public static Type TypeFor(ElementKind kind) => kind switch
    {
        ElementKind.Text => typeof(TextData),
        ElementKind.Heading => typeof(HeadingData),
        ElementKind.Photo => typeof(PhotoData),
        ElementKind.Emoji => typeof(EmojiData),
        ElementKind.Note => typeof(NoteData),
        ElementKind.Checklist => typeof(ChecklistData),
        ElementKind.PageLink => typeof(PageLinkData),
        ElementKind.Quote => typeof(QuoteData),
        ElementKind.Label => typeof(LabelData),
        ElementKind.Divider => typeof(DividerData),
        ElementKind.Tape => typeof(TapeData),
        ElementKind.Shape => typeof(ShapeData),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ElementData CreateDefault(ElementKind kind) => (ElementData)Activator.CreateInstance(TypeFor(kind))!;

    public static string Serialize(ElementData data) => JsonSerializer.Serialize(data, data.GetType(), Options);

    public static ElementData Deserialize(ElementKind kind, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return CreateDefault(kind);
        try
        {
            return (ElementData?)JsonSerializer.Deserialize(json, TypeFor(kind), Options) ?? CreateDefault(kind);
        }
        catch (JsonException)
        {
            return CreateDefault(kind);
        }
    }

    public static ElementData Deserialize(ElementKind kind, JsonElement element) => Deserialize(kind, element.GetRawText());

    public static T Read<T>(this PageElement el) where T : ElementData => (T)Deserialize(el.Kind, el.DataJson);
    public static ElementData ReadData(this PageElement el) => Deserialize(el.Kind, el.DataJson);
}
