using Salter.Core.Wallhaven;

namespace Salter.Core.Feed;

/// <summary>The fixed set of genres offered in the feed.</summary>
public static class GenreCatalog
{
    /// <summary>All genres, in display order. "All" and "Anime" select categories; the rest search a tag in general.</summary>
    public static IReadOnlyList<Genre> All { get; } =
    [
        new("all", null, Categories.General | Categories.Anime),
        TagGenre("nature"),
        new("anime", null, Categories.Anime),
        TagGenre("space"),
        TagGenre("city"),
        TagGenre("architecture"),
        TagGenre("abstract"),
        TagGenre("cars"),
        TagGenre("minimal"),
    ];

    /// <summary>The genre selected initially: <c>all</c>.</summary>
    public static Genre Default => All[0];

    private static Genre TagGenre(string key) => new(key, key, Categories.General);
}
