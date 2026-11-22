using Salter.Core.Wallhaven;

namespace Salter.Core.Feed;

/// <summary>The minimum resolution of feed results.</summary>
public enum ResolutionFilter
{
    /// <summary>At least the primary monitor's resolution.</summary>
    Native,
    /// <summary>At least 3840 × 2160.</summary>
    Uhd,
    /// <summary>No minimum.</summary>
    Any,
}

/// <summary>The user's feed filters.</summary>
/// <param name="Ratio">The aspect ratio filter.</param>
/// <param name="Resolution">The minimum resolution.</param>
/// <param name="Color">Hex colour to match, or <see langword="null"/> for any.</param>
public sealed record FeedFilters(
    RatioFilter Ratio = RatioFilter.Any,
    ResolutionFilter Resolution = ResolutionFilter.Native,
    string? Color = null);
