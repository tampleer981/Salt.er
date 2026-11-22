using Salter.Core.Feed;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Feed;

public class FeedRequestFactoryTests
{
    private static readonly FeedRequestFactory Factory = new(() => (2560, 1440));

    private static Genre GenreOf(string key) => GenreCatalog.All.Single(g => g.Key == key);

    private static SearchQuery Create(
        string genre = "all",
        string? text = null,
        Sorting sorting = Sorting.Toplist,
        FeedFilters? filters = null,
        Purity purity = Purity.Sfw,
        int page = 1,
        string? seed = null,
        FeedRequestFactory? factory = null) =>
        (factory ?? Factory).Create(GenreOf(genre), text, sorting, filters ?? new FeedFilters(), purity, page, seed);

    [Fact]
    public void Catalog_lists_genres_in_order_with_all_as_default()
    {
        Assert.Equal(
            ["all", "nature", "anime", "space", "city", "architecture", "abstract", "cars", "minimal"],
            GenreCatalog.All.Select(g => g.Key));
        Assert.Same(GenreCatalog.All[0], GenreCatalog.Default);
        Assert.All(GenreCatalog.All, g => Assert.DoesNotContain("people", g.Key, StringComparison.Ordinal));
    }

    [Fact]
    public void Tag_genre_searches_tag_in_general()
    {
        var query = Create("nature");

        Assert.Equal("nature", query.Text);
        Assert.Equal(Categories.General, query.Categories);
    }

    [Theory]
    [InlineData("all", Categories.General | Categories.Anime)]
    [InlineData("anime", Categories.Anime)]
    public void Category_genres_have_no_text(string genre, Categories expected)
    {
        var query = Create(genre);

        Assert.Null(query.Text);
        Assert.Equal(expected, query.Categories);
    }

    [Fact]
    public void Search_text_is_trimmed_and_overrides_genre()
    {
        var query = Create("space", text: "  cyberpunk ");

        Assert.Equal("cyberpunk", query.Text);
        Assert.Equal(Categories.General | Categories.Anime, query.Categories);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_search_text_falls_back_to_genre(string? text)
    {
        var query = Create("city", text: text);

        Assert.Equal("city", query.Text);
        Assert.Equal(Categories.General, query.Categories);
    }

    [Theory]
    [InlineData(ResolutionFilter.Native, RatioFilter.Any, "2560x1440")]
    [InlineData(ResolutionFilter.Native, RatioFilter.Wide16x9, "2560x1440")]
    [InlineData(ResolutionFilter.Native, RatioFilter.Portrait9x16, "1440x2560")]
    [InlineData(ResolutionFilter.Uhd, RatioFilter.Any, "3840x2160")]
    [InlineData(ResolutionFilter.Uhd, RatioFilter.Ultrawide21x9, "3840x2160")]
    [InlineData(ResolutionFilter.Uhd, RatioFilter.Portrait9x16, "2160x3840")]
    [InlineData(ResolutionFilter.Any, RatioFilter.Any, null)]
    [InlineData(ResolutionFilter.Any, RatioFilter.Portrait9x16, null)]
    public void Resolution_filter_sets_minimum(ResolutionFilter resolution, RatioFilter ratio, string? expected)
    {
        var query = Create(filters: new FeedFilters(ratio, resolution));

        Assert.Equal(expected, query.AtLeast);
        Assert.Equal(ratio, query.Ratio);
    }

    [Fact]
    public void Native_with_unknown_primary_has_no_minimum()
    {
        var query = Create(filters: new FeedFilters(Resolution: ResolutionFilter.Native), factory: new(() => null));

        Assert.Null(query.AtLeast);
    }

    [Fact]
    public void Native_on_portrait_monitor_with_landscape_ratio_uses_landscape_minimum()
    {
        var portrait = new FeedRequestFactory(() => (1440, 2560));

        Assert.Equal("2560x1440", Create(filters: new FeedFilters(RatioFilter.Wide16x9), factory: portrait).AtLeast);
        Assert.Equal("1440x2560", Create(filters: new FeedFilters(RatioFilter.Any), factory: portrait).AtLeast);
    }

    [Fact]
    public void Primary_resolution_is_read_on_each_call()
    {
        (int, int)? primary = (1920, 1080);
        var factory = new FeedRequestFactory(() => primary);

        Assert.Equal("1920x1080", Create(factory: factory).AtLeast);
        primary = (3440, 1440);
        Assert.Equal("3440x1440", Create(factory: factory).AtLeast);
    }

    [Fact]
    public void Color_purity_sorting_and_page_pass_through()
    {
        var query = Create(
            sorting: Sorting.Hot,
            filters: new FeedFilters(Color: "663399"),
            purity: Purity.Sfw | Purity.Sketchy,
            page: 3);

        Assert.Equal("663399", query.Color);
        Assert.Equal(Purity.Sfw | Purity.Sketchy, query.Purity);
        Assert.Equal(Sorting.Hot, query.Sorting);
        Assert.Equal(3, query.Page);
    }

    [Fact]
    public void Random_keeps_seed()
    {
        Assert.Equal("abc123", Create(sorting: Sorting.Random, seed: "abc123").Seed);
    }

    [Theory]
    [InlineData(Sorting.Toplist)]
    [InlineData(Sorting.Latest)]
    [InlineData(Sorting.Hot)]
    public void Non_random_drops_seed(Sorting sorting)
    {
        Assert.Null(Create(sorting: sorting, seed: "abc123").Seed);
    }

    [Fact]
    public void Page_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(page: 0));
    }
}
