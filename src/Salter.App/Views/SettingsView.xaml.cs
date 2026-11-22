using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Salter.Core.Settings;
using Salter.Presentation.ViewModels;
using Windows.System;

namespace Salter.App.Views;

/// <summary>
/// The settings screen: API key, content purity, download folder, start at sign-in, theme and project links.
/// </summary>
public sealed partial class SettingsView : UserControl
{
    /// <summary>Creates the settings screen.</summary>
    /// <param name="shell">The shell; the screen shows its settings and navigates back with it.</param>
    public SettingsView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        Shell = shell;
        InitializeComponent();

        // A password box cannot be bound to a null key, and the theme is one enum shown as three buttons; both are
        // kept in step here instead.
        ShowApiKey();
        ShowTheme();
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>Gets the shell.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Gets the settings shown.</summary>
    public SettingsViewModel Settings => Shell.Settings;

    /// <summary>Moves keyboard focus to the back button.</summary>
    public void FocusFirst() => _ = BackButton.Focus(FocusState.Programmatic);

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.ApiKey):
                ShowApiKey();
                break;
            case nameof(SettingsViewModel.Theme):
                ShowTheme();
                break;
        }
    }

    private void ShowApiKey()
    {
        var key = Settings.ApiKey ?? string.Empty;
        if (ApiKeyBox.Password != key)
        {
            ApiKeyBox.Password = key;
        }
    }

    private void ShowTheme()
    {
        ThemeSystem.IsChecked = Settings.Theme == ThemePreference.System;
        ThemeLight.IsChecked = Settings.Theme == ThemePreference.Light;
        ThemeDark.IsChecked = Settings.Theme == ThemePreference.Dark;
    }

    private void OnApiKeyChanged(object sender, RoutedEventArgs e) => Settings.ApiKey = ApiKeyBox.Password;

    private void OnApiKeyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Settings.ApplyApiKeyCommand.Execute(null);
        }
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        Settings.Theme = ReferenceEquals(sender, ThemeLight) ? ThemePreference.Light
            : ReferenceEquals(sender, ThemeDark) ? ThemePreference.Dark
            : ThemePreference.System;
        // A refused change leaves the stored theme in place; show it.
        ShowTheme();
    }
}
