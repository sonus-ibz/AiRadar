using AiRadar.Core.Sources;
using AiRadar.Core.Sync;
using AiRadar.Core.Tests.TestSupport;
using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AiRadar.Core.Tests.Sync;

public sealed class SyncServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _time = new(Now);

    public void Dispose() => _database.Dispose();

    private async Task<SyncSummary> SyncAsync(params IToolSource[] sources)
    {
        await using var db = _database.CreateContext();
        var service = new SyncService(db, sources, _time, NullLogger<SyncService>.Instance);
        return await service.SyncAllAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SyncAllAsync_FirstRun_FetchesLastSevenDays()
    {
        var source = new FakeToolSource("A");

        await SyncAsync(source);

        Assert.Equal(Now.UtcDateTime - SyncService.FirstRunWindow, Assert.Single(source.RequestedSince));
    }

    [Fact]
    public async Task SyncAllAsync_NewTools_SavesThemWithFirstSeenTimeAndCountsThem()
    {
        var source = new FakeToolSource("A");
        source.Tools.AddRange([FakeToolSource.Tool("1"), FakeToolSource.Tool("2")]);

        var summary = await SyncAsync(source);

        Assert.Equal(2, summary.NewToolCount);
        Assert.Empty(summary.Failures);
        await using var db = _database.CreateContext();
        var saved = await db.Tools.OrderBy(t => t.ExternalId).ToListAsync();
        Assert.Equal(["1", "2"], saved.Select(t => t.ExternalId));
        Assert.All(saved, t => Assert.Equal("A", t.Source));
        Assert.All(saved, t => Assert.Equal(Now.UtcDateTime, t.FirstSeenAtUtc));
    }

    [Fact]
    public async Task SyncAllAsync_ToolAlreadySaved_UpdatesScoreWithoutDuplicateOrNewCount()
    {
        var source = new FakeToolSource("A");
        source.Tools.Add(FakeToolSource.Tool("1", score: 10));
        await SyncAsync(source);
        _time.Advance(TimeSpan.FromHours(3));
        source.Tools[0] = FakeToolSource.Tool("1", score: 250);

        var summary = await SyncAsync(source);

        Assert.Equal(0, summary.NewToolCount);
        await using var db = _database.CreateContext();
        var tool = Assert.Single(await db.Tools.ToListAsync());
        Assert.Equal(250, tool.Score);
        Assert.Equal(Now.UtcDateTime, tool.FirstSeenAtUtc);
    }

    [Fact]
    public async Task SyncAllAsync_SameIdFromDifferentSources_KeepsBoth()
    {
        var a = new FakeToolSource("A");
        var b = new FakeToolSource("B");
        a.Tools.Add(FakeToolSource.Tool("1"));
        b.Tools.Add(FakeToolSource.Tool("1"));

        var summary = await SyncAsync(a, b);

        Assert.Equal(2, summary.NewToolCount);
    }

    [Fact]
    public async Task SyncAllAsync_AfterSuccessfulRun_FetchesSinceThatRunStarted()
    {
        var source = new FakeToolSource("A");
        await SyncAsync(source);
        _time.Advance(TimeSpan.FromHours(5));

        await SyncAsync(source);

        Assert.Equal(Now.UtcDateTime, source.RequestedSince[1]);
    }

    [Fact]
    public async Task SyncAllAsync_AfterFailedRun_StillFetchesSinceLastSuccessfulRun()
    {
        var source = new FakeToolSource("A");
        await SyncAsync(source);
        _time.Advance(TimeSpan.FromHours(1));
        source.FailWith = new ToolSourceException("down");
        await SyncAsync(source);
        _time.Advance(TimeSpan.FromHours(1));
        source.FailWith = null;

        await SyncAsync(source);

        Assert.Equal(Now.UtcDateTime, source.RequestedSince[2]);
    }

    [Fact]
    public async Task SyncAllAsync_OneSourceFails_RecordsFailureAndStillSyncsTheOthers()
    {
        var broken = new FakeToolSource("Broken") { FailWith = new ToolSourceException("Product Hunt rejected the token.") };
        var working = new FakeToolSource("Working");
        working.Tools.Add(FakeToolSource.Tool("1"));

        var summary = await SyncAsync(broken, working);

        var failure = Assert.Single(summary.Failures);
        Assert.Equal("Broken", failure.Source);
        Assert.Equal("Product Hunt rejected the token.", failure.Message);
        Assert.Equal(1, summary.NewToolCount);
        await using var db = _database.CreateContext();
        var failedRun = await db.SyncRuns.SingleAsync(r => r.Source == "Broken");
        Assert.False(failedRun.Succeeded);
        Assert.Equal("Product Hunt rejected the token.", failedRun.Error);
        Assert.True(await db.SyncRuns.AnyAsync(r => r.Source == "Working" && r.Succeeded && r.NewToolCount == 1));
    }

    [Fact]
    public async Task SyncAllAsync_SameToolTwiceInOneFetch_SavesItOnce()
    {
        var source = new FakeToolSource("A");
        source.Tools.AddRange([FakeToolSource.Tool("1", score: 5), FakeToolSource.Tool("1", score: 9)]);

        var summary = await SyncAsync(source);

        Assert.Equal(1, summary.NewToolCount);
        await using var db = _database.CreateContext();
        Assert.Equal(9, (await db.Tools.SingleAsync()).Score);
    }
}
