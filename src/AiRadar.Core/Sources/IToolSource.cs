namespace AiRadar.Core.Sources;

public interface IToolSource
{
    string Name { get; }

    Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken);
}

public sealed record DiscoveredTool(
    string ExternalId,
    string Name,
    string Tagline,
    string? Description,
    string Url,
    int Score,
    DateTime LaunchedAtUtc);
