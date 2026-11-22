namespace Salter.Core.Wallhaven;

/// <summary>The wallhaven categories to search. The "people" category is intentionally not supported.</summary>
[Flags]
public enum Categories
{
    /// <summary>No category selected (invalid for a query).</summary>
    None = 0,
    /// <summary>The general category.</summary>
    General = 1,
    /// <summary>The anime category.</summary>
    Anime = 2,
}

/// <summary>The wallhaven purity levels to search.</summary>
[Flags]
public enum Purity
{
    /// <summary>Safe for work.</summary>
    Sfw = 1,
    /// <summary>Sketchy content.</summary>
    Sketchy = 2,
    /// <summary>Not safe for work (requires an API key).</summary>
    Nsfw = 4,
}

/// <summary>The result ordering.</summary>
public enum Sorting
{
    /// <summary>Most popular over the last month.</summary>
    Toplist,
    /// <summary>Most recently added.</summary>
    Latest,
    /// <summary>Currently hot.</summary>
    Hot,
    /// <summary>Random order, stable for a given seed.</summary>
    Random,
}

/// <summary>An aspect ratio filter.</summary>
public enum RatioFilter
{
    /// <summary>No ratio filter.</summary>
    Any,
    /// <summary>16:9.</summary>
    Wide16x9,
    /// <summary>21:9.</summary>
    Ultrawide21x9,
    /// <summary>9:16.</summary>
    Portrait9x16,
}

/// <summary>A wallhaven search request.</summary>
/// <param name="Text">Free-text query, or <see langword="null"/> for none.</param>
/// <param name="Categories">The categories to include; at least one is required.</param>
/// <param name="Purity">The purity levels to include; at least one is required.</param>
/// <param name="Sorting">The result ordering.</param>
/// <param name="Ratio">The aspect ratio filter.</param>
/// <param name="AtLeast">Minimum resolution such as <c>2560x1440</c>, or <see langword="null"/>.</param>
/// <param name="Color">Hex colour with or without a leading <c>#</c>, or <see langword="null"/>.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="Seed">The seed for <see cref="Sorting.Random"/>, or <see langword="null"/>.</param>
public sealed record SearchQuery(
    string? Text,
    Categories Categories,
    Purity Purity,
    Sorting Sorting,
    RatioFilter Ratio,
    string? AtLeast,
    string? Color,
    int Page = 1,
    string? Seed = null);
