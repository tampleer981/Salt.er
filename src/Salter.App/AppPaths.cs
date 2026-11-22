namespace Salter.App;

/// <summary>Locations of the app's per-user data under <c>%LOCALAPPDATA%\Salt.er</c>.</summary>
internal static class AppPaths
{
    /// <summary>Gets the app data folder.</summary>
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Salt.er");

    /// <summary>Gets the settings file.</summary>
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    /// <summary>Gets the log file path pattern; the logger inserts the date before the extension.</summary>
    public static string LogFile => Path.Combine(DataFolder, "logs", "salter-.log");
}
