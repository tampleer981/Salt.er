using System.Runtime.ExceptionServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Salter.App.Interop;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

// The namespace is Salter.App rather than Salter.App.Windows, which would shadow the Windows.* namespaces.
namespace Salter.App;

/// <summary>
/// The borderless, always-on-top flyout shown next to the tray. It stays out of the taskbar and Alt+Tab, and hides
/// when it loses focus or on Escape. Closing it only hides it; <see cref="CloseForExit"/> really closes it.
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    // Clicking the tray icon while the flyout is open first deactivates (and so hides) it; a toggle this soon
    // afterwards comes from that same click and must not reopen it.
    private const long ReopenGuardMilliseconds = 300;

    private readonly nint _handle;
    private bool _closeForExit;
    private long? _autoHiddenAt;
    private GlassAcrylicBackdrop? _backdrop;

    /// <summary>Creates the hidden flyout.</summary>
    public FlyoutWindow()
    {
        InitializeComponent();
        Title = "Salt.er";
        // On the content root, so they fire wherever the focus is inside the flyout. Their key tips stay hidden:
        // otherwise a tooltip such as "Esc" floats over the flyout whenever the root shows a tooltip.
        Root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        AddAccelerator(VirtualKey.Escape, VirtualKeyModifiers.None, OnEscapeInvoked);
        AddAccelerator(VirtualKey.K, VirtualKeyModifiers.Control, OnSearchInvoked);
        _handle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        // Keeps the thin frame without a caption: Windows 11 rounds only framed windows, and a frameless popup is
        // drawn with square corners and a light outline. The border itself is hidden in RoundCorners.
        presenter.SetBorderAndTitleBar(true, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += OnClosing;
        Activated += OnActivated;
        Closed += OnClosed;

        RoundCorners();
    }

    /// <summary>Raised when Ctrl+K asks to focus the search box.</summary>
    public event EventHandler? SearchFocusRequested;

    /// <summary>Raised each time the flyout has been shown and activated.</summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Gets or sets a value indicating whether losing focus keeps the flyout open, for example while a folder
    /// picker it opened is shown.
    /// </summary>
    public bool SuppressAutoHide { get; set; }

    /// <summary>
    /// Gets or sets the Escape handler of the content. It returns <see langword="true"/> if it handled the key;
    /// otherwise, or without a handler, Escape hides the flyout.
    /// </summary>
    public Func<bool>? EscapeHandler { get; set; }

    /// <summary>Gets a value indicating whether the flyout is shown.</summary>
    public bool IsOpen => AppWindow.IsVisible;

    /// <summary>Gets the root element of the content, which receives the theme.</summary>
    public FrameworkElement RootElement => Root;

    /// <summary>Gets the window handle, for example to own a picker.</summary>
    public nint Handle => _handle;

    /// <summary>
    /// Puts a thin desktop acrylic backdrop behind the content where the system supports it: lighter than the system
    /// default, with explicit Dark and Light tint/fallback palettes so the glass follows the flyout's theme cleanly.
    /// When acrylic is unsupported or fails to attach, reveals the gradient fallback background instead.
    /// </summary>
    /// <returns><see langword="true"/> if the backdrop was set; <see langword="false"/> if it is not supported.</returns>
    public bool TrySetAcrylicBackdrop()
    {
        if (_backdrop is not null)
        {
            return true;
        }

        if (!DesktopAcrylicController.IsSupported())
        {
            FallbackBackground.Visibility = Visibility.Visible;
            return false;
        }

        Exception? connectionError = null;
        var unavailable = false;
        var attaching = true;
        var backdrop = new GlassAcrylicBackdrop(
            () => !SuppressAutoHide,
            ex =>
            {
                FallbackBackground.Visibility = Visibility.Visible;
                if (attaching)
                {
                    unavailable = true;
                    connectionError = ex;
                }
            });
        try
        {
            DispatcherQueue.EnsureSystemDispatcherQueue();
            FallbackBackground.Visibility = Visibility.Collapsed;
            SystemBackdrop = backdrop;
        }
        catch
        {
            SystemBackdrop = null;
            FallbackBackground.Visibility = Visibility.Visible;
            throw;
        }
        finally
        {
            attaching = false;
        }

        if (connectionError is not null)
        {
            SystemBackdrop = null;
            ExceptionDispatchInfo.Capture(connectionError).Throw();
        }

        if (unavailable)
        {
            SystemBackdrop = null;
            return false;
        }

        _backdrop = backdrop;
        Root.ActualThemeChanged += OnRootThemeChanged;
        return true;
    }

    /// <summary>Sets the content shown in the flyout.</summary>
    /// <param name="content">The content; it fills the window.</param>
    public void SetContent(UIElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ContentHost.Children.Clear();
        ContentHost.Children.Add(content);
    }

    /// <summary>Shows the flyout at the given bounds and gives it focus.</summary>
    /// <param name="bounds">Bounds in physical screen pixels.</param>
    public void ShowAt(RectInt32 bounds)
    {
        // Moving onto a monitor with another DPI makes Windows rescale the window; the second call restores the
        // intended size.
        AppWindow.MoveAndResize(bounds);
        AppWindow.MoveAndResize(bounds);
        AppWindow.Show();
        Activate();
        // Activate alone may leave another window in the foreground; without focus the flyout would never get the
        // Deactivated event that hides it. Allowed because the tray click or a redirected launch granted it.
        _ = NativeMethods.SetForegroundWindow(_handle);
        // The content moves the focus inside, which also lets the keyboard accelerators fire.
        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hides the flyout if it is open, or shows it at the bounds <paramref name="bounds"/> computes.</summary>
    /// <param name="bounds">Computes the bounds when the flyout is about to be shown.</param>
    public void Toggle(Func<RectInt32> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        if (IsOpen)
        {
            Dismiss();
        }
        else if (_autoHiddenAt is not { } hiddenAt || Environment.TickCount64 - hiddenAt > ReopenGuardMilliseconds)
        {
            ShowAt(bounds());
        }
    }

    /// <summary>Hides the flyout.</summary>
    public void Dismiss() => AppWindow.Hide();

    /// <summary>Closes the window for good, when the app exits.</summary>
    public void CloseForExit()
    {
        _closeForExit = true;
        Close();
    }

    private void AddAccelerator(
        VirtualKey key,
        VirtualKeyModifiers modifiers,
        TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> invoked)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += invoked;
        Root.KeyboardAccelerators.Add(accelerator);
    }

    private void RoundCorners()
    {
        // Both fail on Windows 10, which has no rounded window corners or border colour: the window stays square,
        // and the content is still clipped to its own rounded corners.
        var preference = NativeMethods.DwmCornerRound;
        _ = NativeMethods.DwmSetWindowAttribute(_handle, NativeMethods.DwmWindowCornerPreference, in preference, sizeof(int));
        // The glass draws its own hairline; the system's 1 px window border would outline it in a second colour.
        var borderColor = NativeMethods.DwmColorNone;
        _ = NativeMethods.DwmSetWindowAttribute(_handle, NativeMethods.DwmBorderColor, in borderColor, sizeof(int));
    }

    private void OnRootThemeChanged(FrameworkElement sender, object args) => _backdrop?.SyncTheme(sender.ActualTheme);

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Root.ActualThemeChanged -= OnRootThemeChanged;
        SystemBackdrop = null;
        _backdrop = null;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && !SuppressAutoHide && IsOpen)
        {
            _autoHiddenAt = Environment.TickCount64;
            Dismiss();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_closeForExit)
        {
            // Alt+F4 must not end the app; the tray menu's Exit does.
            args.Cancel = true;
            Dismiss();
        }
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (EscapeHandler?.Invoke() != true)
        {
            Dismiss();
        }
    }

    private void OnSearchInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchFocusRequested?.Invoke(this, EventArgs.Empty);
    }
}
