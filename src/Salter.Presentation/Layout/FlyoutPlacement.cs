namespace Salter.Presentation.Layout;

/// <summary>Computes where the tray flyout is shown relative to the taskbar.</summary>
public static class FlyoutPlacement
{
    /// <summary>Infers the taskbar edge from the gap between a monitor and its work area.</summary>
    /// <param name="monitorBounds">Full monitor bounds.</param>
    /// <param name="workArea">Monitor area not covered by the taskbar.</param>
    /// <returns>
    /// The edge with the largest positive gap; <see cref="TaskbarEdge.Bottom"/> when there is no gap or gaps are equal.
    /// </returns>
    public static TaskbarEdge DetectTaskbarEdge(PixelRect monitorBounds, PixelRect workArea)
    {
        var bottom = monitorBounds.Bottom - workArea.Bottom;
        var top = workArea.Y - monitorBounds.Y;
        var left = workArea.X - monitorBounds.X;
        var right = monitorBounds.Right - workArea.Right;

        var edge = TaskbarEdge.Bottom;
        var best = Math.Max(bottom, 0);

        if (top > best)
        {
            edge = TaskbarEdge.Top;
            best = top;
        }

        if (left > best)
        {
            edge = TaskbarEdge.Left;
            best = left;
        }

        if (right > best)
        {
            edge = TaskbarEdge.Right;
        }

        return edge;
    }

    /// <summary>Limits the flyout height so it fits the work area with the margin above and below.</summary>
    /// <param name="workArea">Work area of the monitor holding the tray, in physical pixels.</param>
    /// <param name="height">Preferred flyout height in physical pixels.</param>
    /// <param name="margin">Distance kept from the work area edges, in physical pixels.</param>
    /// <returns>
    /// <paramref name="height"/>, or the work area height minus twice the margin if that is smaller; at least 1.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="height"/> is not positive, or <paramref name="margin"/> is negative.
    /// </exception>
    public static int FitHeight(PixelRect workArea, int height, int margin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(margin);

        return Math.Max(1, Math.Min(height, workArea.Height - (2 * margin)));
    }

    /// <summary>Computes the flyout rectangle, anchored to the corner nearest the tray.</summary>
    /// <param name="monitorBounds">Full bounds of the monitor holding the tray, in physical pixels.</param>
    /// <param name="workArea">Work area of that monitor, in physical pixels.</param>
    /// <param name="width">Flyout width in physical pixels.</param>
    /// <param name="height">Flyout height in physical pixels.</param>
    /// <param name="margin">Distance kept from the work area edges, in physical pixels.</param>
    /// <returns>
    /// The flyout rectangle. Its size is never changed; if it is larger than the work area, the position is
    /// clamped so the top-left corner stays inside the work area.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive, or <paramref name="margin"/> is negative.
    /// </exception>
    public static PixelRect Compute(PixelRect monitorBounds, PixelRect workArea, int width, int height, int margin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(margin);

        var edge = DetectTaskbarEdge(monitorBounds, workArea);

        var rightAligned = workArea.Right - margin - width;
        var leftAligned = workArea.X + margin;
        var bottomAligned = workArea.Bottom - margin - height;
        var topAligned = workArea.Y + margin;

        var (x, y) = edge switch
        {
            TaskbarEdge.Top => (rightAligned, topAligned),
            TaskbarEdge.Left => (leftAligned, bottomAligned),
            _ => (rightAligned, bottomAligned),
        };

        return new PixelRect(Math.Max(x, workArea.X), Math.Max(y, workArea.Y), width, height);
    }
}
