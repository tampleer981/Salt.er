using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Salter.Core.Wallhaven;

namespace Salter.Presentation.ViewModels;

/// <summary>The flyout's root: navigates between the feed, a wallpaper's details and the settings.</summary>
/// <remarks>Members must be used from the UI thread.</remarks>
public sealed partial class ShellViewModel : ObservableObject
{
    /// <summary>Creates the shell and connects its screens.</summary>
    /// <param name="feed">The wallpaper feed.</param>
    /// <param name="detail">The detail screen; also applies wallpapers set straight from the feed.</param>
    /// <param name="settings">The settings screen; content changes reload the feed.</param>
    /// <param name="toast">The toast shown over every screen.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ShellViewModel(FeedViewModel feed, DetailViewModel detail, SettingsViewModel settings, ToastViewModel toast)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(toast);
        Feed = feed;
        Detail = detail;
        Settings = settings;
        Toast = toast;
        Page = ShellPage.Feed;

        feed.WallpaperOpened += OnWallpaperOpened;
        feed.QuickApplyRequested += OnQuickApplyRequested;
        detail.Closed += OnDetailClosed;
        settings.ContentSettingsChanged += OnContentSettingsChanged;
    }

    /// <summary>The wallpaper feed.</summary>
    public FeedViewModel Feed { get; }

    /// <summary>The detail screen.</summary>
    public DetailViewModel Detail { get; }

    /// <summary>The settings screen.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>The toast shown over every screen.</summary>
    public ToastViewModel Toast { get; }

    /// <summary>The screen being shown.</summary>
    [ObservableProperty]
    public partial ShellPage Page { get; private set; }

    /// <summary>Handles the Escape key.</summary>
    /// <returns>
    /// <see langword="true"/> if it navigated back to the feed; <see langword="false"/> if the feed is already
    /// shown and the window should hide.
    /// </returns>
    public bool HandleEscape()
    {
        if (Page == ShellPage.Feed)
        {
            return false;
        }

        Back();
        return true;
    }

    /// <summary>Shows the settings.</summary>
    [RelayCommand]
    private void ShowSettings() => Page = ShellPage.Settings;

    /// <summary>Returns to the feed, closing the detail screen if it is shown.</summary>
    [RelayCommand]
    private void Back()
    {
        if (Page == ShellPage.Detail)
        {
            // Raises Closed, which shows the feed.
            Detail.CloseCommand.Execute(null);
        }
        else
        {
            Page = ShellPage.Feed;
        }
    }

    private void OnWallpaperOpened(object? sender, WallpaperSummary wallpaper)
    {
        Page = ShellPage.Detail;
        OpenDetail(wallpaper);
    }

    private void OnQuickApplyRequested(object? sender, WallpaperSummary wallpaper) => QuickApply(wallpaper);

    private void OnDetailClosed(object? sender, EventArgs e)
    {
        // An apply finishing while the user is elsewhere, such as the settings, must not navigate.
        if (Page == ShellPage.Detail)
        {
            Page = ShellPage.Feed;
        }
    }

    private void OnContentSettingsChanged(object? sender, EventArgs e) => Feed.RefreshCommand.Execute(null);

    // Event handlers have no caller to await these; awaiting here surfaces unexpected failures on the UI context the
    // same way an async command would. Expected failures are handled inside DetailViewModel.
    private async void OpenDetail(WallpaperSummary wallpaper) => await Detail.OpenAsync(wallpaper, CancellationToken.None);

    private async void QuickApply(WallpaperSummary wallpaper) => await Detail.ApplyQuickAsync(wallpaper);
}
