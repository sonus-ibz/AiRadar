using System.Net;
using AiRadar.Core.Sources;
using AiRadar.Core.Tests.TestSupport;

namespace AiRadar.Core.Tests.Sources;

public sealed class GitHubSourceTests
{
    private const string EmptySearch = """{ "total_count": 0, "incomplete_results": false, "items": [] }""";
    private static readonly DateTime Since = new(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

    private static (GitHubSource Source, StubHttpHandler Handler) Create()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return (new GitHubSource(http), handler);
    }

    private static StubHttpHandler RespondToEveryTopic(StubHttpHandler handler, string firstTopicJson)
    {
        handler.RespondWithJson(firstTopicJson);
        for (var i = 1; i < GitHubSource.Topics.Count; i++)
        {
            handler.RespondWithJson(EmptySearch);
        }

        return handler;
    }

    [Fact]
    public async Task FetchSinceAsync_RecordedResponse_MapsRepositories()
    {
        var (source, handler) = Create();
        RespondToEveryTopic(handler, Fixture.Read("gh-search.json"));

        var tools = await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(3, tools.Count);
        var magpie = tools.Single(t => t.ExternalId == "1383809012");
        Assert.Equal("magpie", magpie.Name);
        Assert.Equal("Every agent's model. One place. Codex on DeepSeek, Claude Code on Kimi, from the menu bar.", magpie.Tagline);
        Assert.Equal("https://github.com/yetone/magpie", magpie.Url);
        Assert.Equal("https://usemagpie.ai", magpie.Website);
        Assert.Equal(6815, magpie.Score);
        Assert.Equal(new DateTime(2026, 9, 23, 16, 22, 35, DateTimeKind.Utc), magpie.LaunchedAtUtc);
    }

    [Fact]
    public async Task FetchSinceAsync_SearchesEachAiTopicForReposCreatedAfterSince()
    {
        var (source, handler) = Create();
        RespondToEveryTopic(handler, EmptySearch);

        await source.FetchSinceAsync(Since, CancellationToken.None);

        Assert.Equal(GitHubSource.Topics.Count, handler.RequestUris.Count);
        var firstQuery = Uri.UnescapeDataString(handler.RequestUris[0].Query);
        Assert.Contains($"topic:{GitHubSource.Topics[0]}", firstQuery);
        Assert.Contains("created:>2026-09-09", firstQuery);
        Assert.Contains($"stars:>={GitHubSource.MinimumStarsToFetch}", firstQuery);
        Assert.Contains("sort=stars", firstQuery);
    }

    [Fact]
    public async Task FetchSinceAsync_EmptyHomepage_LeavesWebsiteEmpty()
    {
        var (source, handler) = Create();
        RespondToEveryTopic(handler, """
            { "items": [ { "id": 5, "name": "x", "full_name": "a/x", "description": null, "html_url": "https://github.com/a/x",
                           "homepage": "", "stargazers_count": 300, "created_at": "2026-10-01T00:00:00Z" } ] }
            """);

        var tool = Assert.Single(await source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Null(tool.Website);
        Assert.Equal("", tool.Tagline);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task FetchSinceAsync_RateLimited_ThrowsRateLimitMessage(HttpStatusCode status)
    {
        var (source, handler) = Create();
        handler.RespondWithJson("""{ "message": "API rate limit exceeded" }""", status);

        var ex = await Assert.ThrowsAsync<ToolSourceException>(() => source.FetchSinceAsync(Since, CancellationToken.None));

        Assert.Contains("rate limit", ex.Message);
    }
}
