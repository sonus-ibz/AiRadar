using System.Net;
using System.Text.Json;
using AiRadar.Core.Sources;
using AiRadar.Core.Tests.TestSupport;

namespace AiRadar.Core.Tests.Sources;

public sealed class ProductHuntSourceTests
{
    private static readonly DateTime Since = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (ProductHuntSource Source, StubHttpHandler Handler) Create()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.producthunt.com/") };
        return (new ProductHuntSource(http), handler);
    }

    [Fact]
    public async Task FetchSinceAsync_SinglePage_MapsEveryPost()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(ProductHuntFixtures.SecondPage);

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal("1003", tool.ExternalId);
        Assert.Equal("VoiceNote AI", tool.Name);
        Assert.Equal("Records calls and writes searchable summaries", tool.Tagline);
        Assert.Equal("Summaries in 20 languages.", tool.Description);
        Assert.Equal("https://www.producthunt.com/posts/voicenote-ai", tool.Url);
        Assert.Equal(98, tool.Score);
        Assert.Equal(new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc), tool.LaunchedAtUtc);
        Assert.Equal(DateTimeKind.Utc, tool.LaunchedAtUtc.Kind);
    }

    [Fact]
    public async Task FetchSinceAsync_OffsetTimestamp_ConvertsToUtc()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(ProductHuntFixtures.FirstPage).RespondWithJson(ProductHuntFixtures.Empty);

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        var mosaic = Assert.Single(tools, t => t.ExternalId == "1002");
        Assert.Equal(new DateTime(2026, 10, 6, 16, 45, 30, DateTimeKind.Utc), mosaic.LaunchedAtUtc);
        Assert.Null(mosaic.Description);
    }

    [Fact]
    public async Task FetchSinceAsync_HasNextPage_FollowsCursorUntilLastPage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(ProductHuntFixtures.FirstPage).RespondWithJson(ProductHuntFixtures.SecondPage);

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(["1001", "1002", "1003"], tools.Select(t => t.ExternalId));
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Equal(JsonValueKind.Null, Variables(handler.RequestBodies[0]).GetProperty("after").ValueKind);
        Assert.Equal("cursor-page-2", Variables(handler.RequestBodies[1]).GetProperty("after").GetString());
    }

    [Fact]
    public async Task FetchSinceAsync_SendsAiTopicAndPostedAfterAsUtcIso8601()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(ProductHuntFixtures.Empty);

        await source.FetchSinceAsync(Since, CancellationToken.None);

        var variables = Variables(handler.RequestBodies[0]);
        Assert.Equal("artificial-intelligence", variables.GetProperty("topic").GetString());
        Assert.Equal("2026-10-01T00:00:00Z", variables.GetProperty("postedAfter").GetString());
    }

    [Fact]
    public async Task FetchSinceAsync_EveryPageHasNextPage_StopsAtMaxPages()
    {
        var (source, handler) = Create();
        for (var i = 0; i < ProductHuntSource.MaxPages + 5; i++)
        {
            handler.RespondWithJson(ProductHuntFixtures.FirstPage);
        }

        await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(ProductHuntSource.MaxPages, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task FetchSinceAsync_GraphQlErrors_ThrowsWithApiMessage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(ProductHuntFixtures.GraphQlError);

        var ex = await Assert.ThrowsAsync<ToolSourceException>(() => source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Contains("invalid value", ex.Message);
    }

    [Fact]
    public async Task FetchSinceAsync_Unauthorized_ThrowsTokenMessage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson("""{ "error": "invalid_oauth_token" }""", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<ToolSourceException>(() => source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Contains("token", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement Variables(string requestBody) =>
        JsonDocument.Parse(requestBody).RootElement.GetProperty("variables");
}
