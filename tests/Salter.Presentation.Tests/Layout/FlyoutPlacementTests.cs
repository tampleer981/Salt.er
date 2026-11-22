using Salter.Presentation.Layout;

namespace Salter.Presentation.Tests.Layout;

public class FlyoutPlacementTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(0, 0, 1920, 1032, 1536, 360)] // taskbar bottom
    [InlineData(0, 48, 1920, 1032, 1536, 60)] // taskbar top
    [InlineData(48, 0, 1872, 1080, 60, 408)] // taskbar left
    [InlineData(0, 0, 1872, 1080, 1488, 408)] // taskbar right
    public void FlyoutPlacement_positions_next_to_taskbar(int wx, int wy, int ww, int wh, int x, int y)
    {
        var result = FlyoutPlacement.Compute(Monitor, new PixelRect(wx, wy, ww, wh), 372, 660, 12);

        Assert.Equal(new PixelRect(x, y, 372, 660), result);
    }

    [Fact]
    public void FlyoutPlacement_handles_secondary_monitor_with_scaling()
    {
        var bounds = new PixelRect(-2560, 0, 2560, 1440);
        var work = new PixelRect(-2560, 0, 2560, 1392);

        var result = FlyoutPlacement.Compute(bounds, work, 558, 990, 18);

        Assert.Equal(new PixelRect(-576, 384, 558, 990), result);
    }

    [Fact]
    public void FlyoutPlacement_without_gap_defaults_to_bottom()
    {
        var work = new PixelRect(0, 0, 1920, 1080);

        var result = FlyoutPlacement.Compute(work, work, 372, 660, 12);

        Assert.Equal(new PixelRect(1536, 408, 372, 660), result);
    }

    [Fact]
    public void FlyoutPlacement_keeps_top_left_inside_tiny_work_area()
    {
        var bounds = new PixelRect(0, 0, 800, 648);
        var work = new PixelRect(0, 0, 800, 600);

        var result = FlyoutPlacement.Compute(bounds, work, 372, 660, 12);

        Assert.Equal(new PixelRect(416, 0, 372, 660), result);
        Assert.True(result.X >= work.X && result.Y >= work.Y);
    }

    [Fact]
    public void FlyoutPlacement_clamps_to_work_area_when_flyout_is_wider_than_it()
    {
        var bounds = new PixelRect(100, 50, 300, 648);
        var work = new PixelRect(100, 50, 300, 600);

        var result = FlyoutPlacement.Compute(bounds, work, 372, 300, 12);

        Assert.Equal(new PixelRect(100, 338, 372, 300), result);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1032, TaskbarEdge.Bottom)]
    [InlineData(0, 48, 1920, 1032, TaskbarEdge.Top)]
    [InlineData(48, 0, 1872, 1080, TaskbarEdge.Left)]
    [InlineData(0, 0, 1872, 1080, TaskbarEdge.Right)]
    [InlineData(0, 0, 1920, 1080, TaskbarEdge.Bottom)]
    [InlineData(0, 40, 1920, 1000, TaskbarEdge.Bottom)] // equal gaps: Bottom wins the tie
    public void DetectTaskbarEdge_infers_edge_from_gap(int wx, int wy, int ww, int wh, TaskbarEdge expected)
    {
        var result = FlyoutPlacement.DetectTaskbarEdge(Monitor, new PixelRect(wx, wy, ww, wh));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectTaskbarEdge_picks_largest_gap()
    {
        var edge = FlyoutPlacement.DetectTaskbarEdge(Monitor, new PixelRect(10, 0, 1900, 1020));

        Assert.Equal(TaskbarEdge.Bottom, edge);
    }

    [Theory]
    [InlineData(0, 100, 12)]
    [InlineData(100, 0, 12)]
    [InlineData(-1, 100, 12)]
    [InlineData(100, 100, -1)]
    public void Compute_rejects_invalid_arguments(int width, int height, int margin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlyoutPlacement.Compute(Monitor, Monitor, width, height, margin));
    }

    [Fact]
    public void FitHeight_caps_height_to_work_area_minus_margins()
    {
        // 1366x768 at 125 %: 660 DIP = 825 px, margin 12 DIP = 15 px, 40 px taskbar.
        var bounds = new PixelRect(0, 0, 1366, 768);
        var work = new PixelRect(0, 0, 1366, 728);

        var height = FlyoutPlacement.FitHeight(work, 825, 15);
        var result = FlyoutPlacement.Compute(bounds, work, 465, height, 15);

        Assert.Equal(698, height);
        Assert.Equal(new PixelRect(886, 15, 465, 698), result);
        Assert.True(result.Y >= work.Y && result.Bottom <= work.Bottom);
    }

    [Fact]
    public void FitHeight_keeps_height_that_fits()
    {
        var height = FlyoutPlacement.FitHeight(new PixelRect(0, 0, 1920, 1032), 660, 12);

        Assert.Equal(660, height);
    }

    [Fact]
    public void FitHeight_never_returns_less_than_one()
    {
        var height = FlyoutPlacement.FitHeight(new PixelRect(0, 0, 800, 20), 660, 12);

        Assert.Equal(1, height);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(100, -1)]
    public void FitHeight_rejects_invalid_arguments(int height, int margin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FlyoutPlacement.FitHeight(Monitor, height, margin));
    }
}
