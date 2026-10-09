using System.Net;
using AiRadar.Core.Sources;
using AiRadar.Core.Tests.TestSupport;

namespace AiRadar.Core.Tests.Sources;

public sealed class HuggingFaceSpacesSourceTests
{
    private static (HuggingFaceSpacesSource Source, StubHttpHandler Handler) Create()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://huggingface.co/") };
        return (new HuggingFaceSpacesSource(http), handler);
    }

    [Fact]
    public async Task FetchSinceAsync_RecordedResponse_KeepsSpacesCreatedAfterSince()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Fixture.Read("hf-spaces.json"));

        var tools = await source.FetchSinceAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(["multimodalart/jev-decision-index", "arudradey/qwen-image-2.1-uncensored-gguf"], tools.Select(t => t.ExternalId));
    }

    [Fact]
    public async Task FetchSinceAsync_RecordedResponse_MapsCardTitleDescriptionLikesAndUrl()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Fixture.Read("hf-spaces.json"));

        var tools = await source.FetchSinceAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var space = tools.Single(t => t.ExternalId == "multimodalart/jev-decision-index");
        Assert.Equal("Jev Decision Index", space.Name);
        Assert.Equal("Benchmarks and news on various repros of TypeSafe's Jev", space.Tagline);
        Assert.Equal("https://huggingface.co/spaces/multimodalart/jev-decision-index", space.Url);
        Assert.Equal(537, space.Score);
        Assert.Equal(new DateTime(2026, 9, 17, 20, 5, 16, DateTimeKind.Utc), space.LaunchedAtUtc);
    }

    [Fact]
    public async Task FetchSinceAsync_SpaceWithoutCard_FallsBackToSpaceName()
    {
        var (source, handler) = Create();
        handler.RespondWithJson("""
            [ { "id": "someone/cool-demo", "likes": 70, "createdAt": "2026-10-01T00:00:00.000Z" } ]
            """);

        var tool = Assert.Single(await source.FetchSinceAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None));

        Assert.Equal("cool-demo", tool.Name);
        Assert.Equal("", tool.Tagline);
    }

    [Fact]
    public async Task FetchSinceAsync_AsksForTrendingSpacesWithLightFields()
    {
        var (source, handler) = Create();
        handler.RespondWithJson("[]");

        await source.FetchSinceAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.RequestUris[0].Query);
        Assert.Contains("sort=trendingScore", query);
        Assert.Contains("expand[]=cardData", query);
        Assert.DoesNotContain("full=", query);
    }

    [Fact]
    public async Task FetchSinceAsync_RateLimited_ThrowsReadableMessage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson("{}", HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<ToolSourceException>(() =>
            source.FetchSinceAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None));

        Assert.Equal("Hugging Face returned HTTP 429.", ex.Message);
    }
}
