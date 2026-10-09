using System.Net;
using AiRadar.Core.Sources;
using AiRadar.Core.Tests.TestSupport;

namespace AiRadar.Core.Tests.Sources;

public sealed class HackerNewsSourceTests
{
    private static readonly DateTime Since = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    private static (HackerNewsSource Source, StubHttpHandler Handler) Create()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hn.algolia.com/") };
        return (new HackerNewsSource(http), handler);
    }

    private static string Page(int page, int nbPages, string hitsJson = "") =>
        $$"""{ "hits": [{{hitsJson}}], "page": {{page}}, "nbPages": {{nbPages}} }""";

    [Fact]
    public async Task FetchSinceAsync_RecordedResponse_KeepsOnlyAiPosts()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Fixture.Read("hn-show.json"));

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(["50015061", "50014903", "50014659"], tools.Select(t => t.ExternalId));
    }

    [Fact]
    public async Task FetchSinceAsync_RecordedResponse_SplitsShowHnTitleIntoNameAndTagline()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Fixture.Read("hn-show.json"));

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        var moching = tools.Single(t => t.ExternalId == "50015061");
        Assert.Equal("Moching", moching.Name);
        Assert.Equal("AI desktop agent with 219 built-in tools (Rust)", moching.Tagline);
        Assert.Equal("https://github.com/moching-ai-dev/moching", moching.Url);
        Assert.Equal(1, moching.Score);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791511221).UtcDateTime, moching.LaunchedAtUtc);
        Assert.Equal(DateTimeKind.Utc, moching.LaunchedAtUtc.Kind);
    }

    [Theory]
    [InlineData("Show HN: Moching – AI desktop agent", "Moching", "AI desktop agent")]
    [InlineData("Show HN: Nace.ai Document Intelligence 1.0", "Nace.ai Document Intelligence 1.0", "")]
    [InlineData("Show HN: Auro, a package manager", "Auro", "a package manager")]
    [InlineData("Show HN: Tiny - LLM in 100 lines", "Tiny", "LLM in 100 lines")]
    [InlineData("Show HN: Kit: agents for your terminal", "Kit", "agents for your terminal")]
    public void SplitTitle_SeparatesNameFromTagline(string title, string name, string tagline)
    {
        Assert.Equal((name, tagline), HackerNewsSource.SplitTitle(title));
    }

    [Fact]
    public async Task FetchSinceAsync_PostWithoutLink_UsesHackerNewsDiscussionUrl()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Page(0, 1, """
            { "objectID": "777", "title": "Show HN: Tiny – LLM in 100 lines", "url": null, "points": 12, "created_at_i": 1791500000 }
            """));

        var tool = Assert.Single(await source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Equal("https://news.ycombinator.com/item?id=777", tool.Url);
    }

    [Fact]
    public async Task FetchSinceAsync_AsksForShowHnPostsCreatedAfterSince()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Page(0, 1));

        await source.FetchSinceAsync(Since, CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.RequestUris[0].Query);
        Assert.Contains("tags=show_hn", query);
        Assert.Contains($"created_at_i>{new DateTimeOffset(Since).ToUnixTimeSeconds()}", query);
        Assert.Contains("page=0", query);
    }

    [Fact]
    public async Task FetchSinceAsync_SeveralPages_ReadsUntilLastPage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson(Page(0, 2)).RespondWithJson(Page(1, 2));

        await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains("page=1", handler.RequestUris[1].Query);
    }

    [Fact]
    public async Task FetchSinceAsync_ServerError_ThrowsReadableMessage()
    {
        var (source, handler) = Create();
        handler.RespondWithJson("{}", HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<ToolSourceException>(() => source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Equal("Hacker News returned HTTP 503.", ex.Message);
    }
}
