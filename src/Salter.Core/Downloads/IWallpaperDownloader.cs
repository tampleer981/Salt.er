using Salter.Core.Wallhaven;

namespace Salter.Core.Downloads;

/// <summary>Downloads full-resolution wallpapers to a local folder.</summary>
public interface IWallpaperDownloader
{
    /// <summary>
    /// Downloads <paramref name="wallpaper"/> to <c>wallhaven-&lt;id&gt;.&lt;ext&gt;</c> in <paramref name="folder"/>,
    /// creating the folder if needed. An existing file of that name is reused without downloading again. A failed
    /// or cancelled download leaves no partial file behind.
    /// </summary>
    /// <param name="wallpaper">The wallpaper to download.</param>
    /// <param name="folder">The destination folder.</param>
    /// <param name="ct">Cancels the download.</param>
    /// <returns>The full path of the downloaded file.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="folder"/> is blank, or the wallpaper id is not usable in a file name.
    /// </exception>
    /// <exception cref="WallhavenException">The image could not be fetched (network error, timeout or HTTP error).</exception>
    /// <exception cref="IOException">The file could not be written, for example because the disk is full.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<string> DownloadAsync(WallpaperSummary wallpaper, string folder, CancellationToken ct);
}
