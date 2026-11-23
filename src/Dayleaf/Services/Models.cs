using Dayleaf.Domain;

namespace Dayleaf.Services;

public sealed record SpaceSummary(
    Space Space,
    int PageCount,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    int SharedCount,
    DateTimeOffset? LastActivity);

public sealed record PageRef(
    Guid Id,
    Guid SpaceId,
    string SpaceSlug,
    string SpaceName,
    string SpaceEmoji,
    string SpaceColor,
    DateOnly Date,
    string Title,
    string Theme,
    int Number,
    int PhotoCount,
    int StickerCount,
    IReadOnlyList<Guid> PhotoIds,
    string? Excerpt,
    bool IsShared)
{
    public string Url => $"/s/{SpaceSlug}/{Date:yyyy-MM-dd}";
}

public sealed record PrevNext(PageRef? Prev, PageRef? Next);

/// <summary>Everything an A4Sheet needs to render a page. Built once, rendered in View, Edit or Print mode.</summary>
public sealed class SheetModel
{
    public required Guid PageId { get; init; }
    public required Guid SpaceId { get; init; }
    public required string SpaceName { get; init; }
    public required string SpaceSlug { get; init; }
    public required string SpaceEmoji { get; init; }
    public required DateOnly Date { get; set; }
    public required string Title { get; set; }
    public required string Theme { get; set; }
    public string Language { get; set; } = "en";
    public required List<string> Tags { get; set; }
    public required int SheetCount { get; set; }
    public required int PageNumber { get; set; }
    public required List<SheetElement> Elements { get; init; }
    public Dictionary<Guid, LinkCard?> LinkCards { get; init; } = [];
    /// <summary>Resolve image URLs. Default: /media/{id}. HTML export swaps in data: URIs.</summary>
    public Func<Guid, string> MediaUrl { get; set; } = id => $"/media/{id:N}";
}

public sealed class SheetElement
{
    public required PageElement Element { get; set; }
    public required ElementData Data { get; set; }
    public bool Editable { get; set; }
    public string SectionLabel { get; set; } = "";
    public int Version { get; set; }
}

public sealed record LinkCard(Guid PageId, string Url, string SpaceName, string SpaceEmoji, DateOnly Date, string Title, string Theme, Guid? PhotoId, int Number);

public sealed record SearchHit(PageRef Page, string Snippet, IReadOnlyList<string> Terms, bool TitleMatch);

public sealed record Placement(int Sheet, double X, double Y, double W, double H, bool AddsSheet);
