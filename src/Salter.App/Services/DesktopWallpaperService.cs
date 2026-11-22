using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Salter.App.Interop;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Layout;

namespace Salter.App.Services;

/// <summary>
/// Sets the wallpaper through the shell's <c>IDesktopWallpaper</c>. Each call runs on its own short-lived STA thread,
/// so the COM object never lives on, or is marshalled to, the UI thread.
/// </summary>
internal sealed partial class DesktopWallpaperService : IWallpaperService
{
    private const int SOk = 0;

    private readonly ILocalizer _localizer;
    private readonly ILogger<DesktopWallpaperService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="localizer">Supplies the display names.</param>
    /// <param name="logger">Receives monitor enumeration failures.</param>
    public DesktopWallpaperService(ILocalizer localizer, ILogger<DesktopWallpaperService> logger)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(logger);
        _localizer = localizer;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Blocks the caller briefly while the shell is queried. Monitors whose bounds cannot be read (for example while
    /// they are being disconnected) are left out; if the query fails altogether the failure is logged and the list is
    /// empty, so callers fall back to "all displays".
    /// </remarks>
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        IReadOnlyList<(string Id, PixelRect Bounds)> monitors;
        try
        {
            monitors = RunOnSta(ReadMonitors, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            LogMonitorsUnavailable(ex);
            return [];
        }

        return MonitorCatalog.Build(monitors, number => _localizer.Format("Monitor_DisplayName", number));
    }

    /// <inheritdoc/>
    /// <remarks>Uses the "fill" position, which applies to every monitor.</remarks>
    public Task SetAsync(string filePath, string? monitorId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        return RunOnSta(
            wallpaper =>
            {
                ThrowIfFailed(wallpaper.SetPosition(DesktopWallpaperPosition.Fill), "Setting the wallpaper position");
                ThrowIfFailed(wallpaper.SetWallpaper(monitorId, filePath), "Setting the wallpaper");
                return true;
            },
            ct);
    }

    private static List<(string Id, PixelRect Bounds)> ReadMonitors(IDesktopWallpaper wallpaper)
    {
        ThrowIfFailed(wallpaper.GetMonitorDevicePathCount(out var count), "Counting monitors");

        var monitors = new List<(string Id, PixelRect Bounds)>((int)Math.Min(count, 64));
        for (uint i = 0; i < count; i++)
        {
            // Inactive monitors (or ones that went away between the calls) fail or report S_FALSE; skip them.
            // MonitorCatalog drops entries without a positive size.
            if (wallpaper.GetMonitorDevicePathAt(i, out var id) < 0 || string.IsNullOrEmpty(id)
                || wallpaper.GetMonitorRECT(id, out var rect) != SOk)
            {
                continue;
            }

            monitors.Add((id, new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)));
        }

        return monitors;
    }

    private static void ThrowIfFailed(int hr, string operation)
    {
        if (hr < 0)
        {
            throw new InvalidOperationException($"{operation} failed (HRESULT 0x{hr:X8}).");
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> against a new <c>IDesktopWallpaper</c> on a dedicated STA thread. The task
    /// fails with <see cref="InvalidOperationException"/> when the shell object reports an error, and with any other
    /// exception unchanged; no exception escapes the thread.
    /// </summary>
    private static Task<T> RunOnSta<T>(Func<IDesktopWallpaper, T> action, CancellationToken ct)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            if (ct.IsCancellationRequested)
            {
                completion.TrySetCanceled(ct);
                return;
            }

            try
            {
                var wallpaper = DesktopWallpaperFactory.Create();
                try
                {
                    completion.TrySetResult(action(wallpaper));
                }
                finally
                {
                    DesktopWallpaperFactory.Release(wallpaper);
                }
            }
            catch (InvalidOperationException ex)
            {
                completion.TrySetException(ex);
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                // A shell without the interface (or a broken registration) fails the cast or the COM runtime call.
                completion.TrySetException(new InvalidOperationException(
                    $"The desktop wallpaper API failed (HRESULT 0x{ex.HResult:X8}).", ex));
            }
            catch (Exception ex)
            {
                // Nothing may escape a raw thread (it would end the process); the awaiting caller sees it instead.
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Salt.er desktop wallpaper",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not enumerate monitors.")]
    private partial void LogMonitorsUnavailable(Exception exception);
}
