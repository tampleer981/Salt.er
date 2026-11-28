using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

// The namespace is Salter.App rather than Salter.App.Windows, which would shadow the Windows.* namespaces.
namespace Salter.App;

/// <summary>
/// A thin desktop acrylic system backdrop whose tint and fallback colours track the flyout's theme explicitly.
/// Customizing any of <see cref="DesktopAcrylicController"/>'s tint or luminosity properties disables its automatic
/// Dark/Light colour switching, so this backdrop supplies explicit palettes for both themes and keeps them in sync
/// with the window's configuration and theme changes.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The acrylic controller is disposed in OnTargetDisconnected when the backdrop detaches from its window.")]
public sealed partial class GlassAcrylicBackdrop : SystemBackdrop
{
    // Thin desktop acrylic with explicit Dark and Light palettes: translucent enough for the blurred desktop to read
    // like glass while staying dark in Dark mode and falling back to clean charcoal / frost when transparency is off.
    private const float DarkTintOpacity = 0.20f;
    private const float DarkLuminosityOpacity = 0.58f;
    private const float LightTintOpacity = 0.16f;
    private const float LightLuminosityOpacity = 0.64f;

    private static readonly Color DarkTintColor = Color.FromArgb(0xFF, 0x10, 0x12, 0x16);
    private static readonly Color DarkFallbackColor = Color.FromArgb(0xFF, 0x14, 0x16, 0x1A);
    private static readonly Color LightTintColor = Color.FromArgb(0xFF, 0xF8, 0xF9, 0xFB);
    private static readonly Color LightFallbackColor = Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF8);

    private readonly Func<bool> _keepActiveWhenDeactivated;
    private readonly Action<Exception?>? _onUnavailable;
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;
    private XamlRoot? _xamlRoot;

    /// <summary>Creates the glass acrylic backdrop.</summary>
    /// <param name="keepActiveWhenDeactivated">
    /// Returns <see langword="true"/> when losing focus should keep the acrylic active (because the flyout hides on
    /// deactivation and would otherwise flash its fallback on reopen), or <see langword="false"/> when the flyout
    /// stays visible in the background and should show its inactive fallback.
    /// </param>
    /// <param name="onUnavailable">
    /// Invoked if the backing <see cref="DesktopAcrylicController"/> cannot attach to the window target, passing the
    /// failure exception or <see langword="null"/> when the target was rejected.
    /// </param>
    public GlassAcrylicBackdrop(Func<bool> keepActiveWhenDeactivated, Action<Exception?>? onUnavailable = null)
    {
        ArgumentNullException.ThrowIfNull(keepActiveWhenDeactivated);
        _keepActiveWhenDeactivated = keepActiveWhenDeactivated;
        _onUnavailable = onUnavailable;
    }

    /// <summary>
    /// Synchronizes the backdrop's theme and colour palette with the window root's current actual theme, in case the
    /// theme changes while the window is hidden.
    /// </summary>
    /// <param name="actualTheme">The root element's resolved theme.</param>
    internal void SyncTheme(ElementTheme actualTheme)
    {
        if (_controller is null || _configuration is null)
        {
            return;
        }

        var theme = actualTheme switch
        {
            ElementTheme.Light => SystemBackdropTheme.Light,
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            _ => SystemBackdropTheme.Default,
        };
        _configuration.Theme = theme;
        if (!_configuration.IsHighContrast)
        {
            ApplyPalette(_controller, IsLightTheme(theme, _xamlRoot));
        }
    }

    /// <inheritdoc/>
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (_controller is not null)
        {
            throw new InvalidOperationException("GlassAcrylicBackdrop can only be attached to one window at a time.");
        }

        var defaultConfiguration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        var configuration = new SystemBackdropConfiguration();
        var controller = new DesktopAcrylicController();
        try
        {
            ApplyConfiguration(controller, configuration, defaultConfiguration, xamlRoot);
            controller.SetSystemBackdropConfiguration(configuration);
            if (!controller.AddSystemBackdropTarget(connectedTarget))
            {
                controller.Dispose();
                _onUnavailable?.Invoke(null);
                return;
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NotImplementedException)
        {
            controller.Dispose();
            _onUnavailable?.Invoke(ex);
            return;
        }
        catch
        {
            controller.Dispose();
            throw;
        }

        _controller = controller;
        _configuration = configuration;
        _xamlRoot = xamlRoot;
    }

    /// <inheritdoc/>
    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot)
    {
        // No call to the base method: SystemBackdrop's default implementation rejects the target it is handed here
        // (ArgumentException "target") when the window's theme changes, and a custom backdrop has nothing to delegate.
        if (_controller is null || _configuration is null)
        {
            return;
        }

        _xamlRoot = xamlRoot;
        try
        {
            var defaultConfiguration = GetDefaultSystemBackdropConfiguration(target, xamlRoot);
            ApplyConfiguration(_controller, _configuration, defaultConfiguration, xamlRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or COMException)
        {
            // Keep the current palette; SyncTheme applies the theme from the root element instead.
            if (xamlRoot.Content is FrameworkElement root)
            {
                SyncTheme(root.ActualTheme);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        if (_controller is not null)
        {
            _ = _controller.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller.Dispose();
            _controller = null;
        }

        _configuration = null;
        _xamlRoot = null;
    }

    private static bool IsLightTheme(SystemBackdropTheme theme, XamlRoot? xamlRoot) => theme switch
    {
        SystemBackdropTheme.Light => true,
        SystemBackdropTheme.Dark => false,
        _ when xamlRoot?.Content is FrameworkElement root && root.ActualTheme != ElementTheme.Default =>
            root.ActualTheme == ElementTheme.Light,
        _ => Application.Current.RequestedTheme == ApplicationTheme.Light,
    };

    private static void ApplyPalette(DesktopAcrylicController controller, bool isLight)
    {
        controller.Kind = DesktopAcrylicKind.Thin;
        if (isLight)
        {
            controller.TintColor = LightTintColor;
            controller.FallbackColor = LightFallbackColor;
            controller.TintOpacity = LightTintOpacity;
            controller.LuminosityOpacity = LightLuminosityOpacity;
        }
        else
        {
            controller.TintColor = DarkTintColor;
            controller.FallbackColor = DarkFallbackColor;
            controller.TintOpacity = DarkTintOpacity;
            controller.LuminosityOpacity = DarkLuminosityOpacity;
        }
    }

    private void ApplyConfiguration(
        DesktopAcrylicController controller,
        SystemBackdropConfiguration configuration,
        SystemBackdropConfiguration defaultConfiguration,
        XamlRoot? xamlRoot)
    {
        configuration.IsHighContrast = defaultConfiguration.IsHighContrast;
        configuration.HighContrastBackgroundColor = defaultConfiguration.HighContrastBackgroundColor;
        configuration.Theme = defaultConfiguration.Theme;
        configuration.IsInputActive = defaultConfiguration.IsInputActive || _keepActiveWhenDeactivated();

        if (defaultConfiguration.IsHighContrast)
        {
            controller.ResetProperties();
            return;
        }

        ApplyPalette(controller, IsLightTheme(defaultConfiguration.Theme, xamlRoot));
    }
}
