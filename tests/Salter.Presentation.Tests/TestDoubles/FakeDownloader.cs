using Salter.Core.Downloads;
using Salter.Core.Wallhaven;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Downloader that records requests and returns the path a real download would produce.</summary>
internal sealed class FakeDownloader : IWallpaperDownloader
{
    private TaskCompletionSource? _gate;

    public List<(WallpaperSummary Wallpaper, string Folder)> Calls { get; } = [];

    /// <summary>When set, downloads fail with this exception.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Makes downloads wait until the returned source is completed.</summary>
    public TaskCompletionSource Hold() => _gate = new TaskCompletionSource();

    public async Task<string> DownloadAsync(WallpaperSummary wallpaper, string folder, CancellationToken ct)
    {
        Calls.Add((wallpaper, folder));
        if (_gate is not null)
        {
            await _gate.Task.WaitAsync(ct);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        return Path.Combine(folder, $"wallhaven-{wallpaper.Id}.{wallpaper.Extension}");
    }
}
