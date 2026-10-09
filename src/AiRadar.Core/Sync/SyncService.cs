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

    /// <summary>Two tools with the same name count as one only if they launched this close together.</summary>
    public static readonly TimeSpan SameNameWindow = TimeSpan.FromDays(30);

    /// <summary>Shorter names ("Jet", "Kit") are too common to match on name alone.</summary>
    public const int MinimumNameKeyLength = 4;

    public async Task<SyncSummary> SyncAllAsync(CancellationToken cancellationToken)
    {
        var newToolCount = 0;
        var failures = new List<SyncFailure>();

        foreach (var source in sources)
        {
            var run = new SyncRun { Source = source.Name, StartedAtUtc = time.GetUtcNow().UtcDateTime };

            try
            {
                var since = await GetSinceAsync(source, run.StartedAtUtc, cancellationToken);
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

    private async Task<DateTime> GetSinceAsync(IToolSource source, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var lastSuccess = await db.SyncRuns
            .AsNoTracking()
            .Where(r => r.Source == source.Name && r.Succeeded)
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => (DateTime?)r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var since = lastSuccess ?? nowUtc - FirstRunWindow;
        var refreshFrom = nowUtc - source.RefreshWindow;
        return since < refreshFrom ? since : refreshFrom;
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

        var added = new List<Tool>();
        foreach (var item in latestById)
        {
            if (!existing.TryGetValue(item.ExternalId, out var tool))
            {
                tool = new Tool
                {
                    Source = sourceName,
                    ExternalId = item.ExternalId,
                    Name = item.Name,
                    Tagline = item.Tagline,
                    Url = item.Url,
                    LaunchedAtUtc = item.LaunchedAtUtc,
                    FirstSeenAtUtc = firstSeenAtUtc,
                };
                db.Tools.Add(tool);
                added.Add(tool);
            }

            tool.Name = item.Name;
            tool.Tagline = item.Tagline;
            tool.Description = item.Description;
            tool.Url = item.Url;
            tool.Score = item.Score;
            tool.NameKey = ToolKeys.ForName(item.Name);
            tool.UrlKey = ToolKeys.ForUrl(item.Url);
            tool.WebsiteKey = ToolKeys.ForUrl(item.Website);
        }

        await LinkDuplicatesAsync(sourceName, added, cancellationToken);
        return added.Count;
    }

    private async Task LinkDuplicatesAsync(string sourceName, List<Tool> added, CancellationToken cancellationToken)
    {
        if (added.Count == 0)
        {
            return;
        }

        var nameKeys = added.Select(t => t.NameKey).Where(k => k.Length >= MinimumNameKeyLength).Distinct().ToList();
        var linkKeys = added.SelectMany(t => new[] { t.UrlKey, t.WebsiteKey }).OfType<string>().Distinct().ToList();

        var candidates = await db.Tools
            .AsNoTracking()
            .Where(t => t.Source != sourceName && t.DuplicateOfId == null)
            .Where(t => nameKeys.Contains(t.NameKey)
                || (t.UrlKey != null && linkKeys.Contains(t.UrlKey))
                || (t.WebsiteKey != null && linkKeys.Contains(t.WebsiteKey)))
            .Select(t => new { t.Id, t.NameKey, t.UrlKey, t.WebsiteKey, t.LaunchedAtUtc })
            .ToListAsync(cancellationToken);

        foreach (var tool in added)
        {
            string?[] links = [tool.UrlKey, tool.WebsiteKey];
            var match = candidates.FirstOrDefault(c =>
                    links.Any(link => link is not null && (link == c.UrlKey || link == c.WebsiteKey)))
                ?? candidates.FirstOrDefault(c =>
                    tool.NameKey.Length >= MinimumNameKeyLength
                    && c.NameKey == tool.NameKey
                    && (c.LaunchedAtUtc - tool.LaunchedAtUtc).Duration() <= SameNameWindow);

            tool.DuplicateOfId = match?.Id;
        }
    }
}

public sealed record SyncSummary(int NewToolCount, IReadOnlyList<SyncFailure> Failures);

public sealed record SyncFailure(string Source, string Message);
