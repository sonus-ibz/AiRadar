using System.IO;
using System.Text.Json;
using AiRadar.Core.Sources;
using AiRadar.Core.Tools;

namespace AiRadar.App.Services;

/// <summary>Minimum scores a tool needs to appear. Tools below them are still saved, only hidden.</summary>
public sealed class AppSettings
{
    public int ProductHuntMinVotes { get; set; } = 20;
    public int HackerNewsMinPoints { get; set; } = 10;
    public int HuggingFaceMinLikes { get; set; } = 50;
    public int GitHubMinStars { get; set; } = 200;

    public ToolFilter ProductsFilter() => new(new Dictionary<string, int>
    {
        [ProductHuntSource.SourceName] = ProductHuntMinVotes,
        [HackerNewsSource.SourceName] = HackerNewsMinPoints,
    });

    public ToolFilter DevelopersFilter() => new(new Dictionary<string, int>
    {
        [HuggingFaceSpacesSource.SourceName] = HuggingFaceMinLikes,
        [GitHubSource.SourceName] = GitHubMinStars,
    });
}

public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // A hand-edited or damaged file falls back to the defaults; the next save overwrites it.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
