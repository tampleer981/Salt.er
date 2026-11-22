namespace Salter.Core.Feed;

/// <summary>The fixed set of colour swatches offered in the feed's filter panel.</summary>
public static class ColorCatalog
{
    /// <summary>All swatches, in display order; each is one of the colours wallhaven can search for.</summary>
    public static IReadOnlyList<FilterColor> All { get; } =
    [
        new("red", "#cc0000"),
        new("orange", "#ff6600"),
        new("yellow", "#ffcc33"),
        new("green", "#77cc33"),
        new("teal", "#66cccc"),
        new("blue", "#0066cc"),
        new("violet", "#993399"),
        new("mono", "#999999"),
    ];
}
