using System.IO;

namespace AiRadar.App.Services;

public static class AppPaths
{
    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiRadar");

    public static string DatabaseFile => Path.Combine(DataFolder, "airadar.db");

    public static string TokenFile => Path.Combine(DataFolder, "producthunt.token");
}
