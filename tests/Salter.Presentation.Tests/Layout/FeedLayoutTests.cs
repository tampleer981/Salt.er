using Salter.Presentation.Layout;

namespace Salter.Presentation.Tests.Layout;

public class FeedLayoutTests
{
    [Theory]
    [InlineData(1.0, 372)]
    [InlineData(1.25, 465)]
    [InlineData(1.5, 558)]
    [InlineData(1.75, 651)]
    [InlineData(2.0, 744)]
    [InlineData(1.1, 410)] // 409.2 rounds up so the image is never upscaled
    public void DecodePixelWidth_scales_the_flyout_width(double scale, int expected)
    {
        Assert.Equal(expected, FeedLayout.DecodePixelWidth(scale));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DecodePixelWidth_rejects_invalid_scales(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeedLayout.DecodePixelWidth(scale));
    }

    [Theory]
    [InlineData(0, 500, 5000, false)] // top of a long list
    [InlineData(3899, 500, 5000, false)] // 601 px left below the viewport
    [InlineData(3900, 500, 5000, true)] // exactly 600 px left
    [InlineData(4500, 500, 5000, true)] // at the end
    [InlineData(0, 500, 400, true)] // content shorter than the viewport
    [InlineData(0, 500, 1100, true)] // content barely taller than the viewport
    public void IsNearEnd_triggers_within_600_px_of_the_end(double offset, double viewport, double extent, bool expected)
    {
        Assert.Equal(expected, FeedLayout.IsNearEnd(offset, viewport, extent));
    }

    [Fact]
    public void IsNearEnd_is_false_before_layout()
    {
        Assert.False(FeedLayout.IsNearEnd(0, 0, 0));
    }
}
