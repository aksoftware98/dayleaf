using Dayleaf.Data;
using Dayleaf.Domain;
using Dayleaf.Services;
using Dayleaf.Services.Export;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dayleaf.Tests;

/// <summary>In-memory SQLite + fake clock + temp data directory. Everything a service test needs.</summary>
public sealed class TestHost : IDisposable
{
    private readonly SqliteConnection _conn;
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
    public DayleafPaths Paths { get; }
    public IDbContextFactory<DayleafDbContext> Db { get; }
    public SectionLockPolicy Policy { get; }
    public DayleafOptions Options { get; } = new();
    public SpaceService Spaces { get; }
    public PageService Pages { get; }
    public TimelineService Timeline { get; }
    public MediaService Media { get; }
    public SearchService Search { get; }

    public TestHost(double editWindowHours = 6, double autoSubmitHours = 24)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<DayleafDbContext>().UseSqlite(_conn).Options;
        Db = new Factory(opts);
        using (var db = Db.CreateDbContext()) db.Database.Migrate();

        Paths = new DayleafPaths(Path.Combine(Path.GetTempPath(), "dayleaf-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Paths.MediaDirectory);
        Options.EditWindowHours = editWindowHours;
        Options.AutoSubmitDraftAfterHours = autoSubmitHours;
        Policy = new SectionLockPolicy(Microsoft.Extensions.Options.Options.Create(Options));
        Spaces = new SpaceService(Db, Clock);
        Pages = new PageService(Db, Clock, Policy, new LayoutService());
        Timeline = new TimelineService(Db);
        Media = new MediaService(Db, Paths, Microsoft.Extensions.Options.Options.Create(Options), Clock);
        Search = new SearchService(Db);
    }

    public DayleafExporter Exporter() => new(Db, Media, Clock);
    public DayleafImporter Importer() => new(Db, Paths, Clock);

    public async Task<(Space Space, Page Page)> NewPageAsync(string spaceName = "Our Farm", DateOnly? date = null)
    {
        var space = await Spaces.CreateAsync(spaceName, "🌳", "marigold", null, "field");
        var page = await Pages.GetOrCreatePageAsync(space.Id, date ?? new DateOnly(2026, 11, 16), "Starting over");
        return (space, page);
    }

    public static byte[] TinyPng(byte seed = 0)
    {
        // 1×1 PNG; the seed changes a byte in the tEXt-free IDAT CRC-irrelevant area so hashes differ.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        var copy = png.Concat(new[] { seed }).ToArray();
        return copy;
    }

    public void Dispose()
    {
        _conn.Dispose();
        try { Directory.Delete(Paths.DataDirectory, true); } catch { }
    }

    private sealed class Factory(DbContextOptions<DayleafDbContext> o) : IDbContextFactory<DayleafDbContext>
    {
        public DayleafDbContext CreateDbContext() => new(o);
    }
}
