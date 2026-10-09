using AiRadar.Core.Data;
using AiRadar.Core.Sources;
using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiRadar.Core.Sync;

/// <summary>
/// Pulls every source once, saves new tools and refreshes scores of known ones.
/// A failing source is recorded and skipped so the others still sync.
/// </summary>
public sealed class SyncService(
    AiRadarDbContext db,
    IEnumerable<IToolSource> sources,
    TimeProvider time,
    ILogger<SyncService> logger)
{
    public static readonly TimeSpan FirstRunWindow = TimeSpan.FromDays(7);

    public async Task<SyncSummary> SyncAllAsync(CancellationToken cancellationToken)
    {
        var newToolCount = 0;
        var failures = new List<SyncFailure>();

        foreach (var source in sources)
        {
            var run = new SyncRun { Source = source.Name, StartedAtUtc = time.GetUtcNow().UtcDateTime };

            try
            {
                var since = await GetSinceAsync(source.Name, run.StartedAtUtc, cancellationToken);
                var discovered = await source.FetchSinceAsync(since, cancellationToken);
                run.NewToolCount = await StageChangesAsync(source.Name, discovered, run.StartedAtUtc, cancellationToken);
                run.Succeeded = true;
                newToolCount += run.NewToolCount;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Sync failed for source {Source}", source.Name);
                db.ChangeTracker.Clear();
                run.Error = ex is ToolSourceException ? ex.Message : $"Could not read {source.Name}: {ex.Message}";
                failures.Add(new SyncFailure(source.Name, run.Error));
            }

            run.FinishedAtUtc = time.GetUtcNow().UtcDateTime;
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Synced {Source}: {NewToolCount} new tools", source.Name, run.NewToolCount);
        }

        return new SyncSummary(newToolCount, failures);
    }

    private async Task<DateTime> GetSinceAsync(string sourceName, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var lastSuccess = await db.SyncRuns
            .AsNoTracking()
            .Where(r => r.Source == sourceName && r.Succeeded)
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => (DateTime?)r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return lastSuccess ?? nowUtc - FirstRunWindow;
    }

    private async Task<int> StageChangesAsync(
        string sourceName,
        IReadOnlyList<DiscoveredTool> discovered,
        DateTime firstSeenAtUtc,
        CancellationToken cancellationToken)
    {
        var latestById = discovered
            .GroupBy(d => d.ExternalId)
            .Select(g => g.Last())
            .ToList();
        var ids = latestById.Select(d => d.ExternalId).ToList();

        var existing = await db.Tools
            .Where(t => t.Source == sourceName && ids.Contains(t.ExternalId))
            .ToDictionaryAsync(t => t.ExternalId, cancellationToken);

        var added = 0;
        foreach (var item in latestById)
        {
            if (existing.TryGetValue(item.ExternalId, out var tool))
            {
                tool.Name = item.Name;
                tool.Tagline = item.Tagline;
                tool.Description = item.Description;
                tool.Url = item.Url;
                tool.Score = item.Score;
                continue;
            }

            db.Tools.Add(new Tool
            {
                Source = sourceName,
                ExternalId = item.ExternalId,
                Name = item.Name,
                Tagline = item.Tagline,
                Description = item.Description,
                Url = item.Url,
                Score = item.Score,
                LaunchedAtUtc = item.LaunchedAtUtc,
                FirstSeenAtUtc = firstSeenAtUtc,
            });
            added++;
        }

        return added;
    }
}

public sealed record SyncSummary(int NewToolCount, IReadOnlyList<SyncFailure> Failures);

public sealed record SyncFailure(string Source, string Message);
