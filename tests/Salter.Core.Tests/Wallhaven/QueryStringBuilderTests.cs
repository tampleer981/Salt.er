using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Wallhaven;

public class QueryStringBuilderTests
{
    private static SearchQuery Query(
        string? text = null,
        Categories categories = Categories.General,
        Purity purity = Purity.Sfw,
        Sorting sorting = Sorting.Latest,
        RatioFilter ratio = RatioFilter.Any,
        string? atLeast = null,
        string? color = null,
        int page = 1,
        string? seed = null) =>
        new(text, categories, purity, sorting, ratio, atLeast, color, page, seed);

    [Fact]
    public void Toplist_general_sfw_with_text()
    {
        var result = QueryStringBuilder.Build(Query("nature", sorting: Sorting.Toplist));

        Assert.Equal("search?q=nature&categories=100&purity=100&sorting=toplist&topRange=1M&page=1", result);
    }

    [Fact]
    public void Latest_with_general_and_anime_has_no_top_range()
    {
        var result = QueryStringBuilder.Build(Query(categories: Categories.General | Categories.Anime));

        Assert.Equal("search?categories=110&purity=100&sorting=date_added&page=1", result);
    }

    [Fact]
    public void Hot_sorting()
    {
        Assert.Contains("sorting=hot", QueryStringBuilder.Build(Query(sorting: Sorting.Hot)));
    }

    [Fact]
    public void Random_with_seed_emits_seed_before_page()
    {
        var result = QueryStringBuilder.Build(Query(sorting: Sorting.Random, seed: "abc123", page: 2));

        Assert.Equal("search?categories=100&purity=100&sorting=random&seed=abc123&page=2", result);
    }

    [Fact]
    public void Seed_is_ignored_unless_random()
    {
        Assert.DoesNotContain("seed=", QueryStringBuilder.Build(Query(sorting: Sorting.Hot, seed: "abc123")));
    }

    [Fact]
    public void Random_without_seed_omits_seed()
    {
        Assert.DoesNotContain("seed=", QueryStringBuilder.Build(Query(sorting: Sorting.Random, seed: "")));
    }

    [Theory]
    [InlineData(RatioFilter.Wide16x9, "ratios=16x9")]
    [InlineData(RatioFilter.Ultrawide21x9, "ratios=21x9")]
    [InlineData(RatioFilter.Portrait9x16, "ratios=9x16")]
    public void Ratio_filters(RatioFilter ratio, string expected)
    {
        Assert.Contains(expected, QueryStringBuilder.Build(Query(ratio: ratio)));
    }

    [Fact]
    public void Any_ratio_is_omitted()
    {
        Assert.DoesNotContain("ratios", QueryStringBuilder.Build(Query()));
    }

    [Fact]
    public void AtLeast_and_color_in_documented_order()
    {
        var result = QueryStringBuilder.Build(Query(ratio: RatioFilter.Wide16x9, atLeast: "2560x1440", color: "#660000"));

        Assert.Equal(
            "search?categories=100&purity=100&sorting=date_added&ratios=16x9&atleast=2560x1440&colors=660000&page=1",
            result);
    }

    [Fact]
    public void Color_without_hash_is_accepted()
    {
        Assert.Contains("colors=660000", QueryStringBuilder.Build(Query(color: "660000")));
    }

    [Fact]
    public void Purity_sfw_and_sketchy()
    {
        Assert.Contains("purity=110", QueryStringBuilder.Build(Query(purity: Purity.Sfw | Purity.Sketchy)));
    }

    [Fact]
    public void Text_is_url_encoded()
    {
        Assert.Contains("q=red%20car%20%C3%BC", QueryStringBuilder.Build(Query("red car ü")));
    }

    [Fact]
    public void Whitespace_text_is_omitted()
    {
        Assert.DoesNotContain("q=", QueryStringBuilder.Build(Query("   ")));
    }

    [Fact]
    public void Page_below_one_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryStringBuilder.Build(Query(page: 0)));
    }

    [Fact]
    public void No_categories_throws()
    {
        Assert.Throws<ArgumentException>(() => QueryStringBuilder.Build(Query(categories: Categories.None)));
    }

    [Fact]
    public void No_purity_throws()
    {
        Assert.Throws<ArgumentException>(() => QueryStringBuilder.Build(Query(purity: 0)));
    }
}
