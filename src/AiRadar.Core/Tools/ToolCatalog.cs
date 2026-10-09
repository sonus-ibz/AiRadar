using System.Linq.Expressions;
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

        return await Filtered(filter)
            .OrderByDescending(t => t.LaunchedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(take)
            .Select(t => new ToolListItem(t.Name, t.Tagline, t.Url, t.Source, t.Score, t.LaunchedAtUtc, t.FirstSeenAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ToolFilter filter, CancellationToken cancellationToken) =>
        Filtered(filter).CountAsync(cancellationToken);

    private IQueryable<Tool> Filtered(ToolFilter filter)
    {
        var sources = filter.MinScoreBySource.Keys.ToList();

        // A tool also found earlier on another source in the same view shows once, under that source.
        return db.Tools
            .AsNoTracking()
            .Where(MeetsMinimumScore(filter))
            .Where(t => t.DuplicateOfId == null || !sources.Contains(t.DuplicateOf!.Source));
    }

    private static Expression<Func<Tool, bool>> MeetsMinimumScore(ToolFilter filter)
    {
        var tool = Expression.Parameter(typeof(Tool), "t");
        Expression body = Expression.Constant(false);

        foreach (var (source, minimum) in filter.MinScoreBySource)
        {
            var sourceMatches = Expression.Equal(Expression.Property(tool, nameof(Tool.Source)), Expression.Constant(source));
            var scoreHighEnough = Expression.GreaterThanOrEqual(Expression.Property(tool, nameof(Tool.Score)), Expression.Constant(minimum));
            body = Expression.OrElse(body, Expression.AndAlso(sourceMatches, scoreHighEnough));
        }

        return Expression.Lambda<Func<Tool, bool>>(body, tool);
    }
}

/// <summary>The sources to show, each with the minimum score a tool needs to appear.</summary>
public sealed record ToolFilter(IReadOnlyDictionary<string, int> MinScoreBySource);

public sealed record ToolListItem(
    string Name,
    string Tagline,
    string Url,
    string Source,
    int Score,
    DateTime LaunchedAtUtc,
    DateTime FirstSeenAtUtc);
