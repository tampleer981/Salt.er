using Salter.Core.Feed;

namespace Salter.Core.Tests.Feed;

public class ColorCatalogTests
{
    [Fact]
    public void Catalog_lists_the_swatches_in_order()
    {
        Assert.Equal(
            ["red", "orange", "yellow", "green", "teal", "blue", "violet", "mono"],
            ColorCatalog.All.Select(c => c.Key));
    }

    [Fact]
    public void Every_swatch_is_a_distinct_wallhaven_hex_colour()
    {
        Assert.All(ColorCatalog.All, c => Assert.Matches("^#[0-9a-f]{6}$", c.Hex));
        Assert.Equal(ColorCatalog.All.Count, ColorCatalog.All.Select(c => c.Hex).Distinct(StringComparer.Ordinal).Count());
    }
}
