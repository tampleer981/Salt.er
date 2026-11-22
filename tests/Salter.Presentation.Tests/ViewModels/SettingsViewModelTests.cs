using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Tests.TestDoubles;
using Salter.Presentation.ViewModels;

namespace Salter.Presentation.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly FakeSettingsStore _settings = new()
    {
        Settings = new AppSettings("/pictures", LaunchAtStartup: false, ThemePreference.Dark, Purity.Sfw),
    };

    private readonly FakeSecretStore _secrets = new();
    private readonly FakeStartupRegistration _startup = new();
    private readonly FakeFolderPicker _picker = new();
    private readonly RecordingToastService _toasts = new();

    [Fact]
    public void Starts_with_stored_values()
    {
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Nsfw };
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _startup.IsEnabled = true;

        var vm = CreateViewModel();

        Assert.Equal("/pictures", vm.DownloadFolder);
        Assert.Equal(ThemePreference.Dark, vm.Theme);
        Assert.True(vm.LaunchAtStartup);
        Assert.Equal("key-1", vm.ApiKey);
        Assert.True(vm.HasApiKey);
        Assert.True(vm.CanChangePurity);
        Assert.False(vm.AllowSketchy);
        Assert.True(vm.AllowNsfw);
    }

    [Fact]
    public void Without_key_nsfw_and_sketchy_are_off_and_disabled()
    {
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Sketchy | Purity.Nsfw };
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        Assert.Null(vm.ApiKey);
        Assert.False(vm.HasApiKey);
        Assert.False(vm.CanChangePurity);
        Assert.False(vm.AllowSketchy);
        Assert.False(vm.AllowNsfw);
        Assert.Equal(Purity.Sfw, _settings.Settings.Purity);

        vm.AllowSketchy = true;
        vm.AllowNsfw = true;

        Assert.False(vm.AllowSketchy);
        Assert.False(vm.AllowNsfw);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void With_key_purity_changes_persist_and_raise_ContentSettingsChanged()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        vm.AllowSketchy = true;
        Assert.Equal(Purity.Sfw | Purity.Sketchy, _settings.Settings.Purity);
        vm.AllowNsfw = true;
        Assert.Equal(Purity.Sfw | Purity.Sketchy | Purity.Nsfw, _settings.Settings.Purity);
        vm.AllowSketchy = false;
        Assert.Equal(Purity.Sfw | Purity.Nsfw, _settings.Settings.Purity);
        vm.AllowSketchy = false;

        Assert.Equal(3, changes);
    }

    [Fact]
    public void Setting_key_persists_to_secret_store_and_raises_ContentSettingsChanged()
    {
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        vm.ApiKey = "  key-2  ";
        Assert.Empty(_secrets.Secrets);
        vm.ApplyApiKeyCommand.Execute(null);

        Assert.Equal("key-2", _secrets.Secrets[SecretNames.WallhavenApiKey]);
        Assert.Equal("key-2", vm.ApiKey);
        Assert.True(vm.HasApiKey);
        Assert.True(vm.CanChangePurity);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Applying_a_blank_key_clears_it()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Sketchy };
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        vm.ApiKey = "   ";
        vm.ApplyApiKeyCommand.Execute(null);

        Assert.Empty(_secrets.Secrets);
        Assert.Null(vm.ApiKey);
        Assert.False(vm.HasApiKey);
        Assert.False(vm.AllowSketchy);
        Assert.Equal(Purity.Sfw, _settings.Settings.Purity);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Clearing_key_resets_purity_to_Sfw()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Sketchy | Purity.Nsfw };
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        vm.ClearApiKeyCommand.Execute(null);

        Assert.Empty(_secrets.Secrets);
        Assert.Null(vm.ApiKey);
        Assert.False(vm.HasApiKey);
        Assert.False(vm.CanChangePurity);
        Assert.False(vm.AllowSketchy);
        Assert.False(vm.AllowNsfw);
        Assert.Equal(Purity.Sfw, _settings.Settings.Purity);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void LaunchAtStartup_toggles_registration()
    {
        var vm = CreateViewModel();

        vm.LaunchAtStartup = true;
        Assert.True(_startup.IsEnabled);
        Assert.True(_settings.Settings.LaunchAtStartup);

        vm.LaunchAtStartup = false;
        Assert.False(_startup.IsEnabled);
        Assert.False(_settings.Settings.LaunchAtStartup);
    }

    [Fact]
    public void Theme_change_persists_and_raises_ThemeChanged()
    {
        var vm = CreateViewModel();
        var raised = 0;
        vm.ThemeChanged += (_, _) => raised++;

        vm.Theme = ThemePreference.Light;

        Assert.Equal(ThemePreference.Light, _settings.Settings.Theme);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task ChangeFolder_persists_the_chosen_folder()
    {
        _picker.Result = "/elsewhere";
        var vm = CreateViewModel();

        await vm.ChangeFolderCommand.ExecuteAsync(null);

        Assert.Equal("/elsewhere", vm.DownloadFolder);
        Assert.Equal("/elsewhere", _settings.Settings.DownloadFolder);
    }

    [Fact]
    public async Task ChangeFolder_cancelled_keeps_old_value()
    {
        _picker.Result = null;
        var vm = CreateViewModel();
        var before = _settings.Settings;

        await vm.ChangeFolderCommand.ExecuteAsync(null);

        Assert.Equal(1, _picker.Calls);
        Assert.Equal("/pictures", vm.DownloadFolder);
        Assert.Same(before, _settings.Settings);
    }

    [Fact]
    public void Changes_keep_the_other_stored_values()
    {
        var vm = CreateViewModel();

        vm.Theme = ThemePreference.Light;
        vm.LaunchAtStartup = true;

        Assert.Equal(new AppSettings("/pictures", LaunchAtStartup: true, ThemePreference.Light, Purity.Sfw), _settings.Settings);
    }

    [Fact]
    public void With_key_stored_purity_is_kept()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Nsfw };

        CreateViewModel();

        Assert.Equal(0, _settings.SaveAttempts);
    }

    [Fact]
    public void Without_key_a_failed_purity_reset_still_starts_sfw_only()
    {
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Nsfw };
        _settings.SaveFailure = new IOException("locked");

        var vm = CreateViewModel();

        Assert.False(vm.AllowNsfw);
        Assert.Empty(_toasts.Shown);
    }

    [Fact]
    public async Task ChangeFolder_save_failure_keeps_old_folder_and_shows_error()
    {
        _picker.Result = "/elsewhere";
        _settings.SaveFailure = new UnauthorizedAccessException("read-only");
        var vm = CreateViewModel();

        await vm.ChangeFolderCommand.ExecuteAsync(null);

        Assert.Equal("/pictures", vm.DownloadFolder);
        Assert.Equal(1, _settings.SaveAttempts);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Theme_save_failure_reverts_without_ThemeChanged()
    {
        _settings.SaveFailure = new IOException("disk full");
        var vm = CreateViewModel();
        var raised = 0;
        vm.ThemeChanged += (_, _) => raised++;

        vm.Theme = ThemePreference.Light;

        Assert.Equal(ThemePreference.Dark, vm.Theme);
        Assert.Equal(0, raised);
        Assert.Equal(1, _settings.SaveAttempts);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Startup_registration_failure_reverts()
    {
        _startup.Failure = new InvalidOperationException("registry denied");
        var vm = CreateViewModel();

        vm.LaunchAtStartup = true;

        Assert.False(vm.LaunchAtStartup);
        Assert.False(_startup.IsEnabled);
        Assert.Equal([true], _startup.Calls);
        Assert.Equal(0, _settings.SaveAttempts);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Startup_save_failure_rolls_back_the_registration()
    {
        _settings.SaveFailure = new IOException("disk full");
        var vm = CreateViewModel();

        vm.LaunchAtStartup = true;

        Assert.False(vm.LaunchAtStartup);
        Assert.False(_startup.IsEnabled);
        Assert.Equal([true, false], _startup.Calls);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Purity_save_failure_reverts_without_ContentSettingsChanged()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.SaveFailure = new IOException("disk full");
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;

        vm.AllowNsfw = true;

        Assert.False(vm.AllowNsfw);
        Assert.Equal(0, changes);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Api_key_store_failure_keeps_the_stored_key()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;
        _secrets.Failure = new InvalidOperationException("credential manager unavailable");

        vm.ApiKey = "key-2";
        vm.ApplyApiKeyCommand.Execute(null);

        Assert.Equal("key-1", vm.ApiKey);
        Assert.True(vm.HasApiKey);
        Assert.Equal("key-1", _secrets.Secrets[SecretNames.WallhavenApiKey]);
        Assert.Equal(0, changes);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Api_key_clear_failure_keeps_the_key_and_purity()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Nsfw };
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;
        _secrets.Failure = new InvalidOperationException("credential manager unavailable");

        vm.ClearApiKeyCommand.Execute(null);

        Assert.Equal("key-1", vm.ApiKey);
        Assert.True(vm.HasApiKey);
        Assert.True(vm.AllowNsfw);
        Assert.Equal(Purity.Sfw | Purity.Nsfw, _settings.Settings.Purity);
        Assert.Equal(0, changes);
        AssertSaveFailedToast();
    }

    [Fact]
    public void Api_key_cleared_but_purity_save_failure_still_reloads_sfw_only()
    {
        _secrets.Secrets[SecretNames.WallhavenApiKey] = "key-1";
        _settings.Settings = _settings.Settings with { Purity = Purity.Sfw | Purity.Nsfw };
        var vm = CreateViewModel();
        var changes = 0;
        vm.ContentSettingsChanged += (_, _) => changes++;
        _settings.SaveFailure = new IOException("disk full");

        vm.ClearApiKeyCommand.Execute(null);

        Assert.Empty(_secrets.Secrets);
        Assert.False(vm.HasApiKey);
        Assert.False(vm.AllowNsfw);
        Assert.Equal(1, changes);
        AssertSaveFailedToast();
    }

    private void AssertSaveFailedToast() =>
        Assert.Equal([("Toast_SettingsSaveFailed", ToastKind.Error)], _toasts.Shown);

    private SettingsViewModel CreateViewModel() =>
        new(_settings, _secrets, _startup, _picker, _toasts, new FakeLocalizer());
}
