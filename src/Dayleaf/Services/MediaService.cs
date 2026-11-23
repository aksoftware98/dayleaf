using Dayleaf.Localization;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dayleaf.Services;

public interface IMediaService
{
    Task<MediaFile> SaveAsync(Stream content, string originalName, CancellationToken ct = default);
    Task<MediaFile?> GetAsync(Guid id);
    string PathOf(MediaFile media);
    Task<int> CleanupOrphansAsync();
    Task<bool> IsOnSharedPageAsync(Guid id);
    long MaxUploadBytes { get; }
}

public sealed class MediaService(
    IDbContextFactory<DayleafDbContext> dbf,
    DayleafPaths paths,
    IOptions<DayleafOptions> options,
    TimeProvider clock) : IMediaService
{
    public long MaxUploadBytes => options.Value.MaxUploadMb * 1024L * 1024L;

    public string PathOf(MediaFile media) => Path.Combine(paths.MediaDirectory, media.FileName);

    public async Task<MediaFile?> GetAsync(Guid id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<MediaFile> SaveAsync(Stream content, string originalName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(paths.TempDirectory);
        Directory.CreateDirectory(paths.MediaDirectory);
        var temp = Path.Combine(paths.TempDirectory, $"{Guid.NewGuid():N}.upload");
        try
        {
            string sha;
            long size;
            byte[] head = new byte[64];
            int headLen;
            await using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                size = 0;
                headLen = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > MaxUploadBytes) throw new DayleafRuleException(Loc.T("That file is bigger than {0} MB.", options.Value.MaxUploadMb));
                    if (headLen < head.Length)
                    {
                        var n = Math.Min(head.Length - headLen, read);
                        Array.Copy(buffer, 0, head, headLen, n);
                        headLen += n;
                    }
                    hasher.AppendData(buffer, 0, read);
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                sha = Convert.ToHexStringLower(hasher.GetHashAndReset());
            }

            var kind = ImageSniffer.Detect(head.AsSpan(0, headLen))
                       ?? throw new DayleafRuleException(Loc.T("That doesn't look like a JPEG, PNG, WebP or GIF image."));

            await using var db = await dbf.CreateDbContextAsync();
            var existing = await db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == sha, ct);
            if (existing is not null && File.Exists(PathOf(existing)))
            {
                File.Delete(temp);
                return existing;
            }
            if (existing is not null)
            {
                // Record exists but the file was lost: restore it.
                File.Move(temp, PathOf(existing), overwrite: true);
                return existing;
            }

            var (w, h) = ImageSniffer.Dimensions(temp, kind.Extension);
            var media = new MediaFile
            {
                ContentType = kind.ContentType,
                Extension = kind.Extension,
                OriginalName = SafeName(originalName),
                Size = size,
                Sha256 = sha,
                Width = w,
                Height = h,
                CreatedAt = clock.GetUtcNow(),
            };
            File.Move(temp, PathOf(media), overwrite: true);
            db.Media.Add(media);
            await db.SaveChangesAsync(ct);
            return media;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<bool> IsOnSharedPageAsync(Guid id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Elements.Where(e => e.MediaId == id)
            .Join(db.Pages, e => e.PageId, p => p.Id, (e, p) => p.ShareToken)
            .AnyAsync(t => t != null);
    }

    public async Task<int> CleanupOrphansAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var used = new HashSet<Guid>(await db.Elements.Where(e => e.MediaId != null).Select(e => e.MediaId!.Value).ToListAsync());
        foreach (var c in await db.Spaces.Where(s => s.CoverMediaId != null).Select(s => s.CoverMediaId!.Value).ToListAsync()) used.Add(c);
        var orphans = await db.Media.Where(m => !used.Contains(m.Id)).ToListAsync();
        foreach (var m in orphans)
        {
            var p = PathOf(m);
            if (File.Exists(p)) File.Delete(p);
        }
        db.Media.RemoveRange(orphans);
        await db.SaveChangesAsync();
        return orphans.Count;
    }

    private static string SafeName(string? name)
    {
        name = Path.GetFileName(name ?? "image");
        return name.Length > 120 ? name[..120] : name;
    }
}

public sealed record ImageKind(string ContentType, string Extension);

/// <summary>Detects image type by magic bytes (never trust extensions) and reads pixel size from headers.</summary>
public static class ImageSniffer
{
    public static ImageKind? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return new("image/jpeg", ".jpg");
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return new("image/png", ".png");
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return new("image/webp", ".webp");
        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8))) return new("image/gif", ".gif");
        return null;
    }

    public static (int? W, int? H) Dimensions(string path, string ext)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[Math.Min(fs.Length, 256 * 1024)];
            var n = fs.ReadAtLeast(buf, buf.Length, throwOnEndOfStream: false);
            return Dimensions(buf.AsSpan(0, n), ext);
        }
        catch
        {
            return (null, null);
        }
    }

    public static (int? W, int? H) Dimensions(ReadOnlySpan<byte> b, string ext)
    {
        switch (ext)
        {
            case ".png" when b.Length >= 24:
                return ((int)BinaryPrimitives.ReadUInt32BigEndian(b[16..]), (int)BinaryPrimitives.ReadUInt32BigEndian(b[20..]));
            case ".gif" when b.Length >= 10:
                return (BinaryPrimitives.ReadUInt16LittleEndian(b[6..]), BinaryPrimitives.ReadUInt16LittleEndian(b[8..]));
            case ".webp" when b.Length >= 30:
            {
                var chunk = b[12..16];
                if (chunk.SequenceEqual("VP8X"u8))
                    return (1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16));
                if (chunk.SequenceEqual("VP8 "u8))
                    return (BinaryPrimitives.ReadUInt16LittleEndian(b[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(b[28..]) & 0x3FFF);
                if (chunk.SequenceEqual("VP8L"u8))
                {
                    var bits = BinaryPrimitives.ReadUInt32LittleEndian(b[21..]);
                    return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
                }
                return (null, null);
            }
            case ".jpg":
            {
                var i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var marker = b[i + 1];
                    if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
                    var len = BinaryPrimitives.ReadUInt16BigEndian(b[(i + 2)..]);
                    if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                        return (BinaryPrimitives.ReadUInt16BigEndian(b[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(b[(i + 5)..]));
                    i += 2 + len;
                }
                return (null, null);
            }
        }
        return (null, null);
    }
}
