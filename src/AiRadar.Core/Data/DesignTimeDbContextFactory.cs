using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiRadar.Core.Data;

/// <summary>Used only by <c>dotnet ef migrations add</c>.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AiRadarDbContext>
{
    public AiRadarDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AiRadarDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
