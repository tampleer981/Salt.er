using Salter.Core.Settings;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Settings store backed by a property.</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Settings { get; set; } = AppSettings.Default;

    public AppSettings Load() => Settings;

    /// <summary>When set, <see cref="Save"/> fails with this exception and keeps the stored settings.</summary>
    public Exception? SaveFailure { get; set; }

    public int SaveAttempts { get; private set; }

    public void Save(AppSettings settings)
    {
        SaveAttempts++;
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Settings = settings;
    }
}
