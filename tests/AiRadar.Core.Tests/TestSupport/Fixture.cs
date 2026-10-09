namespace AiRadar.Core.Tests.TestSupport;

/// <summary>Real API responses recorded on 09-10-2026, trimmed to a few items.</summary>
internal static class Fixture
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}
