using Microsoft.UI.Xaml;
using Salter.Core.Settings;

namespace Salter.App.Services;

/// <summary>Applies the user's theme preference to the flyout's content.</summary>
internal sealed class ThemeService
{
    private readonly FrameworkElement _root;

    /// <summary>Creates the service for a window's root element.</summary>
    /// <param name="root">The element whose <see cref="FrameworkElement.RequestedTheme"/> is set.</param>
    public ThemeService(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
    }

    /// <summary>Applies a theme preference; <see cref="ThemePreference.System"/> follows the Windows app theme.</summary>
    /// <param name="preference">The preferred theme.</param>
    public void Apply(ThemePreference preference) => _root.RequestedTheme = preference switch
    {
        ThemePreference.Light => ElementTheme.Light,
        ThemePreference.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
