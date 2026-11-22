using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace Salter.App.Views;

/// <summary>
/// Short, one-off animations built on implicit transitions, which the compositor runs off the UI thread and which
/// cost nothing once finished. All of them are skipped while the Windows "Animation effects" setting is off.
/// </summary>
internal static class Motion
{
    private static UISettings? _settings;

    /// <summary>Gets a value indicating whether the user allows animations; read on every use, so changes apply at once.</summary>
    public static bool Enabled
    {
        get
        {
            try
            {
                _settings ??= new UISettings();
                return _settings.AnimationsEnabled;
            }
            catch (COMException)
            {
                return true;
            }
        }
    }

    /// <summary>Fades an element in while it slides up into place.</summary>
    /// <param name="element">The element; its opacity and translation transitions are replaced.</param>
    /// <param name="offset">How far below its place it starts, in pixels; 0 only fades.</param>
    /// <param name="duration">How long the entrance takes.</param>
    public static void Enter(UIElement element, float offset, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.OpacityTransition = null;
        element.TranslationTransition = null;
        if (!Enabled)
        {
            element.Opacity = 1;
            element.Translation = Vector3.Zero;
            return;
        }

        // The start values are set without transitions; the end values then animate from them.
        element.Opacity = 0;
        element.Translation = new Vector3(0, offset, 0);
        element.OpacityTransition = new ScalarTransition { Duration = duration };
        element.TranslationTransition = new Vector3Transition
        {
            Duration = duration,
            Components = Vector3TransitionComponents.Y,
        };
        element.Opacity = 1;
        element.Translation = Vector3.Zero;
    }

    /// <summary>Changes an element's opacity through its <see cref="UIElement.OpacityTransition"/>, if any.</summary>
    /// <param name="element">The element.</param>
    /// <param name="opacity">The new opacity.</param>
    /// <param name="animate">
    /// <see langword="false"/> to change it at once, for example when a recycled element is reset; animations are
    /// also skipped while they are turned off in Windows.
    /// </param>
    public static void SetOpacity(UIElement element, double opacity, bool animate)
    {
        ArgumentNullException.ThrowIfNull(element);

        var transition = element.OpacityTransition;
        if (!animate || !Enabled)
        {
            element.OpacityTransition = null;
        }

        element.Opacity = opacity;
        element.OpacityTransition = transition;
    }

    /// <summary>Changes an element's uniform scale through its <see cref="UIElement.ScaleTransition"/>, if any.</summary>
    /// <param name="element">The element; it scales around its <see cref="UIElement.CenterPoint"/>.</param>
    /// <param name="scale">The new scale.</param>
    /// <param name="animate"><see langword="false"/> to change it at once; see <see cref="SetOpacity"/>.</param>
    public static void SetScale(UIElement element, float scale, bool animate)
    {
        ArgumentNullException.ThrowIfNull(element);

        var transition = element.ScaleTransition;
        if (!animate || !Enabled)
        {
            element.ScaleTransition = null;
        }

        element.Scale = new Vector3(scale, scale, 1);
        element.ScaleTransition = transition;
    }
}
