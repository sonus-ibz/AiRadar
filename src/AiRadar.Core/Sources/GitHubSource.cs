using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace AiRadar.Core.Sources;

/// <summary>
/// Reads new, already-popular AI repositories through GitHub's free search API (no token needed
/// for one search per topic). The caller's HttpClient must have <c>https://api.github.com/</c> as its
/// base address and send a User-Agent header, which GitHub requires.
/// </summary>
public sealed class GitHubSource(HttpClient http) : IToolSource
{
    public const string SourceName = "GitHub";

    /// <summary>Lower than the default display minimum, so lowering it in Settings shows more repos.</summary>
    public const int MinimumStarsToFetch = 50;

    public static readonly IReadOnlyList<string> Topics = ["llm", "ai-agents", "generative-ai", "ai"];

    private const int MaxTaglineLength = 300;

    public string Name => SourceName;

    public TimeSpan RefreshWindow => TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var createdAfter = sinceUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var tools = new List<DiscoveredTool>();

        foreach (var topic in Topics)
        {
            var query = Uri.EscapeDataString($"topic:{topic} created:>{createdAfter} stars:>={MinimumStarsToFetch}");
            using var response = await http.GetAsync($"search/repositories?q={query}&sort=stars&order=desc&per_page=100", cancellationToken);

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                throw new ToolSourceException("GitHub search rate limit reached. It resets within an hour.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ToolSourceException($"GitHub returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<SearchResult>(cancellationToken)
                ?? throw new ToolSourceException("GitHub returned an empty response.");

            tools.AddRange(result.Items.Select(ToDiscoveredTool));
        }

        return tools;
    }

    private static DiscoveredTool ToDiscoveredTool(Repository repo)
    {
        var description = repo.Description ?? "";
        return new DiscoveredTool(
            repo.Id.ToString(CultureInfo.InvariantCulture),
            repo.Name,
            description.Length > MaxTaglineLength ? description[..MaxTaglineLength].TrimEnd() + "…" : description,
            repo.Description,
            repo.Html_url,
            repo.Stargazers_count,
            repo.Created_at.UtcDateTime,
            string.IsNullOrWhiteSpace(repo.Homepage) ? null : repo.Homepage);
    }

    private sealed record SearchResult(List<Repository> Items);

    private sealed record Repository(
        long Id,
        string Name,
        string? Description,
        string Html_url,
        string? Homepage,
        int Stargazers_count,
        DateTimeOffset Created_at);
}
