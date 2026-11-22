using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Salter.Core.Wallhaven;

namespace Salter.Core.Downloads;

/// <summary>
/// <see cref="IWallpaperDownloader"/> that streams the image into a uniquely named <c>.part</c> file next to the
/// target and moves it into place only once complete. Uses the
/// <see cref="ServiceCollectionExtensions.WallhavenImagesClientName"/> client, which bypasses the API rate limiter.
/// </summary>
/// <remarks>
/// The whole download, headers and body, must finish within <see cref="DownloadDeadline"/> measured by the
/// injected <see cref="TimeProvider"/>; the client's own timeout covers only the response headers.
/// </remarks>
internal sealed partial class WallpaperDownloader(
    IHttpClientFactory httpClientFactory,
    TimeProvider time,
    ILogger<WallpaperDownloader>? logger = null) : IWallpaperDownloader
{
    /// <summary>The longest a single download may take, including the response body.</summary>
    internal static readonly TimeSpan DownloadDeadline = TimeSpan.FromSeconds(120);

    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    public async Task<string> DownloadAsync(WallpaperSummary wallpaper, string folder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(wallpaper);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (wallpaper.Id.Length == 0 || !wallpaper.Id.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("The wallpaper id is not usable in a file name.", nameof(wallpaper));
        }

        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"wallhaven-{wallpaper.Id}.{wallpaper.Extension}");
        if (File.Exists(target))
        {
            return target;
        }

        var temp = $"{target}.{Guid.NewGuid():N}.part";
        try
        {
            using (var deadline = new CancellationTokenSource(DownloadDeadline, time))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token))
            {
                await DownloadToFileAsync(wallpaper.ImageUrl, temp, linked.Token, ct).ConfigureAwait(false);
            }

            File.Move(temp, target, overwrite: false);
        }
        catch (IOException) when (File.Exists(target) && !ct.IsCancellationRequested)
        {
            // Another download of the same wallpaper finished first; its complete file wins.
            TryDelete(temp);
            return target;
        }
        catch (Exception ex)
        {
            if (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                LogDownloadFailed(_logger, wallpaper.Id, ex);
            }

            TryDelete(temp);
            throw;
        }

        return target;
    }

    /// <summary>Streams the image into <paramref name="temp"/>, mapping transport failures and timeouts to <see cref="WallhavenException"/>.</summary>
    /// <param name="imageUrl">The image to fetch.</param>
    /// <param name="temp">The temporary file to write.</param>
    /// <param name="ct">The caller's token linked with the download deadline.</param>
    /// <param name="callerToken">The caller's token alone, to tell cancellation from a timeout.</param>
    private async Task DownloadToFileAsync(Uri imageUrl, string temp, CancellationToken ct, CancellationToken callerToken)
    {
        using var http = httpClientFactory.CreateClient(ServiceCollectionExtensions.WallhavenImagesClientName);
        try
        {
            using var response = await http.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw response.StatusCode == HttpStatusCode.NotFound
                    ? new WallhavenException(WallhavenErrorKind.NotFound, "The image was not found on wallhaven.")
                    : new WallhavenException(
                        WallhavenErrorKind.Server, $"wallhaven returned HTTP {(int)response.StatusCode} for the image.");
            }

            var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var file = new FileStream(
                    temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                await using (file.ConfigureAwait(false))
                {
                    await source.CopyToAsync(file, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ToNetworkError(ex, callerToken) is { } network)
        {
            throw network;
        }
    }

    /// <summary>Maps transport failures and timeouts to <see cref="WallhavenErrorKind.Network"/>; anything else passes through.</summary>
    /// <param name="ex">The failure.</param>
    /// <param name="callerToken">The caller's token; cancellation it requested is not a timeout.</param>
    private static WallhavenException? ToNetworkError(Exception ex, CancellationToken callerToken) => ex switch
    {
        // HttpIOException is an IOException raised by the connection, not by the disk.
        HttpRequestException or HttpIOException => new WallhavenException(
            WallhavenErrorKind.Network, "The image could not be downloaded from wallhaven.", ex),
        OperationCanceledException when !callerToken.IsCancellationRequested => new WallhavenException(
            WallhavenErrorKind.Network, "The image download timed out.", ex),
        _ => null,
    };

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogCleanupFailed(_logger, path, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloading wallpaper {Id} failed.")]
    private static partial void LogDownloadFailed(ILogger logger, string id, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the partial download {Path}.")]
    private static partial void LogCleanupFailed(ILogger logger, string path, Exception exception);
}
