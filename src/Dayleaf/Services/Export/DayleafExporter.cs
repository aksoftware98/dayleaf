using System.IO.Compression;
using System.Text.Json;
using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services.Export;

public enum ExportScope { Library, Space, Page }

public sealed class DayleafExporter(IDbContextFactory<DayleafDbContext> dbf, IMediaService media, TimeProvider clock)
{
    /// <summary>Writes a .dayleaf archive to <paramref name="output"/>. The stream may be non-seekable.</summary>
    public async Task WriteAsync(Stream output, ExportScope scope, Guid? id = null, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var data = await BuildDataAsync(db, scope, id, ct);

        var manifest = new ManifestDto
        {
            Kind = scope.ToString().ToLowerInvariant(),
            ExportedAt = clock.GetUtcNow(),
            Counts = new()
            {
                ["spaces"] = data.Spaces.Count,
                ["pages"] = data.Spaces.Sum(s => s.Pages.Count),
                ["elements"] = data.Spaces.Sum(s => s.Pages.Sum(p => p.Elements.Count)),
                ["media"] = data.Media.Count,
            },
        };

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await WriteJsonAsync(zip, "manifest.json", manifest, ct);
        await WriteJsonAsync(zip, "data.json", data, ct);
        foreach (var m in data.Media)
        {
            var file = await media.GetAsync(m.Id);
            if (file is null) continue;
            var path = media.PathOf(file);
            if (!File.Exists(path)) continue;
            var entry = zip.CreateEntry(m.File, CompressionLevel.NoCompression);
            await using var es = entry.Open();
            await using var fs = File.OpenRead(path);
            await fs.CopyToAsync(es, ct);
        }
    }

    public async Task<string> FileNameAsync(ExportScope scope, Guid? id)
    {
        var today = clock.GetLocalNow().ToString("yyyy-MM-dd");
        await using var db = await dbf.CreateDbContextAsync();
        switch (scope)
        {
            case ExportScope.Space:
                var s = await db.Spaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return $"{s?.Slug ?? "space"}-{today}{DayleafFormat.Extension}";
            case ExportScope.Page:
                var p = await db.Pages.AsNoTracking().Include(x => x.Space).FirstOrDefaultAsync(x => x.Id == id);
                return $"{p?.Space?.Slug ?? "page"}-{p?.Date:yyyy-MM-dd}{DayleafFormat.Extension}";
            default:
                return $"dayleaf-library-{today}{DayleafFormat.Extension}";
        }
    }

    internal static async Task<DataDto> BuildDataAsync(DayleafDbContext db, ExportScope scope, Guid? id, CancellationToken ct)
    {
        IQueryable<Space> spaces = db.Spaces.AsNoTracking();
        Guid? onlyPage = null;
        switch (scope)
        {
            case ExportScope.Space:
                spaces = spaces.Where(s => s.Id == id);
                break;
            case ExportScope.Page:
                var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new InvalidOperationException("Page not found.");
                onlyPage = page.Id;
                spaces = spaces.Where(s => s.Id == page.SpaceId);
                break;
        }

        var spaceList = await spaces.OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var spaceIds = spaceList.Select(s => s.Id).ToList();
        var pages = await db.Pages.AsNoTracking()
            .Where(p => spaceIds.Contains(p.SpaceId) && (onlyPage == null || p.Id == onlyPage))
            .Include(p => p.Sections).Include(p => p.Elements)
            .AsSplitQuery()
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

        var mediaIds = new HashSet<Guid>();
        var data = new DataDto();
        foreach (var s in spaceList)
        {
            var sd = new SpaceDto
            {
                Id = s.Id, Name = s.Name, Slug = s.Slug, Emoji = s.Emoji, Color = s.Color, Description = s.Description,
                DefaultTheme = s.DefaultTheme, DefaultLanguage = s.DefaultLanguage, CoverMediaId = scope == ExportScope.Page ? null : s.CoverMediaId, CreatedAt = s.CreatedAt,
            };
            if (sd.CoverMediaId is { } cover) mediaIds.Add(cover);
            foreach (var p in pages.Where(p => p.SpaceId == s.Id))
            {
                var pd = new PageDto
                {
                    Id = p.Id, Date = p.Date, Title = p.Title, Theme = p.Theme, Language = p.Language, Tags = [.. p.Tags], SheetCount = p.SheetCount, CreatedAt = p.CreatedAt,
                    // ShareToken is never exported.
                    Sections = p.Sections.OrderBy(x => x.Seq).Select(x => new SectionDto
                    {
                        Id = x.Id, Seq = x.Seq, Label = x.Label, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt,
                    }).ToList(),
                    Elements = p.Elements.OrderBy(e => e.Sheet).ThenBy(e => e.Z).Select(e => new ElementDto
                    {
                        Id = e.Id, SectionId = e.SectionId, Kind = e.Kind.ToString().ToLowerInvariant(), Sheet = e.Sheet,
                        X = e.X, Y = e.Y, W = e.W, H = e.H, Rotation = e.Rotation, Z = e.Z,
                        MediaId = e.MediaId, LinkTargetPageId = e.LinkTargetPageId,
                        Data = JsonDocument.Parse(string.IsNullOrWhiteSpace(e.DataJson) ? "{}" : e.DataJson).RootElement.Clone(),
                        CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
                    }).ToList(),
                };
                foreach (var e in p.Elements.Where(e => e.MediaId != null)) mediaIds.Add(e.MediaId!.Value);
                sd.Pages.Add(pd);
            }
            data.Spaces.Add(sd);
        }

        var media = await db.Media.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).ToListAsync(ct);
        data.Media = media.Select(m => new MediaDto
        {
            Id = m.Id, File = $"media/{m.Id:N}{m.Extension}", ContentType = m.ContentType, Sha256 = m.Sha256, Size = m.Size, OriginalName = m.OriginalName,
        }).ToList();
        return data;
    }

    private static async Task WriteJsonAsync<T>(ZipArchive zip, string name, T value, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var s = entry.Open();
        await JsonSerializer.SerializeAsync(s, value, DayleafFormat.Json, ct);
    }
}
