using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace AiRadar.Core.Sources;

/// <summary>
/// Reads AI launches from the Product Hunt API v2 (GraphQL). The caller's HttpClient must have
/// <c>https://api.producthunt.com/</c> as its base address and send the user's developer token.
/// </summary>
public sealed class ProductHuntSource(HttpClient http) : IToolSource
{
    public const string SourceName = "Product Hunt";
    public const int MaxPages = 10;

    private const string Topic = "artificial-intelligence";
    private const int PageSize = 20;
    private const string PostsQuery = """
        query AiPosts($topic: String!, $postedAfter: DateTime!, $after: String, $first: Int!) {
          posts(topic: $topic, postedAfter: $postedAfter, order: NEWEST, first: $first, after: $after) {
            edges { node { id name tagline description url votesCount createdAt } }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;

    public string Name => SourceName;

    public async Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        if (sinceUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be in UTC.", nameof(sinceUtc));
        }

        var postedAfter = sinceUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var tools = new List<DiscoveredTool>();
        string? cursor = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var posts = await QueryAsync(new Variables(Topic, postedAfter, cursor, PageSize), cancellationToken);
            tools.AddRange(posts.Edges.Select(edge => ToDiscoveredTool(edge.Node)));

            if (!posts.PageInfo.HasNextPage)
            {
                break;
            }

            cursor = posts.PageInfo.EndCursor;
        }

        return tools;
    }

    private async Task<PostConnection> QueryAsync(Variables variables, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("v2/api/graphql", new GraphQlRequest(PostsQuery, variables), cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ToolSourceException("Product Hunt rejected the API token. Check the token in Settings.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ToolSourceException($"Product Hunt returned HTTP {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<GraphQlResponse>(cancellationToken);

        if (body?.Errors is { Count: > 0 } errors)
        {
            throw new ToolSourceException("Product Hunt API error: " + errors[0].Message);
        }

        return body?.Data?.Posts ?? throw new ToolSourceException("Product Hunt returned an empty response.");
    }

    private static DiscoveredTool ToDiscoveredTool(Post post) => new(
        post.Id,
        post.Name ?? "",
        post.Tagline ?? "",
        post.Description,
        post.Url ?? "",
        post.VotesCount,
        post.CreatedAt.UtcDateTime);

    private sealed record GraphQlRequest(string Query, Variables Variables);

    private sealed record Variables(string Topic, string PostedAfter, string? After, int First);

    private sealed record GraphQlResponse(ResponseData? Data, List<GraphQlError>? Errors);

    private sealed record GraphQlError(string Message);

    private sealed record ResponseData(PostConnection? Posts);

    private sealed record PostConnection(List<PostEdge> Edges, PageInfo PageInfo);

    private sealed record PostEdge(Post Node);

    private sealed record PageInfo(bool HasNextPage, string? EndCursor);

    private sealed record Post(
        string Id,
        string? Name,
        string? Tagline,
        string? Description,
        string? Url,
        int VotesCount,
        DateTimeOffset CreatedAt);
}
