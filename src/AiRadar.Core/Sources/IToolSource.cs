namespace AiRadar.Core.Sources;

public interface IToolSource
{
    string Name { get; }

    /// <summary>
    /// How far back every check re-reads, so scores of recent tools keep growing and tools that
    /// cross the score threshold late are still picked up.
    /// </summary>
    TimeSpan RefreshWindow { get; }

    Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken);
}

/// <param name="Website">The product's own site when it differs from <paramref name="Url"/>; used to spot the same tool on other sources.</param>
public sealed record DiscoveredTool(
    string ExternalId,
    string Name,
    string Tagline,
    string? Description,
    string Url,
    int Score,
    DateTime LaunchedAtUtc,
    string? Website = null);
