using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Salter.App.Services;
using Salter.Presentation.Layout;

namespace Salter.App.Views;

/// <summary>Functions the views call from compiled bindings (<c>x:Bind</c>) and code-behind.</summary>
internal static class Ui
{
    private static readonly ResourceLocalizer Strings = new();

    /// <summary>Gets a user-visible string from <c>Strings/en-US/Resources.resw</c>.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The string, or the key if the resource is missing.</returns>
    public static string Text(string key) => Strings.Get(key);

    /// <summary>Gets a user-visible composite format string and fills in its argument.</summary>
    /// <param name="key">The resource key.</param>
    /// <param name="argument">The value for <c>{0}</c>.</param>
    /// <returns>The formatted string.</returns>
    public static string Format(string key, object argument) => Strings.Format(key, argument);

    /// <summary>Shows an element only while a condition is false.</summary>
    /// <param name="value">The condition.</param>
    /// <returns><see cref="Visibility.Collapsed"/> if <paramref name="value"/> is true; otherwise visible.</returns>
    public static Visibility VisibleUnless(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Shows an element only while a text is empty, such as a hint inside an empty box.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Visible if <paramref name="text"/> is null or empty; otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public static Visibility VisibleWhenEmpty(string? text) =>
        string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Creates a thumbnail image decoded at the flyout width for the display scale.</summary>
    /// <param name="uri">The thumbnail URL.</param>
    /// <param name="root">The XAML root that shows it, or <see langword="null"/> before it is shown (scale 1).</param>
    /// <returns>The image; it downloads and decodes asynchronously.</returns>
    public static BitmapImage Thumbnail(Uri uri, XamlRoot? root) => new()
    {
        DecodePixelType = DecodePixelType.Physical,
        DecodePixelWidth = FeedLayout.DecodePixelWidth(root?.RasterizationScale ?? 1.0),
        UriSource = uri,
    };
}
