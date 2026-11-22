using System.ComponentModel;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Salter.Presentation.Abstractions;
using Salter.Presentation.ViewModels;

namespace Salter.App.Views;

/// <summary>
/// The flyout's content: shows the shell's current screen with a short fade (the details also slide up), and the
/// toast over every screen. Each time the flyout opens, the content fades in while sliding up.
/// </summary>
public sealed partial class ShellView : UserControl
{
    // The radius Windows 11 rounds the window with; a larger one would leave plain acrylic showing in the corners.
    private const float GlassCornerRadius = 8;
    private const float EntranceOffset = 12;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);

    private readonly FeedView _feed;
    private readonly DetailView _detail;
    private readonly SettingsView _settings;
    private CompositionRoundedRectangleGeometry? _clip;

    /// <summary>Creates the content for a shell.</summary>
    /// <param name="shell">The shell shown.</param>
    public ShellView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        Shell = shell;
        InitializeComponent();

        // All screens stay alive, so the feed keeps its scroll position while the details or settings are shown.
        _feed = new FeedView(shell);
        _detail = new DetailView(shell.Detail);
        _settings = new SettingsView(shell);
        Pages.Children.Add(_feed);
        Pages.Children.Add(_detail);
        Pages.Children.Add(_settings);
        ShowPage(animate: false);

        Shell.PropertyChanged += OnShellPropertyChanged;
        Shell.Toast.PropertyChanged += OnToastPropertyChanged;
        Glass.SizeChanged += OnGlassSizeChanged;
    }

    /// <summary>Gets the shell shown.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Fades the toast in while it is showing.</summary>
    /// <param name="isVisible">Whether a toast is showing.</param>
    /// <returns>The toast's opacity.</returns>
    public static double ToastOpacity(bool isVisible) => isVisible ? 1 : 0;

    /// <summary>Shows the check mark on success toasts.</summary>
    /// <param name="kind">The toast's severity.</param>
    /// <returns>The check mark's visibility.</returns>
    public static Visibility SuccessVisibility(ToastKind kind) =>
        kind == ToastKind.Success ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Shows the error mark on error toasts.</summary>
    /// <param name="kind">The toast's severity.</param>
    /// <returns>The error mark's visibility.</returns>
    public static Visibility ErrorVisibility(ToastKind kind) =>
        kind == ToastKind.Error ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Shows the feed, if needed, and moves keyboard focus to its search box.</summary>
    public void FocusSearch()
    {
        if (Shell.Page == ShellPage.Feed)
        {
            _feed.FocusSearch();
            return;
        }

        Shell.BackCommand.Execute(null);
        // The feed has just been made visible; focus it once it has been laid out.
        _ = DispatcherQueue.TryEnqueue(_feed.FocusSearch);
    }

    /// <summary>Moves keyboard focus into the screen being shown: the search box, or the back button.</summary>
    public void FocusCurrentPage()
    {
        switch (Shell.Page)
        {
            case ShellPage.Detail:
                _detail.FocusFirst();
                break;
            case ShellPage.Settings:
                _settings.FocusFirst();
                break;
            default:
                _feed.FocusSearch();
                break;
        }
    }

    /// <summary>Fades the content in while it slides up, as the flyout opens. The glass itself stays in place.</summary>
    public void PlayOpenAnimation() => Motion.Enter(Pages, EntranceOffset, OpenDuration);

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Page))
        {
            ShowPage(animate: true);
            // Focus would otherwise stay on an element that has just been hidden.
            _ = DispatcherQueue.TryEnqueue(FocusCurrentPage);
        }
    }

    private void ShowPage(bool animate)
    {
        UIElement shown = Shell.Page switch
        {
            ShellPage.Detail => _detail,
            ShellPage.Settings => _settings,
            _ => _feed,
        };
        foreach (var page in Pages.Children)
        {
            page.Visibility = page == shown ? Visibility.Visible : Visibility.Collapsed;
        }

        if (animate)
        {
            // The details come up from below, like a sheet; the other screens only fade.
            Motion.Enter(shown, shown == _detail ? EntranceOffset : 0, FadeDuration);
        }
    }

    private void OnToastPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A toast appears, or replaces the one showing.
        if ((e.PropertyName is nameof(ToastViewModel.IsVisible) or nameof(ToastViewModel.Message)) && Shell.Toast.IsVisible)
        {
            // Deferred so the binding has updated the text before screen readers read it.
            _ = DispatcherQueue.TryEnqueue(AnnounceToast);
        }
    }

    private void AnnounceToast() =>
        FrameworkElementAutomationPeer.FromElement(ToastText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);

    private void OnGlassSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Clips everything inside the glass, edge-to-edge thumbnails included, to its rounded corners.
        if (_clip is null)
        {
            var visual = ElementCompositionPreview.GetElementVisual(Glass);
            _clip = visual.Compositor.CreateRoundedRectangleGeometry();
            _clip.CornerRadius = new Vector2(GlassCornerRadius);
            visual.Clip = visual.Compositor.CreateGeometricClip(_clip);
        }

        _clip.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
    }
}
