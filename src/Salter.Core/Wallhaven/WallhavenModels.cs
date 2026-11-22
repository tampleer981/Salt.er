namespace Salter.Core.Wallhaven;

/// <summary>A wallpaper as listed in search results.</summary>
/// <param name="Id">The wallhaven wallpaper id.</param>
/// <param name="PageUrl">The wallpaper's page on wallhaven.</param>
/// <param name="ImageUrl">The full-resolution image.</param>
/// <param name="ThumbnailUrl">The large thumbnail.</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="FileSize">File size in bytes.</param>
/// <param name="FileType">The MIME type, for example <c>image/jpeg</c>.</param>
/// <param name="Ratio">The aspect ratio as reported by wallhaven.</param>
/// <param name="Colors">Dominant colors as hex strings.</param>
public sealed record WallpaperSummary(
    string Id,
    Uri PageUrl,
    Uri ImageUrl,
    Uri ThumbnailUrl,
    int Width,
    int Height,
    long FileSize,
    string FileType,
    string Ratio,
    IReadOnlyList<string> Colors)
{
    /// <summary>The dimensions formatted as <c>width × height</c>.</summary>
    public string Resolution => $"{Width} × {Height}";

    /// <summary>File extension without a dot, derived from <see cref="FileType"/>, then the image URL, defaulting to jpg.</summary>
    public string Extension => FileType switch
    {
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        _ => ExtensionFromUrl() ?? "jpg",
    };

    private string? ExtensionFromUrl()
    {
        var ext = Path.GetExtension(ImageUrl.AbsolutePath).TrimStart('.');
        return ext.Length is >= 1 and <= 5 && ext.All(char.IsAsciiLetterOrDigit)
            ? ext.ToLowerInvariant()
            : null;
    }
}

/// <summary>One page of search results.</summary>
/// <param name="Items">The wallpapers on this page.</param>
/// <param name="CurrentPage">The 1-based page number.</param>
/// <param name="LastPage">The last available page number.</param>
/// <param name="Seed">The seed for random sorting, if any.</param>
public sealed record SearchPage(
    IReadOnlyList<WallpaperSummary> Items,
    int CurrentPage,
    int LastPage,
    string? Seed)
{
    /// <summary>Whether further pages are available.</summary>
    public bool HasMore => CurrentPage < LastPage;
}

/// <summary>A wallpaper with its tags and uploader.</summary>
/// <param name="Summary">The wallpaper summary.</param>
/// <param name="Tags">The tag names.</param>
/// <param name="Uploader">The uploader's username, if known.</param>
public sealed record WallpaperDetails(
    WallpaperSummary Summary,
    IReadOnlyList<string> Tags,
    string? Uploader);
