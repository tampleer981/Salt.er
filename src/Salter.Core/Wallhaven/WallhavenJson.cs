using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Salter.Core.Wallhaven;

internal static partial class WallhavenJson
{
    private const string WallhavenHost = "wallhaven.cc";

    public static SearchPage ParseSearch(Stream json)
    {
        var response = Deserialize(json, WallhavenJsonContext.Default.SearchResponseDto);
        return Guard(() =>
        {
            var data = response.Data ?? throw new JsonException("Missing 'data'.");
            var meta = response.Meta ?? throw new JsonException("Missing 'meta'.");
            return new SearchPage(
                [.. data.Select(d => ToSummary(d ?? throw new JsonException("Null item in 'data'.")))],
                meta.CurrentPage,
                meta.LastPage,
                meta.Seed);
        });
    }

    public static WallpaperDetails ParseWallpaper(Stream json)
    {
        var response = Deserialize(json, WallhavenJsonContext.Default.WallpaperResponseDto);
        return Guard(() =>
        {
            var data = response.Data ?? throw new JsonException("Missing 'data'.");
            return new WallpaperDetails(
                ToSummary(data),
                [.. (data.Tags ?? []).Select(t => (t ?? throw new JsonException("Null tag.")).Name)],
                data.Uploader?.Username);
        });
    }

    private static T Deserialize<T>(Stream json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
        where T : class
    {
        return Guard(() => JsonSerializer.Deserialize(json, info) ?? throw new JsonException("Empty response."));
    }

    private static T Guard<T>(Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception ex) when (ex is JsonException or UriFormatException)
        {
            throw new WallhavenException(
                WallhavenErrorKind.InvalidResponse, "wallhaven returned an unexpected response.", ex);
        }
    }

    private static WallpaperSummary ToSummary(WallpaperDto d) => new(
        IdPattern().IsMatch(d.Id) ? d.Id : throw new JsonException("Unexpected wallpaper id."),
        WallhavenPageUri(d.Url),
        HttpsUri(d.Path),
        HttpsUri(d.Thumbs.Large),
        d.DimensionX,
        d.DimensionY,
        d.FileSize,
        d.FileType,
        d.Ratio,
        d.Colors ?? []);

    // Ids become file names and API path segments; image URLs are fetched and written to disk;
    // the page URL is opened in the browser. Anything outside these shapes is not trusted.
    private static Uri HttpsUri(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        return uri.Scheme == Uri.UriSchemeHttps ? uri : throw new JsonException("Expected an https URL.");
    }

    private static Uri WallhavenPageUri(string value)
    {
        var uri = HttpsUri(value);
        var host = uri.IdnHost;
        return host.Equals(WallhavenHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + WallhavenHost, StringComparison.OrdinalIgnoreCase)
            ? uri
            : throw new JsonException("Expected a wallhaven.cc page URL.");
    }

    [GeneratedRegex(@"^[A-Za-z0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}

internal sealed class SearchResponseDto
{
    public List<WallpaperDto?>? Data { get; set; }

    public MetaDto? Meta { get; set; }
}

internal sealed class WallpaperResponseDto
{
    public WallpaperDetailDto? Data { get; set; }
}

internal sealed class MetaDto
{
    public required int CurrentPage { get; set; }

    public required int LastPage { get; set; }

    public string? Seed { get; set; }
}

internal class WallpaperDto
{
    public required string Id { get; set; }

    public required string Url { get; set; }

    public required string Path { get; set; }

    public required int DimensionX { get; set; }

    public required int DimensionY { get; set; }

    public required long FileSize { get; set; }

    public required string FileType { get; set; }

    public required string Ratio { get; set; }

    public List<string>? Colors { get; set; }

    public required ThumbsDto Thumbs { get; set; }
}

internal sealed class WallpaperDetailDto : WallpaperDto
{
    public UploaderDto? Uploader { get; set; }

    public List<TagDto?>? Tags { get; set; }
}

internal sealed class ThumbsDto
{
    public required string Large { get; set; }
}

internal sealed class UploaderDto
{
    public string? Username { get; set; }
}

internal sealed class TagDto
{
    public required string Name { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(SearchResponseDto))]
[JsonSerializable(typeof(WallpaperResponseDto))]
internal sealed partial class WallhavenJsonContext : JsonSerializerContext;
