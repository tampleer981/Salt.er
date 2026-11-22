namespace Salter.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the settings, falling back to <see cref="AppSettings.Default"/> when none are stored or they cannot be
    /// read or parsed. Never throws for file system reasons.
    /// </summary>
    /// <returns>The settings.</returns>
    AppSettings Load();

    /// <summary>Persists the settings, replacing any stored ones.</summary>
    /// <param name="settings">The settings to store.</param>
    /// <exception cref="IOException">The settings could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The settings location is not writable.</exception>
    void Save(AppSettings settings);
}
