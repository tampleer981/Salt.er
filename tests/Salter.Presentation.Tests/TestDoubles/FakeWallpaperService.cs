using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Wallpaper service with a configurable monitor list that records the wallpapers it sets.</summary>
internal sealed class FakeWallpaperService : IWallpaperService
{
    public List<MonitorInfo> Monitors { get; } = [];

    public List<(string FilePath, string? MonitorId)> Calls { get; } = [];

    /// <summary>When set, <see cref="SetAsync"/> fails with this exception.</summary>
    public Exception? Failure { get; set; }

    public IReadOnlyList<MonitorInfo> GetMonitors() => [.. Monitors];

    public Task SetAsync(string filePath, string? monitorId, CancellationToken ct)
    {
        Calls.Add((filePath, monitorId));
        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
