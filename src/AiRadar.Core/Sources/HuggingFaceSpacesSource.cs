using System.Net.Http.Json;

namespace AiRadar.Core.Sources;

/// <summary>
/// Reads trending Hugging Face Spaces (AI demo apps) created after a date, through the free Hub API.
/// The caller's HttpClient must have <c>https://huggingface.co/</c> as its base address.
/// </summary>
public sealed class HuggingFaceSpacesSource(HttpClient http) : IToolSource
{
    public const string SourceName = "Hugging Face";

    private const int TrendingLimit = 100;

    public string Name => SourceName;

    public TimeSpan RefreshWindow => TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        // expand[] keeps the response small; full=true would also list every file in each Space.
        var url = $"api/spaces?sort=trendingScore&direction=-1&limit={TrendingLimit}"
            + "&expand%5B%5D=cardData&expand%5B%5D=likes&expand%5B%5D=createdAt";

        using var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ToolSourceException($"Hugging Face returned HTTP {(int)response.StatusCode}.");
        }

        var spaces = await response.Content.ReadFromJsonAsync<List<Space>>(cancellationToken)
            ?? throw new ToolSourceException("Hugging Face returned an empty response.");

        return spaces
            .Where(s => s.CreatedAt.UtcDateTime >= sinceUtc)
            .Select(s => new DiscoveredTool(
                s.Id,
                string.IsNullOrWhiteSpace(s.CardData?.Title) ? s.Id[(s.Id.IndexOf('/') + 1)..] : s.CardData.Title,
                s.CardData?.Short_description ?? "",
                null,
                $"https://huggingface.co/spaces/{s.Id}",
                s.Likes,
                s.CreatedAt.UtcDateTime))
            .ToList();
    }

    private sealed record Space(string Id, int Likes, DateTimeOffset CreatedAt, CardData? CardData);

    private sealed record CardData(string? Title, string? Short_description);
}
