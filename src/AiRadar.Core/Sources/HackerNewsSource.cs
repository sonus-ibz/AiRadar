using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AiRadar.Core.Sources;

/// <summary>
/// Reads "Show HN" launches about AI through the free Hacker News search API by Algolia.
/// The caller's HttpClient must have <c>https://hn.algolia.com/</c> as its base address.
/// </summary>
public sealed partial class HackerNewsSource(HttpClient http) : IToolSource
{
    public const string SourceName = "Hacker News";
    public const int MaxPages = 10;

    private const int PageSize = 100;
    private const string ShowHnPrefix = "Show HN:";

    public string Name => SourceName;

    public TimeSpan RefreshWindow => TimeSpan.FromDays(2);

    public async Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var since = new DateTimeOffset(sinceUtc).ToUnixTimeSeconds();
        var tools = new List<DiscoveredTool>();

        for (var page = 0; page < MaxPages; page++)
        {
            var url = $"api/v1/search_by_date?tags=show_hn&numericFilters=created_at_i%3E{since}&hitsPerPage={PageSize}&page={page}";
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ToolSourceException($"Hacker News returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<SearchResult>(cancellationToken)
                ?? throw new ToolSourceException("Hacker News returned an empty response.");

            tools.AddRange(result.Hits.Where(h => AiKeywords().IsMatch(h.Title ?? "")).Select(ToDiscoveredTool));

            if (page >= result.NbPages - 1)
            {
                break;
            }
        }

        return tools;
    }

    internal static (string Name, string Tagline) SplitTitle(string title)
    {
        var text = title.StartsWith(ShowHnPrefix, StringComparison.OrdinalIgnoreCase)
            ? title[ShowHnPrefix.Length..].Trim()
            : title.Trim();

        var separator = TitleSeparator().Match(text);
        return separator.Success
            ? (text[..separator.Index].Trim(), text[(separator.Index + separator.Length)..].Trim())
            : (text, "");
    }

    private static DiscoveredTool ToDiscoveredTool(Hit hit)
    {
        var (name, tagline) = SplitTitle(hit.Title ?? "");
        return new DiscoveredTool(
            hit.ObjectID,
            name,
            tagline,
            null,
            string.IsNullOrWhiteSpace(hit.Url) ? $"https://news.ycombinator.com/item?id={hit.ObjectID}" : hit.Url,
            hit.Points ?? 0,
            DateTimeOffset.FromUnixTimeSeconds(hit.Created_at_i).UtcDateTime);
    }

    [GeneratedRegex(@"\s+[–—-]\s+|:\s+|,\s+")]
    private static partial Regex TitleSeparator();

    [GeneratedRegex(
        @"\b(ai|a\.i\.|genai|llms?|gpts?|chatgpt|agents?|agentic|claude|gemini|openai|anthropic|ollama|mcp|rag|copilot|prompts?|chatbots?|diffusion|transformers?|embeddings?|neural|machine learning|deep learning|language models?|whisper|tts|speech[- ]to[- ]text)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AiKeywords();

    private sealed record SearchResult(List<Hit> Hits, int NbPages);

    private sealed record Hit(string ObjectID, string? Title, string? Url, int? Points, long Created_at_i);
}
