using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dayleaf.Services.Export;

/// <summary>DTOs for the .dayleaf zip container (manifest.json + data.json + media/*). Version 1.</summary>
public static class DayleafFormat
{
    public const string FormatName = "dayleaf";
    public const int Version = 1;
    public const string Extension = ".dayleaf";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public sealed class ManifestDto
{
    public string Format { get; set; } = DayleafFormat.FormatName;
    public int Version { get; set; } = DayleafFormat.Version;
    public string Kind { get; set; } = "library"; // library | space | page
    public DateTimeOffset ExportedAt { get; set; }
    public string App { get; set; } = "Dayleaf 1.0.0";
    public Dictionary<string, int> Counts { get; set; } = [];
}

public sealed class DataDto
{
    public List<SpaceDto> Spaces { get; set; } = [];
    public List<MediaDto> Media { get; set; } = [];
}

public sealed class SpaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Color { get; set; } = "";
    public string? Description { get; set; }
    public string DefaultTheme { get; set; } = "";
    public string? DefaultLanguage { get; set; }
    public Guid? CoverMediaId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<PageDto> Pages { get; set; } = [];
}

public sealed class PageDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = "";
    public string Theme { get; set; } = "";
    public string? Language { get; set; }
    public List<string> Tags { get; set; } = [];
    public int SheetCount { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public List<SectionDto> Sections { get; set; } = [];
    public List<ElementDto> Elements { get; set; } = [];
}

public sealed class SectionDto
{
    public Guid Id { get; set; }
    public int Seq { get; set; }
    public string Label { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public sealed class ElementDto
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public string Kind { get; set; } = "";
    public int Sheet { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double Rotation { get; set; }
    public int Z { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? LinkTargetPageId { get; set; }
    public JsonElement Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MediaDto
{
    public Guid Id { get; set; }
    public string File { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public string OriginalName { get; set; } = "";
}

public sealed record ImportSummary(int Spaces, int Pages, int Photos, int Elements, string Message);

public sealed class ImportRejectedException(string message) : Exception(message);
