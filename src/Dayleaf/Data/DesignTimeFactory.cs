using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dayleaf.Data;

/// <summary>Used by `dotnet ef` only.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DayleafDbContext>
{
    public DayleafDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DayleafDbContext>().UseSqlite("Data Source=design.db").Options);
}
