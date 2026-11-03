using AiRadar.Core.Data;
using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiRadar.Core.Categories;

/// <summary>
/// Gives every tool a "good at" category: keywords straight away, then the local AI for the tools
/// people actually see, newest first.
/// </summary>
public sealed class CategorizationService(
    AiRadarDbContext db,
    AiCategoryClassifier ai,
    ILogger<CategorizationService> logger)
{
    private const int SaveEvery = 10;

    public Task<int> CategorizeWithKeywordsAsync(CancellationToken cancellationToken) =>
        ApplyKeywordsAsync(db.Tools.Where(t => t.CategoryMethod == null), cancellationToken);

    /// <summary>Puts every AI-sorted tool back on keyword categories, used when local AI sorting is turned off.</summary>
    public Task<int> ResetToKeywordsAsync(CancellationToken cancellationToken) =>
        ApplyKeywordsAsync(
            db.Tools.Where(t => t.CategoryMethod == CategoryMethod.Ai || t.CategoryMethod == CategoryMethod.KeywordsAiUnsure),
            cancellationToken);

    private async Task<int> ApplyKeywordsAsync(IQueryable<Tool> query, CancellationToken cancellationToken)
    {
        var tools = await query.ToListAsync(cancellationToken);

        foreach (var tool in tools)
        {
            var result = KeywordClassifier.Classify(tool.Name, tool.Tagline, tool.Description);
            tool.PrimaryCategory = result.Primary;
            tool.SecondaryCategory = result.Secondary;
            tool.CategoryMethod = CategoryMethod.Keywords;
        }

        await db.SaveChangesAsync(cancellationToken);
        return tools.Count;
    }

    /// <summary>
    /// Re-sorts up to <paramref name="max"/> visible keyword-sorted tools with the AI. Progress is saved
    /// every few tools, so closing the app keeps the work done so far.
    /// </summary>
    public async Task<AiRefineResult> RefineWithAiAsync(
        ToolFilter filter,
        int max,
        IProgress<(int Done, int Total)>? progress,
        CancellationToken cancellationToken)
    {
        var ids = await db.Tools
            .AsNoTracking()
            .Visible(filter with { Category = null })
            .Where(t => t.CategoryMethod == CategoryMethod.Keywords)
            .OrderByDescending(t => t.LaunchedAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => t.Id)
            .Take(max)
            .ToListAsync(cancellationToken);

        var refined = 0;
        for (var i = 0; i < ids.Count; i++)
        {
            var tool = await db.Tools.SingleAsync(t => t.Id == ids[i], cancellationToken);
            Classification? result;
            try
            {
                result = await ai.ClassifyAsync(tool.Name, tool.Tagline, tool.Description, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Local AI not reachable; keeping keyword categories");
                await db.SaveChangesAsync(cancellationToken);
                return new AiRefineResult(refined, AiUnavailable: true);
            }

            if (result is null)
            {
                tool.CategoryMethod = CategoryMethod.KeywordsAiUnsure;
            }
            else
            {
                tool.PrimaryCategory = result.Primary;
                tool.SecondaryCategory = result.Secondary;
                tool.CategoryMethod = CategoryMethod.Ai;
                refined++;
            }

            if ((i + 1) % SaveEvery == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            progress?.Report((i + 1, ids.Count));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AI sorted {RefinedCount} of {CandidateCount} tools", refined, ids.Count);
        return new AiRefineResult(refined, AiUnavailable: false);
    }
}

public sealed record AiRefineResult(int Refined, bool AiUnavailable);
