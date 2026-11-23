using Dayleaf.Localization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services.Export;

/// <summary>
/// Imports .dayleaf archives. The file is treated as untrusted input (plan §9):
/// size/entry/ratio limits, only known paths are read, every id is remapped,
/// HTML is re-sanitized, coordinates clamped, and everything happens in one transaction.
/// </summary>
public sealed partial class DayleafImporter(
    IDbContextFactory<DayleafDbContext> dbf,
    DayleafPaths paths,
    TimeProvider clock)
{
    public const long MaxArchiveBytes = 2L * 1024 * 1024 * 1024;
    public const int MaxEntries = 20_000;
    public const double MaxCompressionRatio = 100;
    public const long MaxJsonBytes = 256L * 1024 * 1024;
    public const long MaxMediaBytes = 100L * 1024 * 1024;

    [GeneratedRegex(@"^media/[0-9a-f]{32}\.(jpg|png|webp|gif)$")]
    private static partial Regex MediaPath();

    public async Task<ManifestDto> ReadManifestAsync(string archivePath)
    {
        using var zip = OpenChecked(archivePath);
        return await ReadManifestAsync(zip);
    }

    public async Task<ImportSummary> ImportAsync(string archivePath, Guid? targetSpaceId = null, CancellationToken ct = default)
    {
        using var zip = OpenChecked(archivePath);
        var manifest = await ReadManifestAsync(zip);
        var data = await ReadJsonAsync<DataDto>(zip, "data.json") ?? throw new ImportRejectedException(Loc.T("data.json is missing or empty."));

        var isPage = manifest.Kind == "page";
        if (isPage && targetSpaceId is null) throw new ImportRejectedException(Loc.T("Pick a space to import this page into."));

        var now = clock.GetUtcNow();
        var tempDir = Path.Combine(paths.TempDirectory, $"import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var pendingMoves = new List<(string From, string To)>();

        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── media ─────────────────────────────────────────────
            var mediaMap = new Dictionary<Guid, Guid>();
            var newMediaBySha = new Dictionary<string, MediaFile>();
            foreach (var m in data.Media ?? [])
            {
                if (m.File is null || !MediaPath().IsMatch(m.File)) continue; // zip-slip & junk protection
                var entry = zip.GetEntry(m.File);
                if (entry is null) continue;
                if (entry.Length > MaxMediaBytes) throw new ImportRejectedException(Loc.T("An image in the file is too large ({0} MB).", entry.Length / 1024 / 1024));

                var temp = Path.Combine(tempDir, $"{Guid.NewGuid():N}.bin");
                string sha;
                byte[] head = new byte[64];
                int headLen = 0;
                await using (var src = entry.Open())
                await using (var dst = File.Create(temp))
                using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buf = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await src.ReadAsync(buf, ct)) > 0)
                    {
                        total += read;
                        if (total > MaxMediaBytes) throw new ImportRejectedException(Loc.T("An image in the file is larger than it claims."));
                        if (headLen < head.Length)
                        {
                            var n = Math.Min(head.Length - headLen, read);
                            Array.Copy(buf, 0, head, headLen, n);
                            headLen += n;
                        }
                        hasher.AppendData(buf, 0, read);
                        await dst.WriteAsync(buf.AsMemory(0, read), ct);
                    }
                    sha = Convert.ToHexStringLower(hasher.GetHashAndReset());
                }

                var kind = ImageSniffer.Detect(head.AsSpan(0, headLen));
                if (kind is null) { File.Delete(temp); continue; }

                var existing = await db.Media.AsNoTracking().FirstOrDefaultAsync(x => x.Sha256 == sha, ct);
                if (existing is not null && File.Exists(Path.Combine(paths.MediaDirectory, existing.FileName)))
                {
                    mediaMap[m.Id] = existing.Id;
                    File.Delete(temp);
                    continue;
                }
                if (newMediaBySha.TryGetValue(sha, out var dup))
                {
                    mediaMap[m.Id] = dup.Id;
                    File.Delete(temp);
                    continue;
                }

                MediaFile record;
                if (existing is not null)
                {
                    record = existing; // record without a file: restore the file
                }
                else
                {
                    var (w, h) = ImageSniffer.Dimensions(temp, kind.Extension);
                    record = new MediaFile
                    {
                        ContentType = kind.ContentType,
                        Extension = kind.Extension,
                        OriginalName = Path.GetFileName(m.OriginalName ?? "image"),
                        Size = new FileInfo(temp).Length,
                        Sha256 = sha,
                        Width = w,
                        Height = h,
                        CreatedAt = now,
                    };
                    db.Media.Add(record);
                }
                newMediaBySha[sha] = record;
                mediaMap[m.Id] = record.Id;
                pendingMoves.Add((temp, Path.Combine(paths.MediaDirectory, record.FileName)));
            }

            // ── page id map first (so links between imported pages resolve) ──
            var pageMap = new Dictionary<Guid, Guid>();
            foreach (var s in data.Spaces ?? [])
                foreach (var p in s.Pages ?? [])
                    pageMap.TryAdd(p.Id, Guid.NewGuid());

            var linkFixups = new List<(PageElement El, Guid OriginalTarget)>();
            int spaces = 0, pages = 0, photos = 0, elements = 0;
            var importStamp = clock.GetLocalNow().ToString("yyyy-MM-dd");

            Space? targetSpace = null;
            if (isPage)
            {
                targetSpace = await db.Spaces.FirstOrDefaultAsync(s => s.Id == targetSpaceId, ct)
                              ?? throw new ImportRejectedException(Loc.T("The target space doesn't exist."));
            }

            foreach (var sd in data.Spaces ?? [])
            {
                Space space;
                if (targetSpace is not null)
                {
                    space = targetSpace;
                }
                else
                {
                    var name = Cap(sd.Name, 100).Trim();
                    if (name.Length == 0) name = Loc.T("Imported space");
                    if (await db.Spaces.AnyAsync(x => x.Name == name, ct) || db.Spaces.Local.Any(x => x.Name == name))
                        name = Loc.T("{0} (imported {1})", name, importStamp);
                    space = new Space
                    {
                        Name = name,
                        Slug = await SpaceService.UniqueSlugAsync(db, name, null),
                        Emoji = string.IsNullOrWhiteSpace(sd.Emoji) ? "📓" : Cap(sd.Emoji, 16),
                        Color = SpaceColors.Get(sd.Color).Id,
                        Description = string.IsNullOrWhiteSpace(sd.Description) ? null : Cap(sd.Description, 1000),
                        DefaultTheme = Themes.Get(sd.DefaultTheme).Id,
                        DefaultLanguage = Lang.Normalize(sd.DefaultLanguage),
                        CoverMediaId = sd.CoverMediaId is { } c && mediaMap.TryGetValue(c, out var nc) ? nc : null,
                        CreatedAt = sd.CreatedAt == default ? now : sd.CreatedAt,
                    };
                    db.Spaces.Add(space);
                    spaces++;
                }

                foreach (var pd in sd.Pages ?? [])
                {
                    if (targetSpace is not null)
                    {
                        var clash = await db.Pages.AsNoTracking().FirstOrDefaultAsync(x => x.SpaceId == space.Id && x.Date == pd.Date, ct);
                        if (clash is not null)
                            throw new ImportRejectedException(string.IsNullOrWhiteSpace(clash.Title)
                                ? Loc.T("“{0}” already has a page on {1}. Move or delete it first.", space.Name, Dates.Format(pd.Date, DateStyle.Short))
                                : Loc.T("“{0}” already has a page on {1}: “{2}”. Move or delete it first.", space.Name, Dates.Format(pd.Date, DateStyle.Short), clash.Title));
                    }
                    if (db.Pages.Local.Any(x => x.SpaceId == space.Id && x.Date == pd.Date)) continue; // duplicate date inside the file

                    var page = new Page
                    {
                        Id = pageMap[pd.Id],
                        SpaceId = space.Id,
                        Date = pd.Date,
                        Title = Cap(pd.Title, 300),
                        Theme = Themes.Get(pd.Theme).Id,
                        Language = Lang.Normalize(pd.Language ?? sd.DefaultLanguage),
                        Tags = (pd.Tags ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => Cap(t.Trim(), 40)).Distinct().Take(8).ToList(),
                        SheetCount = Math.Clamp(pd.SheetCount, 1, 20),
                        CreatedAt = pd.CreatedAt == default ? now : pd.CreatedAt,
                    };
                    db.Pages.Add(page);
                    pages++;

                    var sectionMap = new Dictionary<Guid, Guid>();
                    var seq = 0;
                    foreach (var secd in (pd.Sections ?? []).OrderBy(x => x.Seq))
                    {
                        var sec = new Section
                        {
                            PageId = page.Id,
                            Seq = ++seq,
                            Label = Cap(string.IsNullOrWhiteSpace(secd.Label) ? "Section" : secd.Label, 60),
                            // Keep timestamps exactly: locked content stays locked.
                            CreatedAt = secd.CreatedAt == default ? now : secd.CreatedAt,
                            SubmittedAt = secd.SubmittedAt,
                        };
                        sectionMap[secd.Id] = sec.Id;
                        db.Sections.Add(sec);
                    }
                    if (sectionMap.Count == 0)
                    {
                        // A page always has at least one section; content without a section is locked as of now.
                        var sec = new Section { PageId = page.Id, Seq = 1, Label = Loc.T("Imported"), CreatedAt = now, SubmittedAt = now };
                        db.Sections.Add(sec);
                    }

                    foreach (var ed in pd.Elements ?? [])
                    {
                        if (!Enum.TryParse<ElementKind>(ed.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind)) continue;
                        if (!sectionMap.TryGetValue(ed.SectionId, out var sectionId)) continue;

                        Guid? mediaId = null;
                        if (ed.MediaId is { } mid)
                        {
                            if (mediaMap.TryGetValue(mid, out var nm)) mediaId = nm;
                            else if (kind == ElementKind.Photo) continue;
                        }
                        if (kind == ElementKind.Photo && mediaId is null) continue;

                        var payload = ed.Data.ValueKind == JsonValueKind.Object
                            ? ElementDataJson.Deserialize(kind, ed.Data)
                            : ElementDataJson.CreateDefault(kind);
                        PageService.Normalize(payload); // re-sanitize everything

                        var (w, h) = LayoutService.ClampSize(Finite(ed.W, 100), Finite(ed.H, 100));
                        var (x, y) = LayoutService.Clamp(Finite(ed.X, 56), Finite(ed.Y, 100), w, h);
                        var el = new PageElement
                        {
                            PageId = page.Id,
                            SectionId = sectionId,
                            Kind = kind,
                            Sheet = Math.Clamp(ed.Sheet, 0, page.SheetCount - 1),
                            X = x, Y = y, W = w, H = h,
                            Rotation = Math.Clamp(Finite(ed.Rotation, 0), -180, 180),
                            Z = ed.Z,
                            DataJson = ElementDataJson.Serialize(payload),
                            PlainText = PageService.PlainTextOf(payload),
                            MediaId = mediaId,
                            CreatedAt = ed.CreatedAt == default ? now : ed.CreatedAt,
                            UpdatedAt = ed.UpdatedAt == default ? now : ed.UpdatedAt,
                        };
                        if (kind == ElementKind.PageLink && ed.LinkTargetPageId is { } target) linkFixups.Add((el, target));
                        db.Elements.Add(el);
                        elements++;
                        if (kind == ElementKind.Photo) photos++;
                    }
                }
            }

            // Links: imported page → remap; local page → keep; otherwise → null ("Page not available").
            foreach (var (el, target) in linkFixups)
            {
                if (pageMap.TryGetValue(target, out var mapped) && db.Pages.Local.Any(p => p.Id == mapped)) el.LinkTargetPageId = mapped;
                else if (await db.Pages.AnyAsync(p => p.Id == target, ct)) el.LinkTargetPageId = target;
                else el.LinkTargetPageId = null;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            Directory.CreateDirectory(paths.MediaDirectory);
            foreach (var (from, to) in pendingMoves)
                File.Move(from, to, overwrite: true);

            var msg = isPage
                ? Loc.T("Imported {0} into {1}.", Loc.Plural(pages, Noun.Page), targetSpace!.Name)
                : Loc.T("Imported {0}, {1}, {2}.", Loc.Plural(spaces, Noun.Space), Loc.Plural(pages, Noun.Page), Loc.Plural(photos, Noun.Photo));
            return new ImportSummary(spaces, pages, photos, elements, msg);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static ZipArchive OpenChecked(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new ImportRejectedException(Loc.T("The file wasn't found."));
        if (info.Length > MaxArchiveBytes) throw new ImportRejectedException(Loc.T("That file is over 2 GB."));
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            throw new ImportRejectedException(Loc.T("That isn't a .dayleaf file (it's not a valid zip archive)."));
        }
        try
        {
            if (zip.Entries.Count > MaxEntries) throw new ImportRejectedException(Loc.T("That file has too many entries (over {0}).", MaxEntries));
            foreach (var e in zip.Entries)
            {
                var ratio = e.Length / (double)Math.Max(1, e.CompressedLength);
                if (ratio > MaxCompressionRatio) throw new ImportRejectedException(Loc.T("That file looks unsafe (an entry is compressed suspiciously well) and was rejected."));
            }
            return zip;
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    private static async Task<ManifestDto> ReadManifestAsync(ZipArchive zip)
    {
        var manifest = await ReadJsonAsync<ManifestDto>(zip, "manifest.json") ?? throw new ImportRejectedException(Loc.T("manifest.json is missing."));
        if (manifest.Format != DayleafFormat.FormatName) throw new ImportRejectedException(Loc.T("That isn't a Dayleaf file."));
        if (manifest.Version > DayleafFormat.Version)
            throw new ImportRejectedException(Loc.T("This file was made by a newer Dayleaf (format v{0}). Update Dayleaf to import it.", manifest.Version));
        if (manifest.Version < 1) throw new ImportRejectedException(Loc.T("Unknown file version."));
        if (manifest.Kind is not ("library" or "space" or "page")) throw new ImportRejectedException(Loc.T("Unknown export kind."));
        return manifest;
    }

    private static async Task<T?> ReadJsonAsync<T>(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return default;
        if (entry.Length > MaxJsonBytes) throw new ImportRejectedException(Loc.T("{0} is too large.", name));
        await using var s = entry.Open();
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(s, DayleafFormat.Json);
        }
        catch (JsonException ex)
        {
            throw new ImportRejectedException(Loc.T("{0} is damaged: {1}", name, ex.Message));
        }
    }

    private static double Finite(double v, double fallback) => double.IsFinite(v) ? v : fallback;
    private static string Cap(string? s, int max) => s is null ? "" : s.Length > max ? s[..max] : s;
}
