namespace Salter.Presentation.Abstractions;

/// <summary>Sets the desktop wallpaper and enumerates monitors.</summary>
public interface IWallpaperService
{
    /// <summary>Returns the monitors currently attached.</summary>
    /// <returns>A snapshot of the attached monitors.</returns>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>Applies an image file as the wallpaper.</summary>
    /// <param name="filePath">Absolute path of the image file.</param>
    /// <param name="monitorId">Target monitor id, or <see langword="null"/> for all monitors.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>A task that completes when the wallpaper has been applied.</returns>
    /// <exception cref="InvalidOperationException">
    /// The platform could not apply the wallpaper, for example because the monitor was detached or the desktop
    /// wallpaper API failed. Implementations wrap platform-specific errors in this exception.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task SetAsync(string filePath, string? monitorId, CancellationToken ct);
}
