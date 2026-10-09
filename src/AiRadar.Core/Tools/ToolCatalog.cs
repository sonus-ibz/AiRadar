using AiRadar.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AiRadar.Core.Tools;

/// <summary>Read-only queries the screens use.</summary>
public sealed class ToolCatalog(AiRadarDbContext db)
{
    public const int MaxPageSize = 200;

    public async Task<IReadOnlyList<ToolListItem>> GetLatestAsync(int skip, int take, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaxPageSize);

        return await db.Tools
            .AsNoTracking()
            .OrderByDescending(t => t.LaunchedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(take)
            .Select(t => new ToolListItem(t.Name, t.Tagline, t.Url, t.Source, t.Score, t.LaunchedAtUtc, t.FirstSeenAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        db.Tools.CountAsync(cancellationToken);
}

public sealed record ToolListItem(
    string Name,
    string Tagline,
    string Url,
    string Source,
    int Score,
    DateTime LaunchedAtUtc,
    DateTime FirstSeenAtUtc);
