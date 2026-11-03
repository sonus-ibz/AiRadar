using AiRadar.Core.Sources;

namespace AiRadar.App.ViewModels;

public sealed record ToolRow(
    string Name,
    string Tagline,
    string Url,
    string Source,
    int Score,
    string LaunchedText,
    bool IsNew,
    IReadOnlyList<string> Categories)
{
    private static readonly Dictionary<string, string> ScoreUnits = new()
    {
        [ProductHuntSource.SourceName] = "vote",
        [HackerNewsSource.SourceName] = "point",
        [HuggingFaceSpacesSource.SourceName] = "like",
        [GitHubSource.SourceName] = "star",
    };

    public string Details
    {
        get
        {
            var unit = ScoreUnits.GetValueOrDefault(Source, "point");
            return $"{Source} · {Score:N0} {unit}{(Score == 1 ? "" : "s")} · {LaunchedText}";
        }
    }
}
