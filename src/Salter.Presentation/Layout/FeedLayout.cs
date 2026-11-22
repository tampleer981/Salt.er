namespace Salter.Presentation.Layout;

/// <summary>Sizes and thresholds of the wallpaper feed.</summary>
public static class FeedLayout
{
    /// <summary>The width of a thumbnail in logical pixels: the full flyout width.</summary>
    public const double ThumbnailWidth = 372;

    /// <summary>How close to the end of the list, in logical pixels, the next page starts loading.</summary>
    public const double LoadMoreThreshold = 600;

    /// <summary>Gets the width to decode thumbnails at, so they are sharp without wasting memory.</summary>
    /// <param name="rasterizationScale">The display scale, for example 1.5 at 150 %.</param>
    /// <returns>The thumbnail width in physical pixels, rounded up.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rasterizationScale"/> is not a positive finite number.
    /// </exception>
    public static int DecodePixelWidth(double rasterizationScale)
    {
        if (!double.IsFinite(rasterizationScale) || rasterizationScale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rasterizationScale), rasterizationScale, "The scale must be a positive finite number.");
        }

        // Rounded to 4 decimals first, so floating point noise such as 465.00000001 does not add a pixel.
        return (int)Math.Ceiling(Math.Round(ThumbnailWidth * rasterizationScale, 4));
    }

    /// <summary>Tells whether the list is scrolled close enough to its end to load the next page.</summary>
    /// <param name="verticalOffset">The scroll offset.</param>
    /// <param name="viewportHeight">The height of the visible area.</param>
    /// <param name="extentHeight">The height of the whole content.</param>
    /// <returns>
    /// <see langword="true"/> if at most <see cref="LoadMoreThreshold"/> pixels of content remain below the visible
    /// area; <see langword="false"/> before the first layout, when the viewport has no height.
    /// </returns>
    public static bool IsNearEnd(double verticalOffset, double viewportHeight, double extentHeight) =>
        viewportHeight > 0 && extentHeight - (verticalOffset + viewportHeight) <= LoadMoreThreshold;
}
