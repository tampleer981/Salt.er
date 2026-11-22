using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;

namespace Salter.Presentation.ViewModels;

/// <summary>
/// The settings screen. Every change is persisted immediately, except the API key, which is stored by
/// <see cref="ApplyApiKeyCommand"/> and removed by <see cref="ClearApiKeyCommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// Sketchy and NSFW content require an API key: without one, <see cref="AllowSketchy"/> and
/// <see cref="AllowNsfw"/> stay <see langword="false"/> and the stored purity is reset to SFW. SFW content is always
/// included.
/// </para>
/// <para>
/// When a change cannot be persisted (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/> or
/// <see cref="InvalidOperationException"/>), an error toast is shown and the property returns to its last persisted
/// value. Members must be used from the UI thread.
/// </para>
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly ISecretStore _secrets;
    private readonly IStartupRegistration _startup;
    private readonly IFolderPicker _folderPicker;
    private readonly IToastService _toasts;
    private readonly ILocalizer _localizer;

    // False while the constructor sets the initial values and while a failed change is reverted, so that neither
    // persists anything.
    private bool _persistChanges;
    private bool _allowSketchy;
    private bool _allowNsfw;

    /// <summary>Creates the settings view model from the stored settings and API key.</summary>
    /// <param name="settings">Loads and saves the settings.</param>
    /// <param name="secrets">Holds the API key.</param>
    /// <param name="startup">Controls starting at sign-in; its current state is shown.</param>
    /// <param name="folderPicker">Lets the user choose the download folder.</param>
    /// <param name="toasts">Reports changes that could not be saved.</param>
    /// <param name="localizer">Provides the toast messages.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public SettingsViewModel(
        ISettingsStore settings,
        ISecretStore secrets,
        IStartupRegistration startup,
        IFolderPicker folderPicker,
        IToastService toasts,
        ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(folderPicker);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(localizer);
        _settings = settings;
        _secrets = secrets;
        _startup = startup;
        _folderPicker = folderPicker;
        _toasts = toasts;
        _localizer = localizer;

        var stored = settings.Load();
        var key = secrets.Get(SecretNames.WallhavenApiKey);
        DownloadFolder = stored.DownloadFolder;
        Theme = stored.Theme;
        LaunchAtStartup = startup.IsEnabled;
        HasApiKey = !string.IsNullOrWhiteSpace(key);
        ApiKey = HasApiKey ? key : null;
        _allowSketchy = HasApiKey && stored.Purity.HasFlag(Purity.Sketchy);
        _allowNsfw = HasApiKey && stored.Purity.HasFlag(Purity.Nsfw);
        if (!HasApiKey && stored.Purity != Purity.Sfw)
        {
            // Best effort and silent: the feed requests SFW only without a key either way.
            TrySave(s => s with { Purity = Purity.Sfw });
        }

        _persistChanges = true;
    }

    /// <summary>Raised when the API key or the allowed purity levels change, so the feed can reload.</summary>
    public event EventHandler? ContentSettingsChanged;

    /// <summary>Raised when <see cref="Theme"/> changes, so the app can apply it.</summary>
    public event EventHandler? ThemeChanged;

    /// <summary>The folder wallpapers are downloaded to; changed by <see cref="ChangeFolderCommand"/>.</summary>
    [ObservableProperty]
    public partial string DownloadFolder { get; private set; }

    /// <summary>Whether the app starts when the user signs in.</summary>
    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    /// <summary>The preferred app theme.</summary>
    [ObservableProperty]
    public partial ThemePreference Theme { get; set; }

    /// <summary>The API key being edited; stored only by <see cref="ApplyApiKeyCommand"/>.</summary>
    [ObservableProperty]
    public partial string? ApiKey { get; set; }

    /// <summary>Whether an API key is stored.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangePurity))]
    public partial bool HasApiKey { get; private set; }

    /// <summary>Whether <see cref="AllowSketchy"/> and <see cref="AllowNsfw"/> can be changed; requires an API key.</summary>
    public bool CanChangePurity => HasApiKey;

    /// <summary>Whether sketchy wallpapers are included. Always <see langword="false"/> without an API key.</summary>
    public bool AllowSketchy
    {
        get => _allowSketchy;
        set => SetPurity(ref _allowSketchy, value);
    }

    /// <summary>Whether NSFW wallpapers are included. Always <see langword="false"/> without an API key.</summary>
    public bool AllowNsfw
    {
        get => _allowNsfw;
        set => SetPurity(ref _allowNsfw, value);
    }

    /// <summary>Lets the user choose a new download folder; cancelling keeps the current one.</summary>
    [RelayCommand]
    private async Task ChangeFolderAsync()
    {
        var folder = await _folderPicker.PickAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DownloadFolder = folder;
        }
    }

    /// <summary>
    /// Stores the trimmed <see cref="ApiKey"/>; a blank key clears the stored one. If the key cannot be stored, the
    /// previously stored key is shown again.
    /// </summary>
    [RelayCommand]
    private void ApplyApiKey()
    {
        var key = ApiKey?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            ClearApiKey();
            return;
        }

        if (!TryPersist(() => _secrets.Set(SecretNames.WallhavenApiKey, key)))
        {
            RestoreStoredApiKey();
            return;
        }

        ApiKey = key;
        HasApiKey = true;
        ContentSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes the stored API key and limits browsing to SFW content. If the key cannot be removed, nothing changes.
    /// </summary>
    [RelayCommand]
    private void ClearApiKey()
    {
        if (!TryPersist(() => _secrets.Set(SecretNames.WallhavenApiKey, null)))
        {
            RestoreStoredApiKey();
            return;
        }

        ApiKey = null;
        HasApiKey = false;
        SetProperty(ref _allowSketchy, false, nameof(AllowSketchy));
        SetProperty(ref _allowNsfw, false, nameof(AllowNsfw));
        // The key is gone, so the feed must reload even if the purity reset is not saved; it then requests SFW only,
        // and the stored purity is reset at the next start.
        TryPersist(s => s with { Purity = Purity.Sfw });
        ContentSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDownloadFolderChanged(string oldValue, string newValue)
    {
        if (_persistChanges && !TryPersist(s => s with { DownloadFolder = newValue }))
        {
            Revert(() => DownloadFolder = oldValue);
        }
    }

    partial void OnLaunchAtStartupChanged(bool oldValue, bool newValue)
    {
        if (!_persistChanges)
        {
            return;
        }

        if (!TryPersist(() => _startup.SetEnabled(newValue)))
        {
            Revert(() => LaunchAtStartup = oldValue);
            return;
        }

        if (!TryPersist(s => s with { LaunchAtStartup = newValue }))
        {
            // Keep the registration and the stored setting in agreement; the failure is already reported.
            TrySave(() => _startup.SetEnabled(oldValue));
            Revert(() => LaunchAtStartup = oldValue);
        }
    }

    partial void OnThemeChanged(ThemePreference oldValue, ThemePreference newValue)
    {
        if (!_persistChanges)
        {
            return;
        }

        if (!TryPersist(s => s with { Theme = newValue }))
        {
            Revert(() => Theme = oldValue);
            return;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPurity(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        var sketchy = propertyName == nameof(AllowSketchy) ? value : _allowSketchy;
        var nsfw = propertyName == nameof(AllowNsfw) ? value : _allowNsfw;
        var purity = Purity.Sfw | (sketchy ? Purity.Sketchy : 0) | (nsfw ? Purity.Nsfw : 0);
        if ((value && !HasApiKey) || !TryPersist(s => s with { Purity = purity }))
        {
            // Refused or not saved: tell bindings to read the unchanged value again.
            OnPropertyChanged(propertyName);
            return;
        }

        SetProperty(ref field, value, propertyName);
        ContentSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreStoredApiKey()
    {
        var stored = _secrets.Get(SecretNames.WallhavenApiKey);
        ApiKey = string.IsNullOrWhiteSpace(stored) ? null : stored;
    }

    private void Revert(Action restore)
    {
        _persistChanges = false;
        try
        {
            restore();
        }
        finally
        {
            _persistChanges = true;
        }
    }

    private bool TryPersist(Func<AppSettings, AppSettings> change) => TryPersist(() => Save(change));

    private bool TryPersist(Action persist)
    {
        if (TrySave(persist))
        {
            return true;
        }

        _toasts.Show(_localizer.Get("Toast_SettingsSaveFailed"), ToastKind.Error);
        return false;
    }

    private bool TrySave(Func<AppSettings, AppSettings> change) => TrySave(() => Save(change));

    private static bool TrySave(Action persist)
    {
        try
        {
            persist();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private void Save(Func<AppSettings, AppSettings> change) => _settings.Save(change(_settings.Load()));
}
