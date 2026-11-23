namespace Dayleaf.Domain;

public sealed class Space
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Emoji { get; set; } = "📓";
    public string Color { get; set; } = "forest";
    public string? Description { get; set; }
    public string DefaultTheme { get; set; } = Themes.Default;
    public Guid? CoverMediaId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Page> Pages { get; set; } = [];
}

public sealed class Page
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = "";
    public string Theme { get; set; } = Themes.Default;
    public List<string> Tags { get; set; } = [];
    public int SheetCount { get; set; } = 1;
    public string? ShareToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Space? Space { get; set; }
    public List<Section> Sections { get; set; } = [];
    public List<PageElement> Elements { get; set; } = [];
}

public sealed class Section
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageId { get; set; }
    public int Seq { get; set; }
    public string Label { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    public Page? Page { get; set; }
}

public enum ElementKind
{
    Text,
    Heading,
    Photo,
    Emoji,
    Note,
    Checklist,
    PageLink,
    Quote,
    Label,
    Divider,
    Tape,
    Shape,
}

public sealed class PageElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageId { get; set; }
    public Guid SectionId { get; set; }
    public ElementKind Kind { get; set; }
    public int Sheet { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double Rotation { get; set; }
    public int Z { get; set; }
    public string DataJson { get; set; } = "{}";
    public string? PlainText { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? LinkTargetPageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PageElement Clone() => (PageElement)MemberwiseClone();
}

public sealed class MediaFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContentType { get; set; } = "";
    public string Extension { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string FileName => $"{Id:N}{Extension}";
}
