using Salter.Core.Feed;

namespace Salter.Core.Tests.Feed;

public class TagExclusionsTests
{
    [Theory]
    [InlineData("people", "people")]
    [InlineData("  People ", "people")]
    [InlineData("#Car", "car")]
    [InlineData("-people", "people")]
    [InlineData("+sci-fi", "sci-fi")]
    [InlineData("pixel_art", "pixel_art")]
    [InlineData("4k", "4k")]
    public void Valid_tags_are_normalized(string tag, string expected)
    {
        Assert.Equal(expected, TagExclusions.Normalize(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("digital art")]
    [InlineData("a&b")]
    [InlineData("café")]
    [InlineData("q=x")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public void Invalid_tags_are_rejected(string? tag)
    {
        Assert.Null(TagExclusions.Normalize(tag));
    }

    [Fact]
    public void Lists_are_deduplicated_and_capped_in_order()
    {
        var tags = Enumerable.Range(0, 15).Select(i => "tag" + i).Prepend("TAG0").Prepend("bad tag");

        var result = TagExclusions.NormalizeAll(tags);

        Assert.Equal(TagExclusions.MaxCount, result.Count);
        Assert.Equal("tag0", result[0]);
        Assert.Equal("tag9", result[^1]);
        Assert.Empty(TagExclusions.NormalizeAll(null));
    }

    [Theory]
    [InlineData("nature", "nature -people -car")]
    [InlineData(" nature ", "nature -people -car")]
    [InlineData(null, "-people -car")]
    [InlineData("  ", "-people -car")]
    public void Exclusions_are_appended_as_minus_terms(string? text, string expected)
    {
        Assert.Equal(expected, TagExclusions.AppendTo(text, ["people", "car"]));
    }

    [Fact]
    public void Text_is_unchanged_without_exclusions()
    {
        Assert.Equal("nature", TagExclusions.AppendTo("nature", null));
        Assert.Null(TagExclusions.AppendTo(null, []));
    }
}
