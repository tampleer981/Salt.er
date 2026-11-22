using System.Globalization;
using System.Text;

namespace Salter.Core.Wallhaven;

/// <summary>Builds the relative wallhaven search URL for a <see cref="SearchQuery"/>.</summary>
public static class QueryStringBuilder
{
    /// <summary>Builds a relative URL such as <c>search?q=nature&amp;categories=100&amp;page=1</c>.</summary>
    /// <param name="q">The query.</param>
    /// <returns>The relative URL with all values percent-encoded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="q"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No category or no purity level is selected.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1.</exception>
    public static string Build(SearchQuery q)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentOutOfRangeException.ThrowIfLessThan(q.Page, 1, nameof(q));

        // The third digit is the unsupported "people" category and is always off.
        var categories = string.Concat(
            Bit(q.Categories.HasFlag(Categories.General)),
            Bit(q.Categories.HasFlag(Categories.Anime)),
            '0');
        var purity = string.Concat(
            Bit(q.Purity.HasFlag(Purity.Sfw)),
            Bit(q.Purity.HasFlag(Purity.Sketchy)),
            Bit(q.Purity.HasFlag(Purity.Nsfw)));
        if (categories == "000")
        {
            throw new ArgumentException("At least one category is required.", nameof(q));
        }

        if (purity == "000")
        {
            throw new ArgumentException("At least one purity level is required.", nameof(q));
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(q.Text))
        {
            parts.Add("q=" + Uri.EscapeDataString(q.Text));
        }

        parts.Add("categories=" + categories);
        parts.Add("purity=" + purity);
        parts.Add("sorting=" + SortingValue(q.Sorting));
        if (q.Sorting == Sorting.Toplist)
        {
            parts.Add("topRange=1M");
        }

        var ratio = RatioValue(q.Ratio);
        if (ratio is not null)
        {
            parts.Add("ratios=" + ratio);
        }

        if (!string.IsNullOrWhiteSpace(q.AtLeast))
        {
            parts.Add("atleast=" + Uri.EscapeDataString(q.AtLeast.Trim()));
        }

        var color = q.Color?.Trim().TrimStart('#');
        if (!string.IsNullOrEmpty(color))
        {
            parts.Add("colors=" + Uri.EscapeDataString(color));
        }

        if (q.Sorting == Sorting.Random && !string.IsNullOrEmpty(q.Seed))
        {
            parts.Add("seed=" + Uri.EscapeDataString(q.Seed));
        }

        parts.Add("page=" + q.Page.ToString(CultureInfo.InvariantCulture));
        return new StringBuilder("search?").AppendJoin('&', parts).ToString();
    }

    private static char Bit(bool set) => set ? '1' : '0';

    private static string SortingValue(Sorting sorting) => sorting switch
    {
        Sorting.Toplist => "toplist",
        Sorting.Latest => "date_added",
        Sorting.Hot => "hot",
        Sorting.Random => "random",
        _ => throw new ArgumentOutOfRangeException(nameof(sorting), sorting, "Unknown sorting."),
    };

    private static string? RatioValue(RatioFilter ratio) => ratio switch
    {
        RatioFilter.Any => null,
        RatioFilter.Wide16x9 => "16x9",
        RatioFilter.Ultrawide21x9 => "21x9",
        RatioFilter.Portrait9x16 => "9x16",
        _ => throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Unknown ratio."),
    };
}
