using System.Linq.Expressions;

namespace AiRadar.Core.Tools;

internal static class ToolQueries
{
    /// <summary>
    /// The tools a screen shows for <paramref name="filter"/>: listed sources at or above their minimum
    /// score, optionally in one category, and a tool found earlier on another source in the same view
    /// shows once, under that source.
    /// </summary>
    public static IQueryable<Tool> Visible(this IQueryable<Tool> tools, ToolFilter filter)
    {
        var sources = filter.MinScoreBySource.Keys.ToList();
        var visible = tools
            .Where(MeetsMinimumScore(filter))
            .Where(t => t.DuplicateOfId == null || !sources.Contains(t.DuplicateOf!.Source));

        return filter.Category is null
            ? visible
            : visible.Where(t => t.PrimaryCategory == filter.Category || t.SecondaryCategory == filter.Category);
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
