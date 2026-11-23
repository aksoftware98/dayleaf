using Dayleaf.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dayleaf.Data;

public sealed class DayleafDbContext(DbContextOptions<DayleafDbContext> options) : DbContext(options)
{
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<PageElement> Elements => Set<PageElement>();
    public DbSet<MediaFile> Media => Set<MediaFile>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite can't order DateTimeOffset natively: store as UTC ticks (sortable integers).
        b.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        b.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Space>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Slug).HasMaxLength(140);
            e.Property(x => x.DefaultLanguage).HasMaxLength(8).HasDefaultValue("en");
            e.HasMany(x => x.Pages).WithOne(p => p.Space).HasForeignKey(p => p.SpaceId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Page>(e =>
        {
            e.HasIndex(x => new { x.SpaceId, x.Date }).IsUnique();
            e.HasIndex(x => x.ShareToken).IsUnique();
            e.HasIndex(x => x.Date);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Language).HasMaxLength(8).HasDefaultValue("en");
            e.HasMany(x => x.Sections).WithOne(s => s.Page).HasForeignKey(s => s.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Elements).WithOne().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Section>(e =>
        {
            e.HasIndex(x => new { x.PageId, x.Seq }).IsUnique();
        });

        m.Entity<PageElement>(e =>
        {
            e.HasIndex(x => x.PageId);
            e.HasIndex(x => x.LinkTargetPageId);
            e.HasIndex(x => x.MediaId);
            e.HasIndex(x => x.SectionId);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        });

        m.Entity<MediaFile>(e =>
        {
            e.HasIndex(x => x.Sha256).IsUnique();
            e.Ignore(x => x.FileName);
        });
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));
}
