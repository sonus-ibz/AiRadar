using AiRadar.Core.Sources;

namespace AiRadar.Core.Tests.TestSupport;

internal sealed class FakeToolSource(string name) : IToolSource
{
    public string Name => name;

    public List<DiscoveredTool> Tools { get; } = [];

    public Exception? FailWith { get; set; }

    public List<DateTime> RequestedSince { get; } = [];

    public Task<IReadOnlyList<DiscoveredTool>> FetchSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        RequestedSince.Add(sinceUtc);
        return FailWith is null
            ? Task.FromResult<IReadOnlyList<DiscoveredTool>>(Tools.ToList())
            : Task.FromException<IReadOnlyList<DiscoveredTool>>(FailWith);
    }

    public static DiscoveredTool Tool(string id, int score = 10, DateTime? launchedAtUtc = null) =>
        new(id, "Tool " + id, "Tagline " + id, null, "https://example.com/" + id, score,
            launchedAtUtc ?? new DateTime(2026, 10, 7, 6, 0, 0, DateTimeKind.Utc));
}
