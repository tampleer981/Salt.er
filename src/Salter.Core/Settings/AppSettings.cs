using Salter.Core.Feed;
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
    /// Gets the tags whose wallpapers the feed leaves out, normalized as described by <see cref="TagExclusions"/>.
    /// Empty by default; <see langword="null"/> is stored as empty.
    /// </summary>
    public IReadOnlyList<string> ExcludedTags
    {
        get;
        init => field = value ?? [];
    } = [];

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

    /// <summary>Compares all settings; <see cref="ExcludedTags"/> are compared by content, in order.</summary>
    /// <param name="other">The settings to compare with.</param>
    /// <returns><see langword="true"/> if both hold the same values.</returns>
    public bool Equals(AppSettings? other) =>
        other is not null
        && string.Equals(DownloadFolder, other.DownloadFolder, StringComparison.Ordinal)
        && LaunchAtStartup == other.LaunchAtStartup
        && Theme == other.Theme
        && Purity == other.Purity
        && ExcludedTags.SequenceEqual(other.ExcludedTags, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DownloadFolder, StringComparer.Ordinal);
        hash.Add(LaunchAtStartup);
        hash.Add(Theme);
        hash.Add(Purity);
        foreach (var tag in ExcludedTags)
        {
            hash.Add(tag, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
