using AiRadar.Core.Tests.TestSupport;
using AiRadar.Core.Tools;

namespace AiRadar.Core.Tests.Tools;

public sealed class ToolCatalogTests : IDisposable
{
    private static readonly ToolFilter ProductHuntOnly = new(new Dictionary<string, int> { ["Product Hunt"] = 0 });

    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Tool> AddAsync(string id, int day, string source = "Product Hunt", int score = 0, Tool? duplicateOf = null)
    {
        await using var db = _database.CreateContext();
        var tool = new Tool
        {
            Source = source,
            ExternalId = id,
            Name = "Tool " + id,
            NameKey = "tool" + id,
            Tagline = "Tagline " + id,
            Url = "https://example.com/" + id,
            Score = score,
            LaunchedAtUtc = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc),
            FirstSeenAtUtc = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            DuplicateOfId = duplicateOf?.Id,
        };
        db.Tools.Add(tool);
        await db.SaveChangesAsync();
        return tool;
    }

    private async Task<IReadOnlyList<string>> NamesAsync(ToolFilter filter, int skip = 0, int take = 50)
    {
        await using var db = _database.CreateContext();
        return (await new ToolCatalog(db).GetLatestAsync(filter, skip, take, CancellationToken.None)).Select(i => i.Name).ToList();
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsNewestLaunchFirst()
    {
        await AddAsync("old", 1);
        await AddAsync("newest", 7);
        await AddAsync("middle", 4);
        await using var db = _database.CreateContext();

        var items = await new ToolCatalog(db).GetLatestAsync(ProductHuntOnly, 0, 10, CancellationToken.None);

        Assert.Equal(["Tool newest", "Tool middle", "Tool old"], items.Select(i => i.Name));
        Assert.Equal(DateTimeKind.Utc, items[0].LaunchedAtUtc.Kind);
    }

    [Fact]
    public async Task GetLatestAsync_SkipAndTake_ReturnsRequestedPage()
    {
        await AddAsync("a", 1);
        await AddAsync("b", 2);
        await AddAsync("c", 3);
        await AddAsync("d", 4);

        Assert.Equal(["Tool c", "Tool b"], await NamesAsync(ProductHuntOnly, skip: 1, take: 2));
    }

    [Fact]
    public async Task GetLatestAsync_PerSourceMinimumScore_KeepsOnlyListedSourcesAtOrAboveTheirMinimum()
    {
        await AddAsync("ph-low", 1, "Product Hunt", score: 19);
        await AddAsync("ph-ok", 2, "Product Hunt", score: 20);
        await AddAsync("hn-ok", 3, "Hacker News", score: 10);
        await AddAsync("hn-low", 4, "Hacker News", score: 9);
        await AddAsync("gh", 5, "GitHub", score: 5000);
        var products = new ToolFilter(new Dictionary<string, int> { ["Product Hunt"] = 20, ["Hacker News"] = 10 });

        Assert.Equal(["Tool hn-ok", "Tool ph-ok"], await NamesAsync(products));
    }

    [Fact]
    public async Task GetLatestAsync_DuplicateOfToolInSameFeed_IsHidden()
    {
        var original = await AddAsync("gh", 1, "GitHub");
        await AddAsync("hn", 2, "Hacker News", duplicateOf: original);
        var both = new ToolFilter(new Dictionary<string, int> { ["GitHub"] = 0, ["Hacker News"] = 0 });

        Assert.Equal(["Tool gh"], await NamesAsync(both));
    }

    [Fact]
    public async Task GetLatestAsync_DuplicateOfToolInOtherFeed_IsShown()
    {
        var original = await AddAsync("gh", 1, "GitHub");
        await AddAsync("hn", 2, "Hacker News", duplicateOf: original);
        var products = new ToolFilter(new Dictionary<string, int> { ["Hacker News"] = 0 });

        Assert.Equal(["Tool hn"], await NamesAsync(products));
    }

    [Fact]
    public async Task GetLatestAsync_EmptyFilter_ReturnsNothing()
    {
        await AddAsync("a", 1);

        Assert.Empty(await NamesAsync(new ToolFilter(new Dictionary<string, int>())));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, ToolCatalog.MaxPageSize + 1)]
    public async Task GetLatestAsync_InvalidPage_Throws(int skip, int take)
    {
        await using var db = _database.CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ToolCatalog(db).GetLatestAsync(ProductHuntOnly, skip, take, CancellationToken.None));
    }

    [Fact]
    public async Task CountAsync_AppliesTheSameFilter()
    {
        await AddAsync("a", 1, score: 5);
        await AddAsync("b", 2, score: 50);
        await using var db = _database.CreateContext();
        var filter = new ToolFilter(new Dictionary<string, int> { ["Product Hunt"] = 20 });

        Assert.Equal(1, await new ToolCatalog(db).CountAsync(filter, CancellationToken.None));
    }
}
