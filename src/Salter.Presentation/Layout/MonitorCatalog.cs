using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Layout;

/// <summary>Turns the monitors reported by the platform into the <see cref="MonitorInfo"/> list shown in the UI.</summary>
public static class MonitorCatalog
{
    /// <summary>
    /// Describes monitors in the order given, naming them by their 1-based position. Monitors without a positive width
    /// and height (inactive or phantom entries) are left out before numbering. The primary monitor is the first one
    /// whose top-left corner is the desktop origin (0, 0); when none is, the first monitor is treated as primary.
    /// </summary>
    /// <param name="monitors">Platform identifier and desktop bounds of each attached monitor, in platform order.</param>
    /// <param name="displayName">Returns the name for a 1-based monitor number, for example "Display 2".</param>
    /// <returns>One entry per monitor, with exactly one primary unless the list is empty.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<MonitorInfo> Build(
        IEnumerable<(string Id, PixelRect Bounds)> monitors,
        Func<int, string> displayName)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(displayName);

        var list = monitors.Where(m => m.Bounds.Width > 0 && m.Bounds.Height > 0).ToList();
        var primary = list.FindIndex(m => m.Bounds.X == 0 && m.Bounds.Y == 0);
        if (primary < 0)
        {
            primary = 0;
        }

        return list
            .Select((m, i) => new MonitorInfo(m.Id, displayName(i + 1), m.Bounds.Width, m.Bounds.Height, i == primary))
            .ToList();
    }
}
