using System.Globalization;
using Salter.Core.Wallhaven;

namespace Salter.Core.Feed;

/// <summary>Turns the feed's current selection into a wallhaven <see cref="SearchQuery"/>.</summary>
public sealed class FeedRequestFactory
{
    private const int UhdLong = 3840;
    private const int UhdShort = 2160;

    private readonly Func<(int Width, int Height)?> _primaryResolution;

    /// <summary>Creates a factory.</summary>
    /// <param name="primaryResolution">
    /// Returns the primary monitor's resolution in physical pixels, or <see langword="null"/> when unknown.
    /// Called on every <see cref="Create"/> so that monitor changes are picked up.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="primaryResolution"/> is <see langword="null"/>.</exception>
    public FeedRequestFactory(Func<(int Width, int Height)?> primaryResolution)
    {
        ArgumentNullException.ThrowIfNull(primaryResolution);
        _primaryResolution = primaryResolution;
    }

    /// <summary>Builds the query for one feed page.</summary>
    /// <param name="genre">The selected genre; ignored when <paramref name="searchText"/> is not blank.</param>
    /// <param name="searchText">Free text typed by the user; trimmed, and searched across general and anime when not blank.</param>
    /// <param name="sorting">The result ordering.</param>
    /// <param name="filters">The ratio, resolution and colour filters.</param>
    /// <param name="purity">The purity levels to include.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="seed">The random seed; kept only for <see cref="Sorting.Random"/>.</param>
    /// <param name="excludedTags">
    /// Tags whose wallpapers are left out, appended to the search text as <c>-tag</c> terms (see
    /// <see cref="TagExclusions"/>); <see langword="null"/> or empty for none.
    /// </param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="genre"/> or <paramref name="filters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is less than 1.</exception>
    public SearchQuery Create(
        Genre genre,
        string? searchText,
        Sorting sorting,
        FeedFilters filters,
        Purity purity,
        int page,
        string? seed,
        IEnumerable<string>? excludedTags = null)
    {
        ArgumentNullException.ThrowIfNull(genre);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        var text = searchText?.Trim();
        var (queryText, categories) = string.IsNullOrEmpty(text)
            ? (genre.Tag, genre.Categories)
            : (text, Categories.General | Categories.Anime);

        return new SearchQuery(
            TagExclusions.AppendTo(queryText, excludedTags),
            categories,
            purity,
            sorting,
            filters.Ratio,
            AtLeast(filters),
            filters.Color,
            page,
            sorting == Sorting.Random ? seed : null);
    }

    private string? AtLeast(FeedFilters filters)
    {
        (int Width, int Height)? minimum = filters.Resolution switch
        {
            ResolutionFilter.Native => _primaryResolution() is { Width: > 0, Height: > 0 } primary ? primary : null,
            ResolutionFilter.Uhd => (UhdLong, UhdShort),
            _ => null,
        };
        if (minimum is not { } size)
        {
            return null;
        }

        var (longSide, shortSide) = (Math.Max(size.Width, size.Height), Math.Min(size.Width, size.Height));
        var (width, height) = filters.Ratio switch
        {
            RatioFilter.Portrait9x16 => (shortSide, longSide),
            RatioFilter.Wide16x9 or RatioFilter.Ultrawide21x9 => (longSide, shortSide),
            // No ratio constraint: keep the monitor's own orientation.
            _ => (size.Width, size.Height),
        };
        return string.Create(CultureInfo.InvariantCulture, $"{width}x{height}");
    }
}
