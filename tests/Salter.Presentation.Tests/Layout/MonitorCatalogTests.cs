using Salter.Presentation.Abstractions;
using Salter.Presentation.Layout;

namespace Salter.Presentation.Tests.Layout;

public class MonitorCatalogTests
{
    private static string Name(int number) => $"Display {number}";

    [Fact]
    public void Build_names_monitors_in_order_and_takes_size_from_bounds()
    {
        var result = MonitorCatalog.Build(
            [
                ("\\\\?\\DISPLAY#A", new PixelRect(-2560, 0, 2560, 1440)),
                ("\\\\?\\DISPLAY#B", new PixelRect(0, 0, 1920, 1080)),
            ],
            Name);

        Assert.Equal(
            [
                new MonitorInfo("\\\\?\\DISPLAY#A", "Display 1", 2560, 1440, IsPrimary: false),
                new MonitorInfo("\\\\?\\DISPLAY#B", "Display 2", 1920, 1080, IsPrimary: true),
            ],
            result);
    }

    [Fact]
    public void Build_marks_first_monitor_primary_when_none_is_at_origin()
    {
        var result = MonitorCatalog.Build(
            [("a", new PixelRect(10, 0, 800, 600)), ("b", new PixelRect(810, 0, 800, 600))],
            Name);

        Assert.Equal([true, false], result.Select(m => m.IsPrimary));
    }

    [Fact]
    public void Build_marks_only_one_monitor_primary_when_several_are_at_origin()
    {
        var result = MonitorCatalog.Build(
            [("a", new PixelRect(-800, 0, 800, 600)), ("b", new PixelRect(0, 0, 800, 600)), ("c", new PixelRect(0, 0, 800, 600))],
            Name);

        Assert.Equal([false, true, false], result.Select(m => m.IsPrimary));
    }

    [Fact]
    public void Build_skips_monitors_without_a_positive_size()
    {
        var result = MonitorCatalog.Build(
            [
                ("inactive", new PixelRect(0, 0, 0, 0)),
                ("flat", new PixelRect(1920, 0, 1280, 0)),
                ("inverted", new PixelRect(3200, 0, -10, 600)),
                ("b", new PixelRect(-1920, 0, 1920, 1080)),
                ("a", new PixelRect(0, 0, 2560, 1440)),
            ],
            Name);

        Assert.Equal(
            [
                new MonitorInfo("b", "Display 1", 1920, 1080, IsPrimary: false),
                new MonitorInfo("a", "Display 2", 2560, 1440, IsPrimary: true),
            ],
            result);
    }

    [Fact]
    public void Build_returns_empty_list_for_no_monitors()
    {
        Assert.Empty(MonitorCatalog.Build([], Name));
    }

    [Fact]
    public void Build_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => MonitorCatalog.Build(null!, Name));
        Assert.Throws<ArgumentNullException>(() => MonitorCatalog.Build([], null!));
    }
}
