using AiRadar.Core.Tests.TestSupport;
using AiRadar.Core.Tools;

namespace AiRadar.Core.Tests.Tools;

public sealed class ToolCatalogTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync(params (string Id, int Day)[] tools)
    {
        await using var db = _database.CreateContext();
        db.Tools.AddRange(tools.Select(t => new Tool
        {
            Source = "Product Hunt",
            ExternalId = t.Id,
            Name = "Tool " + t.Id,
            Tagline = "Tagline " + t.Id,
            Url = "https://example.com/" + t.Id,
            LaunchedAtUtc = new DateTime(2026, 10, t.Day, 0, 0, 0, DateTimeKind.Utc),
            FirstSeenAtUtc = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
        }));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsNewestLaunchFirst()
    {
        await SeedAsync(("old", 1), ("newest", 7), ("middle", 4));
        await using var db = _database.CreateContext();

        var items = await new ToolCatalog(db).GetLatestAsync(0, 10, CancellationToken.None);

        Assert.Equal(["Tool newest", "Tool middle", "Tool old"], items.Select(i => i.Name));
        Assert.Equal(DateTimeKind.Utc, items[0].LaunchedAtUtc.Kind);
    }

    [Fact]
    public async Task GetLatestAsync_SkipAndTake_ReturnsRequestedPage()
    {
        await SeedAsync(("a", 1), ("b", 2), ("c", 3), ("d", 4));
        await using var db = _database.CreateContext();

        var items = await new ToolCatalog(db).GetLatestAsync(1, 2, CancellationToken.None);

        Assert.Equal(["Tool c", "Tool b"], items.Select(i => i.Name));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, ToolCatalog.MaxPageSize + 1)]
    public async Task GetLatestAsync_InvalidPage_Throws(int skip, int take)
    {
        await using var db = _database.CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ToolCatalog(db).GetLatestAsync(skip, take, CancellationToken.None));
    }

    [Fact]
    public async Task CountAsync_ReturnsNumberOfSavedTools()
    {
        await SeedAsync(("a", 1), ("b", 2));
        await using var db = _database.CreateContext();

        Assert.Equal(2, await new ToolCatalog(db).CountAsync(CancellationToken.None));
    }
}
