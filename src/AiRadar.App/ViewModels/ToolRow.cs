namespace AiRadar.App.ViewModels;

public sealed record ToolRow(
    string Name,
    string Tagline,
    string Url,
    string Source,
    int Score,
    string LaunchedText,
    bool IsNew)
{
    public string Details => $"{Source} · {Score:N0} votes · {LaunchedText}";
}
