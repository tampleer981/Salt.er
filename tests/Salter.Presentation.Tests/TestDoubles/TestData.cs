using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Sample wallpapers and monitors shared by view model tests.</summary>
internal static class TestData
{
    public static MonitorInfo Display1 { get; } = new(@"\\?\DISPLAY1", "Display 1", 2560, 1440, IsPrimary: true);

    public static MonitorInfo Display2 { get; } = new(@"\\?\DISPLAY2", "Display 2", 1920, 1080, IsPrimary: false);

    public static WallpaperSummary Wallpaper(string id) =>
        new(
            id,
            new Uri($"https://wallhaven.cc/w/{id}"),
            new Uri($"https://w.wallhaven.cc/full/{id}.jpg"),
            new Uri($"https://th.wallhaven.cc/lg/{id}.jpg"),
            1920,
            1080,
            1024,
            "image/jpeg",
            "1.78",
            []);

    public static WallpaperDetails Details(string id, params string[] tags) => new(Wallpaper(id), tags, "uploader-" + id);
}
