using AiRadar.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AiRadar.Core.Tools;

/// <summary>Read-only queries the screens use.</summary>
public sealed class ToolCatalog(AiRadarDbContext db)
{
    public const int MaxPageSize = 200;

    public async Task<IReadOnlyList<ToolListItem>> GetLatestAsync(
        ToolFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaxPageSize);

        return await db.Tools
            .AsNoTracking()
            .Visible(filter)
            .OrderByDescending(t => t.LaunchedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(take)
            .Select(t => new ToolListItem(
                t.Name, t.Tagline, t.Url, t.Source, t.Score, t.LaunchedAtUtc, t.FirstSeenAtUtc, t.PrimaryCategory, t.SecondaryCategory))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ToolFilter filter, CancellationToken cancellationToken) =>
        db.Tools.AsNoTracking().Visible(filter).CountAsync(cancellationToken);

    /// <summary>How many visible tools are good at each category; a tool counts once for each of its two categories.</summary>
    public async Task<IReadOnlyDictionary<string, int>> CountByCategoryAsync(ToolFilter filter, CancellationToken cancellationToken)
    {
        var visible = db.Tools.AsNoTracking().Visible(filter with { Category = null });

        var primary = await visible
            .Where(t => t.PrimaryCategory != null)
            .GroupBy(t => t.PrimaryCategory!)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var secondary = await visible
            .Where(t => t.SecondaryCategory != null)
            .GroupBy(t => t.SecondaryCategory!)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return primary.Concat(secondary)
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
    }
}

/// <summary>The sources to show, each with the minimum score a tool needs to appear, and optionally one category.</summary>
public sealed record ToolFilter(IReadOnlyDictionary<string, int> MinScoreBySource, string? Category = null);

public sealed record ToolListItem(
    string Name,
    string Tagline,
    string Url,
    string Source,
    int Score,
    DateTime LaunchedAtUtc,
    DateTime FirstSeenAtUtc,
    string? PrimaryCategory,
    string? SecondaryCategory);
