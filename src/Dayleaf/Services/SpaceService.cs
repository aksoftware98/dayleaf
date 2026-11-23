using Dayleaf.Data;
using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dayleaf.Services;

public interface ISpaceService
{
    Task<List<SpaceSummary>> ListAsync();
    Task<Space?> GetBySlugAsync(string slug);
    Task<Space?> GetAsync(Guid id);
    Task<SpaceSummary?> GetSummaryAsync(Guid id);
    Task<Space> CreateAsync(string name, string emoji, string color, string? description, string? defaultTheme);
    Task<Space> UpdateAsync(Guid id, string name, string emoji, string color, string? description, string defaultTheme);
    Task SetCoverAsync(Guid id, Guid? mediaId);
    Task DeleteAsync(Guid id, string typedName);
    Task<string> UniqueSlugAsync(string name, Guid? exceptId = null);
}

public sealed class SpaceService(IDbContextFactory<DayleafDbContext> dbf, TimeProvider clock) : ISpaceService
{
    public async Task<List<SpaceSummary>> ListAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var spaces = await db.Spaces.AsNoTracking().ToListAsync();
        var stats = await db.Pages.AsNoTracking()
            .GroupBy(p => p.SpaceId)
            .Select(g => new
            {
                SpaceId = g.Key,
                Count = g.Count(),
                First = g.Min(p => p.Date),
                Last = g.Max(p => p.Date),
                Shared = g.Count(p => p.ShareToken != null),
            })
            .ToListAsync();
        var activity = await db.Elements.AsNoTracking()
            .Join(db.Pages, e => e.PageId, p => p.Id, (e, p) => new { p.SpaceId, e.UpdatedAt })
            .GroupBy(x => x.SpaceId)
            .Select(g => new { SpaceId = g.Key, Last = g.Max(x => x.UpdatedAt) })
            .ToListAsync();

        return spaces
            .Select(s =>
            {
                var st = stats.FirstOrDefault(x => x.SpaceId == s.Id);
                var act = activity.FirstOrDefault(x => x.SpaceId == s.Id);
                return new SpaceSummary(s, st?.Count ?? 0, st?.First, st?.Last, st?.Shared ?? 0, act?.Last);
            })
            .OrderByDescending(x => x.LastActivity ?? x.Space.CreatedAt)
            .ToList();
    }

    public async Task<SpaceSummary?> GetSummaryAsync(Guid id) => (await ListAsync()).FirstOrDefault(s => s.Space.Id == id);

    public async Task<Space?> GetBySlugAsync(string slug)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Spaces.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug);
    }

    public async Task<Space?> GetAsync(Guid id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Spaces.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Space> CreateAsync(string name, string emoji, string color, string? description, string? defaultTheme)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new DayleafRuleException("Give the space a name.");
        if (name.Length > 120) throw new DayleafRuleException("That name is a little long — keep it under 120 characters.");
        await using var db = await dbf.CreateDbContextAsync();
        var space = new Space
        {
            Name = name,
            Slug = await UniqueSlugAsync(db, name, null),
            Emoji = string.IsNullOrWhiteSpace(emoji) ? "📓" : emoji.Trim(),
            Color = SpaceColors.Get(color).Id,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DefaultTheme = Themes.Get(defaultTheme).Id,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Spaces.Add(space);
        await db.SaveChangesAsync();
        return space;
    }

    public async Task<Space> UpdateAsync(Guid id, string name, string emoji, string color, string? description, string defaultTheme)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new DayleafRuleException("Give the space a name.");
        await using var db = await dbf.CreateDbContextAsync();
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.Id == id) ?? throw new DayleafRuleException("That space no longer exists.");
        if (space.Name != name)
        {
            space.Name = name;
            space.Slug = await UniqueSlugAsync(db, name, id);
        }
        space.Emoji = string.IsNullOrWhiteSpace(emoji) ? "📓" : emoji.Trim();
        space.Color = SpaceColors.Get(color).Id;
        space.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        space.DefaultTheme = Themes.Get(defaultTheme).Id;
        await db.SaveChangesAsync();
        return space;
    }

    public async Task SetCoverAsync(Guid id, Guid? mediaId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.Id == id) ?? throw new DayleafRuleException("That space no longer exists.");
        space.CoverMediaId = mediaId;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, string typedName)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.Id == id) ?? throw new DayleafRuleException("That space no longer exists.");
        if (!string.Equals(space.Name.Trim(), (typedName ?? "").Trim(), StringComparison.Ordinal))
            throw new DayleafRuleException($"Type “{space.Name}” exactly to delete this space.");
        db.Spaces.Remove(space);
        await db.SaveChangesAsync();
    }

    public async Task<string> UniqueSlugAsync(string name, Guid? exceptId = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await UniqueSlugAsync(db, name, exceptId);
    }

    internal static async Task<string> UniqueSlugAsync(DayleafDbContext db, string name, Guid? exceptId)
    {
        var baseSlug = Slug.From(name);
        if (baseSlug is "settings" or "new") baseSlug += "-space";
        var taken = await db.Spaces.Where(s => s.Slug.StartsWith(baseSlug) && s.Id != exceptId).Select(s => s.Slug).ToListAsync();
        taken.AddRange(db.Spaces.Local.Where(s => s.Id != exceptId).Select(s => s.Slug));
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
}
