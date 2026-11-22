using Microsoft.UI;
using Microsoft.UI.Windowing;
using Salter.App.Interop;
using Salter.Presentation.Layout;
using Windows.Graphics;

namespace Salter.App.Services;

/// <summary>Computes the flyout's bounds next to the taskbar of the monitor that shows the tray icon.</summary>
internal static class FlyoutPositioner
{
    private const int WidthDip = 372;
    private const int HeightDip = 660;
    private const int MarginDip = 12;
    private const double DefaultDpi = 96;

    /// <summary>Computes the flyout bounds in physical screen pixels.</summary>
    /// <param name="trayIconBounds">
    /// The tray icon's bounds, or <see langword="null"/> when unknown; the monitor under the cursor is used then.
    /// </param>
    /// <returns>The bounds to pass to <see cref="AppWindow.MoveAndResize(RectInt32)"/>.</returns>
    public static RectInt32 Compute(RectInt32? trayIconBounds)
    {
        var area = trayIconBounds is { } icon
            ? DisplayArea.GetFromRect(icon, DisplayAreaFallback.Nearest)
            : DisplayArea.GetFromPoint(CursorPosition(), DisplayAreaFallback.Nearest);
        var scale = ScaleOf(area);
        var workArea = ToPixelRect(area.WorkArea);
        var margin = ToPixels(MarginDip, scale);
        // Small or highly scaled screens cannot fit the full height; the flyout then shrinks to stay inside.
        var height = FlyoutPlacement.FitHeight(workArea, ToPixels(HeightDip, scale), margin);

        var bounds = FlyoutPlacement.Compute(
            ToPixelRect(area.OuterBounds), workArea, ToPixels(WidthDip, scale), height, margin);
        return new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static PointInt32 CursorPosition() =>
        NativeMethods.GetCursorPos(out var point) ? new PointInt32(point.X, point.Y) : default;

    private static double ScaleOf(DisplayArea area)
    {
        var monitor = Win32Interop.GetMonitorFromDisplayId(area.DisplayId);
        var result = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MonitorEffectiveDpi, out var dpiX, out _);
        return result == 0 && dpiX > 0 ? dpiX / DefaultDpi : 1;
    }

    private static int ToPixels(int dip, double scale) => (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero);

    private static PixelRect ToPixelRect(RectInt32 rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
