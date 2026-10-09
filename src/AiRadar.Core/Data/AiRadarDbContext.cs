using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace AiRadar.Core.Data;

public sealed class AiRadarDbContext(DbContextOptions<AiRadarDbContext> options) : DbContext(options)
{
    public DbSet<Tool> Tools => Set<Tool>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no time-zone type; every DateTime in this app is UTC, so mark it on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tool>(tool =>
        {
            tool.HasIndex(t => new { t.Source, t.ExternalId }).IsUnique();
            tool.HasIndex(t => t.LaunchedAtUtc);
            tool.Property(t => t.Source).HasMaxLength(50);
            tool.Property(t => t.ExternalId).HasMaxLength(200);
            tool.Property(t => t.Name).HasMaxLength(300);
            tool.Property(t => t.Tagline).HasMaxLength(500);
            tool.Property(t => t.Url).HasMaxLength(2000);
        });

        modelBuilder.Entity<SyncRun>(run =>
        {
            run.HasIndex(r => new { r.Source, r.StartedAtUtc });
            run.Property(r => r.Source).HasMaxLength(50);
            run.Property(r => r.Error).HasMaxLength(2000);
        });
    }
}
