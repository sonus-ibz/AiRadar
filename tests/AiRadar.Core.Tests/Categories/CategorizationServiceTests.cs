using AiRadar.Core.Categories;
using AiRadar.Core.Tests.TestSupport;
using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiRadar.Core.Tests.Categories;

public sealed class CategorizationServiceTests : IDisposable
{
    private static readonly ToolFilter ProductHunt = new(new Dictionary<string, int> { ["Product Hunt"] = 10 });

    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task AddAsync(string id, string tagline, int day = 1, int score = 50, CategoryMethod? method = null, string? primary = null)
    {
        await using var db = _database.CreateContext();
        db.Tools.Add(new Tool
        {
            Source = "Product Hunt",
            ExternalId = id,
            Name = id,
            NameKey = id.ToLowerInvariant(),
            Tagline = tagline,
            Url = "https://example.com/" + id,
            Score = score,
            LaunchedAtUtc = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc),
            FirstSeenAtUtc = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
            CategoryMethod = method,
            PrimaryCategory = primary,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Tool> GetAsync(string id)
    {
        await using var db = _database.CreateContext();
        return await db.Tools.SingleAsync(t => t.ExternalId == id);
    }

    private async Task<AiRefineResult> RefineAsync(FakeChatClient chat, int max = 50)
    {
        await using var db = _database.CreateContext();
        var service = new CategorizationService(db, new AiCategoryClassifier(chat), NullLogger<CategorizationService>.Instance);
        return await service.RefineWithAiAsync(ProductHunt, max, null, CancellationToken.None);
    }

    [Fact]
    public async Task CategorizeWithKeywordsAsync_UncategorizedTools_GetKeywordCategories()
    {
        await AddAsync("clipforge", "Cuts long videos into short clips");
        await AddAsync("done", "Cuts long videos", method: CategoryMethod.Ai, primary: "Design");
        await using var db = _database.CreateContext();
        var service = new CategorizationService(db, new AiCategoryClassifier(new FakeChatClient(_ => "")), NullLogger<CategorizationService>.Instance);

        var count = await service.CategorizeWithKeywordsAsync(CancellationToken.None);

        Assert.Equal(1, count);
        var tool = await GetAsync("clipforge");
        Assert.Equal("Video", tool.PrimaryCategory);
        Assert.Equal(CategoryMethod.Keywords, tool.CategoryMethod);
        Assert.Equal("Design", (await GetAsync("done")).PrimaryCategory);
    }

    [Fact]
    public async Task ResetToKeywordsAsync_AiSortedTools_GoBackToKeywordCategories()
    {
        await AddAsync("ai", "Cuts long videos into short clips", method: CategoryMethod.Ai, primary: "Coding");
        await AddAsync("unsure", "Cuts long videos into short clips", method: CategoryMethod.KeywordsAiUnsure, primary: "Video");
        await AddAsync("keywords", "Cuts long videos", method: CategoryMethod.Keywords, primary: "Design");
        await using var db = _database.CreateContext();
        var service = new CategorizationService(db, new AiCategoryClassifier(new FakeChatClient(_ => "")), NullLogger<CategorizationService>.Instance);

        var count = await service.ResetToKeywordsAsync(CancellationToken.None);

        Assert.Equal(2, count);
        var ai = await GetAsync("ai");
        Assert.Equal(("Video", CategoryMethod.Keywords), (ai.PrimaryCategory, ai.CategoryMethod));
        Assert.Equal(CategoryMethod.Keywords, (await GetAsync("unsure")).CategoryMethod);
        Assert.Equal("Design", (await GetAsync("keywords")).PrimaryCategory);
    }

    [Fact]
    public async Task RefineWithAiAsync_VisibleKeywordTools_AreReclassifiedByAi()
    {
        await AddAsync("a", "anything", method: CategoryMethod.Keywords, primary: "Other");
        var chat = new FakeChatClient(_ => """{ "primary": "Image", "secondary": "Design" }""");

        var result = await RefineAsync(chat);

        Assert.Equal(1, result.Refined);
        var tool = await GetAsync("a");
        Assert.Equal(("Image", "Design", CategoryMethod.Ai), (tool.PrimaryCategory, tool.SecondaryCategory, tool.CategoryMethod));
    }

    [Fact]
    public async Task RefineWithAiAsync_NewestFirstAndAtMostMax()
    {
        await AddAsync("old", "x", day: 1, method: CategoryMethod.Keywords, primary: "Other");
        await AddAsync("newest", "x", day: 9, method: CategoryMethod.Keywords, primary: "Other");
        await AddAsync("middle", "x", day: 5, method: CategoryMethod.Keywords, primary: "Other");
        var chat = new FakeChatClient(_ => """{ "primary": "Coding" }""");

        await RefineAsync(chat, max: 2);

        Assert.Equal(["newest", "middle"], chat.Requests.Select(r => r[^1].Text.Split('\n')[0].Replace("Name: ", "")));
        Assert.Equal(CategoryMethod.Keywords, (await GetAsync("old")).CategoryMethod);
    }

    [Fact]
    public async Task RefineWithAiAsync_SkipsToolsHiddenByFilterAndToolsAlreadyDone()
    {
        await AddAsync("hidden", "x", score: 1, method: CategoryMethod.Keywords, primary: "Other");
        await AddAsync("done", "x", method: CategoryMethod.Ai, primary: "Video");
        await AddAsync("unsure", "x", method: CategoryMethod.KeywordsAiUnsure, primary: "Other");
        var chat = new FakeChatClient(_ => """{ "primary": "Coding" }""");

        var result = await RefineAsync(chat);

        Assert.Equal(0, result.Refined);
        Assert.Empty(chat.Requests);
    }

    [Fact]
    public async Task RefineWithAiAsync_UnusableAnswer_KeepsKeywordsAndDoesNotAskAgain()
    {
        await AddAsync("a", "x", method: CategoryMethod.Keywords, primary: "Video");
        var chat = new FakeChatClient(_ => "no idea");

        await RefineAsync(chat);
        await RefineAsync(chat);

        var tool = await GetAsync("a");
        Assert.Equal(("Video", CategoryMethod.KeywordsAiUnsure), (tool.PrimaryCategory, tool.CategoryMethod));
        Assert.Single(chat.Requests);
    }

    [Fact]
    public async Task RefineWithAiAsync_OllamaNotReachable_StopsAndReportsUnavailable()
    {
        await AddAsync("a", "x", day: 2, method: CategoryMethod.Keywords, primary: "Other");
        await AddAsync("b", "x", day: 1, method: CategoryMethod.Keywords, primary: "Other");
        var chat = new FakeChatClient(_ => "") { FailWith = new HttpRequestException("Connection refused") };

        var result = await RefineAsync(chat);

        Assert.True(result.AiUnavailable);
        Assert.Single(chat.Requests);
        Assert.Equal(CategoryMethod.Keywords, (await GetAsync("a")).CategoryMethod);
    }

    [Fact]
    public async Task RefineWithAiAsync_ReportsProgress()
    {
        await AddAsync("a", "x", method: CategoryMethod.Keywords, primary: "Other");
        await AddAsync("b", "x", method: CategoryMethod.Keywords, primary: "Other");
        var reports = new List<(int Done, int Total)>();
        await using var db = _database.CreateContext();
        var service = new CategorizationService(db, new AiCategoryClassifier(new FakeChatClient(_ => """{ "primary": "Coding" }""")), NullLogger<CategorizationService>.Instance);

        await service.RefineWithAiAsync(ProductHunt, 50, new SyncProgress<(int, int)>(reports.Add), CancellationToken.None);

        Assert.Equal([(1, 2), (2, 2)], reports);
    }

    /// <summary>Reports immediately; <see cref="Progress{T}"/> would post to a thread-pool callback.</summary>
    private sealed class SyncProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }
}
