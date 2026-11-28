using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace Salter.App.Views;

/// <summary>
/// Short, one-off animations built on implicit transitions, which the compositor runs off the UI thread and which
/// cost nothing once finished. All of them are skipped while the Windows "Animation effects" setting is off.
/// </summary>
/// <remarks>
/// This is the only place that sets implicit transitions, and only on elements of the app's own views (never on
/// control template parts: in Windows App SDK 1.8 an animated Opacity or Scale on a template part crashes
/// Microsoft.ui.xaml.dll while animation effects are on; see docs/design.md). Should the framework refuse a
/// transition, the element takes its final value at once instead.
/// </remarks>
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

        if (Enabled)
        {
            try
            {
                // The start values are set without transitions; the end values then animate from them.
                element.OpacityTransition = null;
                element.TranslationTransition = null;
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
                return;
            }
            catch (Exception ex) when (IsTransitionFailure(ex))
            {
                // Falls through to the final state without animation.
            }
        }

        ShowAtOnce(element);
    }

    /// <summary>Fades an element to an opacity, or sets it at once while animations are off.</summary>
    /// <param name="element">The element; its opacity transition is replaced.</param>
    /// <param name="opacity">The new opacity.</param>
    /// <param name="duration">How long the fade takes.</param>
    public static void Fade(UIElement element, double opacity, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (Enabled)
        {
            try
            {
                // Reused while its duration matches, so repeated hovers allocate nothing.
                if (element.OpacityTransition is not { } transition || transition.Duration != duration)
                {
                    element.OpacityTransition = new ScalarTransition { Duration = duration };
                }

                element.Opacity = opacity;
                return;
            }
            catch (Exception ex) when (IsTransitionFailure(ex))
            {
                // Falls through to setting the value without animation.
            }
        }

        SetOpacity(element, opacity);
    }

    /// <summary>Sets an element's opacity at once, without animation, for example when a recycled element is reset.</summary>
    /// <param name="element">The element; its opacity transition is removed.</param>
    /// <param name="opacity">The new opacity.</param>
    public static void SetOpacity(UIElement element, double opacity)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.OpacityTransition = null;
        element.Opacity = opacity;
    }

    private static void ShowAtOnce(UIElement element)
    {
        element.OpacityTransition = null;
        element.TranslationTransition = null;
        element.Opacity = 1;
        element.Translation = Vector3.Zero;
    }

    private static bool IsTransitionFailure(Exception ex) =>
        ex is COMException or InvalidOperationException or ArgumentException;
}
