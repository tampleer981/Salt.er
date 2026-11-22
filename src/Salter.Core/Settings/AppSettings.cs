using Salter.Core.Wallhaven;

namespace Salter.Core.Settings;

/// <summary>The app theme the user prefers.</summary>
public enum ThemePreference
{
    /// <summary>Follow the Windows app theme.</summary>
    System,
    /// <summary>Always light.</summary>
    Light,
    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>The user's persisted settings.</summary>
/// <param name="DownloadFolder">The folder wallpapers are downloaded to.</param>
/// <param name="LaunchAtStartup">Whether the app starts when the user signs in.</param>
/// <param name="Theme">The preferred app theme.</param>
/// <param name="Purity">The purity levels to browse.</param>
public sealed record AppSettings(string DownloadFolder, bool LaunchAtStartup, ThemePreference Theme, Purity Purity)
{
    /// <summary>
    /// The settings used before the user changes anything: downloads go to <c>Pictures\Salt.er</c>, no start at
    /// sign-in, system theme, SFW only.
    /// </summary>
    public static AppSettings Default { get; } =
        new(DefaultDownloadFolder(), LaunchAtStartup: false, ThemePreference.System, Purity.Sfw);

    private static string DefaultDownloadFolder()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(pictures))
        {
            pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
        }

        return Path.Combine(pictures, "Salt.er");
    }
}
