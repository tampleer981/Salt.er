using System.Text;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Wallhaven;

public class WallhavenJsonTests
{
    private static FileStream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static MemoryStream Utf8(string json) => new(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void ParseSearch_maps_items_and_meta()
    {
        using var stream = Fixture("search.json");

        var page = WallhavenJson.ParseSearch(stream);

        Assert.Equal(["94x38z", "x8d2l3"], page.Items.Select(i => i.Id));
        var first = page.Items[0];
        Assert.Equal(3840, first.Width);
        Assert.Equal(2160, first.Height);
        Assert.Equal("3840 × 2160", first.Resolution);
        Assert.Equal(new Uri("https://th.wallhaven.cc/lg/94/94x38z.jpg"), first.ThumbnailUrl);
        Assert.Equal(new Uri("https://w.wallhaven.cc/full/94/wallhaven-94x38z.jpg"), first.ImageUrl);
        Assert.Equal(new Uri("https://wallhaven.cc/w/94x38z"), first.PageUrl);
        Assert.Equal(4123456, first.FileSize);
        Assert.Equal("1.78", first.Ratio);
        Assert.Equal(["#424153", "#999999"], first.Colors);
        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(40, page.LastPage);
        Assert.Null(page.Seed);
        Assert.True(page.HasMore);
    }

    [Fact]
    public void ParseWallpaper_maps_tags_and_uploader()
    {
        using var stream = Fixture("wallpaper.json");

        var details = WallhavenJson.ParseWallpaper(stream);

        Assert.Equal("94x38z", details.Summary.Id);
        Assert.Equal(["nature", "fog"], details.Tags);
        Assert.Equal("mossy", details.Uploader);
    }

    [Fact]
    public void ParseSearch_tolerates_object_query_and_unknown_fields()
    {
        using var stream = Utf8("""
            {"data":[],"meta":{"current_page":2,"last_page":2,"per_page":24,"total":0,"query":{"id":1,"tag":"nature"},"seed":"abc123","extra":true},"unknown":1}
            """);

        var page = WallhavenJson.ParseSearch(stream);

        Assert.Empty(page.Items);
        Assert.Equal("abc123", page.Seed);
        Assert.False(page.HasMore);
    }

    [Theory]
    [InlineData("image/jpeg", "https://x/a.jpg", "jpg")]
    [InlineData("image/png", "https://x/a.png", "png")]
    [InlineData("image/webp", "https://x/a.webp", "webp")]
    [InlineData("image/bmp", "https://x/a.bmp", "bmp")]
    [InlineData("application/octet-stream", "https://x/a", "jpg")]
    [InlineData("application/octet-stream", "https://x/file.PHP?x=1", "php")]
    [InlineData("application/octet-stream", "https://x/file.toolongext", "jpg")]
    [InlineData("application/octet-stream", "https://x/file.a-b", "jpg")]
    public void Extension_maps_mime_types(string mime, string imageUrl, string expected)
    {
        var summary = new WallpaperSummary(
            "id", new Uri("https://x/p"), new Uri(imageUrl), new Uri("https://x/t.jpg"),
            1, 1, 1, mime, "1", []);

        Assert.Equal(expected, summary.Extension);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"data\":[null],\"meta\":{\"current_page\":1,\"last_page\":1}}")]
    [InlineData("{\"data\":[{\"id\":\"a\",\"url\":null,\"path\":\"https://x/a.jpg\",\"dimension_x\":1,\"dimension_y\":1,\"file_size\":1,\"file_type\":\"image/jpeg\",\"ratio\":\"1\",\"thumbs\":{\"large\":\"https://x/t.jpg\"}}],\"meta\":{\"current_page\":1,\"last_page\":1}}")]
    [InlineData("{\"data\":[{\"id\":\"a\",\"url\":\"https://x/a\",\"path\":\"https://x/a.jpg\",\"dimension_x\":1,\"dimension_y\":1,\"file_size\":1,\"file_type\":\"image/jpeg\",\"ratio\":\"1\",\"thumbs\":null}],\"meta\":{\"current_page\":1,\"last_page\":1}}")]
    [InlineData("{}")]
    [InlineData("{\"data\":[{\"id\":\"a\"}],\"meta\":{\"current_page\":1,\"last_page\":1}}")]
    public void ParseSearch_throws_on_malformed_json(string json)
    {
        using var stream = Utf8(json);

        var ex = Assert.Throws<WallhavenException>(() => WallhavenJson.ParseSearch(stream));

        Assert.Equal(WallhavenErrorKind.InvalidResponse, ex.Kind);
    }

    private static string Item(
        string id = "abc123",
        string url = "https://wallhaven.cc/w/abc123",
        string path = "https://w.wallhaven.cc/full/ab/wallhaven-abc123.jpg",
        string thumb = "https://th.wallhaven.cc/lg/ab/abc123.jpg") =>
        $$$"""
        {"data":[{"id":"{{{id}}}","url":"{{{url}}}","path":"{{{path}}}","dimension_x":1,"dimension_y":1,"file_size":1,"file_type":"image/jpeg","ratio":"1","thumbs":{"large":"{{{thumb}}}"}}],"meta":{"current_page":1,"last_page":1}}
        """;

    [Fact]
    public void ParseSearch_accepts_wallhaven_subdomain_page_url()
    {
        using var stream = Utf8(Item(url: "https://www.wallhaven.cc/w/abc123"));

        var page = WallhavenJson.ParseSearch(stream);

        Assert.Equal(new Uri("https://www.wallhaven.cc/w/abc123"), page.Items[0].PageUrl);
    }

    public static TheoryData<string> UntrustedItems() => new()
    {
        Item(path: "http://w.wallhaven.cc/full/ab/wallhaven-abc123.jpg"),
        Item(path: "file:///C:/Windows/win.ini"),
        Item(thumb: "http://th.wallhaven.cc/lg/ab/abc123.jpg"),
        Item(url: "http://wallhaven.cc/w/abc123"),
        Item(url: "https://evil.example/w/abc123"),
        Item(url: "https://notwallhaven.cc/w/abc123"),
        Item(url: "https://wallhaven.cc.evil.example/w/abc123"),
        Item(id: "../abc"),
        Item(id: "abc 123"),
        Item(id: ""),
        Item(id: "abc123\\n"),
    };

    [Theory]
    [MemberData(nameof(UntrustedItems))]
    public void ParseSearch_rejects_untrusted_ids_and_urls(string json)
    {
        using var stream = Utf8(json);

        var ex = Assert.Throws<WallhavenException>(() => WallhavenJson.ParseSearch(stream));

        Assert.Equal(WallhavenErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public void ParseWallpaper_throws_on_missing_data()
    {
        using var stream = Utf8("{}");

        var ex = Assert.Throws<WallhavenException>(() => WallhavenJson.ParseWallpaper(stream));

        Assert.Equal(WallhavenErrorKind.InvalidResponse, ex.Kind);
    }
}
