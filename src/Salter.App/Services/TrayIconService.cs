using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Salter.App.Interop;
using Salter.Presentation.Abstractions;
using Windows.Graphics;

namespace Salter.App.Services;

/// <summary>The notification area icon: a left click toggles the flyout, a right click opens its menu.</summary>
/// <remarks>Create and use it on the UI thread. Its events are raised on the UI thread.</remarks>
internal sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _icon;

    /// <summary>Creates the icon; it appears once <see cref="Show"/> is called.</summary>
    /// <param name="localizer">Supplies the tooltip and menu texts.</param>
    public TrayIconService(ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem(localizer.Get("Tray_Open"), () => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(MenuItem(localizer.Get("Tray_Settings"), () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem(localizer.Get("Tray_Exit"), () => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _icon = new TaskbarIcon
        {
            ToolTipText = localizer.Get("Tray_ToolTip"),
            // TaskbarIcon resolves ms-appx URIs against the app directory, so this also works unpackaged.
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/TrayIcon.ico")),
            // Native Win32 menu: looks like every other tray menu and needs no window of its own.
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            LeftClickCommand = new RelayCommand(() => ToggleRequested?.Invoke(this, EventArgs.Empty)),
            // Toggle on the first click instead of waiting to rule out a double click.
            NoLeftClickDelay = true,
        };
    }

    /// <summary>Raised when the icon is left-clicked.</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>Raised when the Open menu item is chosen.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the Settings menu item is chosen.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Raised when the Exit menu item is chosen.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Adds the icon to the notification area.</summary>
    public void Show() =>
        // Efficiency mode would throttle the whole process, including the flyout it shows.
        _icon.ForceCreate(enablesEfficiencyMode: false);

    /// <summary>Gets the icon's bounds on screen.</summary>
    /// <returns>The bounds in physical pixels, or <see langword="null"/> if the shell cannot report them.</returns>
    public RectInt32? GetIconBounds()
    {
        var identifier = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = _icon.TrayIcon.WindowHandle,
            Item = _icon.TrayIcon.Id,
        };
        if (NativeMethods.ShellNotifyIconGetRect(in identifier, out var rect) != 0)
        {
            return null;
        }

        return new RectInt32(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>Removes the icon from the notification area.</summary>
    public void Dispose() => _icon.Dispose();

    private static MenuFlyoutItem MenuItem(string text, Action execute) =>
        new() { Text = text, Command = new RelayCommand(execute) };
}
