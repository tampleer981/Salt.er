using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Salter.Core.Wallhaven;
using Salter.Presentation.Layout;
using Salter.Presentation.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Salter.App.Views;

/// <summary>
/// The feed screen: wordmark, search, filters, genres, sort tabs and the wallpaper list, which loads further pages as
/// it nears its end.
/// </summary>
public sealed partial class FeedView : UserControl
{
    // How much a thumbnail grows under the pointer.
    private const float HoverScale = 1.02f;

    private readonly Storyboard _shimmer;
    private readonly ChoiceInput _choices = new();

    /// <summary>Creates the feed screen.</summary>
    /// <param name="shell">The shell; the screen shows its feed and opens its settings.</param>
    public FeedView(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        Shell = shell;
        InitializeComponent();

        _shimmer = (Storyboard)Resources["ShimmerStoryboard"];
        // The scroll viewer handles the wheel itself; listen to handled events too to scroll the genres sideways.
        GenreScroller.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnGenresPointerWheelChanged), true);
        Feed.PropertyChanged += OnFeedPropertyChanged;
        Feed.ExcludedTags.CollectionChanged += OnExcludedTagsChanged;
        UpdateExclusionChips();
        Loaded += (_, _) => UpdateShimmer();
        Unloaded += (_, _) => _shimmer.Stop();
    }

    /// <summary>Gets the shell.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Gets the feed shown.</summary>
    public FeedViewModel Feed => Shell.Feed;

    /// <summary>Shows the results list once a page is ready.</summary>
    /// <param name="state">The feed state.</param>
    /// <returns>The list's visibility.</returns>
    public static Visibility ResultsVisibility(FeedState state) => VisibleIf(state == FeedState.Ready);

    /// <summary>Shows the loading placeholders while the first page loads.</summary>
    /// <param name="state">The feed state.</param>
    /// <returns>The placeholders' visibility.</returns>
    public static Visibility LoadingVisibility(FeedState state) => VisibleIf(state == FeedState.Loading);

    /// <summary>Shows the empty state when nothing was found.</summary>
    /// <param name="state">The feed state.</param>
    /// <returns>The empty state's visibility.</returns>
    public static Visibility EmptyVisibility(FeedState state) => VisibleIf(state == FeedState.Empty);

    /// <summary>Shows the error state when the first page failed.</summary>
    /// <param name="state">The feed state.</param>
    /// <returns>The error state's visibility.</returns>
    public static Visibility ErrorVisibility(FeedState state) => VisibleIf(state == FeedState.Error);

    /// <summary>Moves keyboard focus to the search box.</summary>
    public void FocusSearch()
    {
        _ = SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private static (Image? Image, UIElement? Overlay) ThumbnailParts(object element) =>
        element is Button { Content: Grid grid } && grid.Children.Count == 2
            ? (grid.Children[0] as Image, grid.Children[1])
            : (null, null);

    private static void SetOverlay(object thumbnail, bool visible)
    {
        var (image, overlay) = ThumbnailParts(thumbnail);
        if (overlay is not null)
        {
            Motion.SetOpacity(overlay, visible ? 1 : 0, animate: true);
            // A hidden Set button must not take clicks meant for the thumbnail.
            overlay.IsHitTestVisible = visible;
        }

        if (image is not null)
        {
            // No growth at all while animations are off, rather than a sudden jump.
            Motion.SetScale(image, visible && Motion.Enabled ? HoverScale : 1, animate: true);
        }
    }

    private static void ResetThumbnail(Image? image, UIElement? overlay, bool keepImage)
    {
        // At once, without transitions, so a recycled element does not fade out the state of its previous item.
        if (overlay is not null)
        {
            Motion.SetOpacity(overlay, 0, animate: false);
            overlay.IsHitTestVisible = false;
        }

        if (image is not null)
        {
            Motion.SetScale(image, 1, animate: false);
            if (!keepImage)
            {
                // Shown again by ImageOpened once the new image is decoded.
                Motion.SetOpacity(image, 0, animate: false);
            }
        }
    }

    private static Color ParseColor(string hex)
    {
        // The catalog holds #rrggbb values only.
        var rgb = Convert.FromHexString(hex.AsSpan(1));
        return Color.FromArgb(0xFF, rgb[0], rgb[1], rgb[2]);
    }

    private void OnFeedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FeedViewModel.State))
        {
            return;
        }

        if (Feed.State == FeedState.Loading)
        {
            // A new query starts at the top.
            _ = FeedScroller.ChangeView(null, 0, null, disableAnimation: true);
        }

        UpdateShimmer();
    }

    private void UpdateShimmer()
    {
        // Runs only while visible, so an idle tray app spends nothing on it.
        if (Feed.State == FeedState.Loading && IsLoaded)
        {
            _shimmer.Begin();
        }
        else
        {
            _shimmer.Stop();
        }
    }

    private void OnExcludedTagsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateExclusionChips();

    // No empty row under the box while nothing is excluded.
    private void UpdateExclusionChips() => ExclusionChips.Visibility = VisibleIf(Feed.ExcludedTags.Count > 0);

    private void OnExclusionKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            // The binding updates the text on every change, so the view model already has what was typed.
            Feed.AddExclusionCommand.Execute(ExclusionBox.Text);
        }
    }

    private void OnRemoveExclusionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            Feed.RemoveExclusionCommand.Execute(tag);
            // The chip, and so the focus, is gone; keep the keyboard in the filters.
            _ = ExclusionBox.Focus(FocusState.Programmatic);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnRemoveExclusionLoaded(object sender, RoutedEventArgs e)
    {
        // Set here: a compiled binding cannot pass the template's string item itself to a formatting function.
        if (sender is Button { Tag: string tag } button)
        {
            var name = Ui.Format("Filter_RemoveExclusion", tag);
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, name);
        }
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Feed.SubmitSearchCommand.Execute(null);
        }
    }

    private void OnGenresPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(GenreScroller).Properties;
        if (!properties.IsHorizontalMouseWheel)
        {
            _ = GenreScroller.ChangeView(GenreScroller.HorizontalOffset - properties.MouseWheelDelta, null, null);
            e.Handled = true;
        }
    }

    // Chips and tabs: the mouse, the keyboard and UI Automation all run the same command (see ChoiceInput).
    private void OnGenreChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Feed.SelectGenreCommand);

    private void OnGenreClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Feed.SelectGenreCommand);

    private void OnSortingChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, SelectSorting);

    private void OnSortingClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, SelectSorting);

    private void OnRatioChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Feed.SelectRatioCommand);

    private void OnRatioClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Feed.SelectRatioCommand);

    private void OnResolutionChecked(object sender, RoutedEventArgs e) =>
        _choices.Checked(sender, Feed.SelectResolutionCommand);

    private void OnResolutionClick(object sender, RoutedEventArgs e) =>
        _choices.Clicked(sender, Feed.SelectResolutionCommand);

    // Choosing the selected swatch again clears the colour filter; the command handles both.
    private void OnColorChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Feed.SelectColorCommand);

    private void OnColorClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Feed.SelectColorCommand);

    private void SelectSorting(ChoiceItem sorting)
    {
        if (sorting is ChoiceItem<Sorting> choice)
        {
            Feed.SelectSortingCommand.Execute(choice.Value);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnColorPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Control swatch && sender.ItemsSourceView.GetAt(args.Index) is ChoiceItem<string> color)
        {
            swatch.Background = new SolidColorBrush(ParseColor(color.Value));
        }
    }

    private void OnThumbnailPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        var (image, overlay) = ThumbnailParts(args.Element);
        var keepImage = false;
        if (image is not null && sender.ItemsSourceView.GetAt(args.Index) is WallpaperSummary wallpaper)
        {
            // An element prepared again for the image it still shows keeps it, instead of downloading and decoding
            // it once more.
            keepImage = image.Source is BitmapImage current && current.UriSource == wallpaper.ThumbnailUrl;
            if (!keepImage)
            {
                image.Source = Ui.Thumbnail(wallpaper.ThumbnailUrl, XamlRoot);
            }
        }

        // Elements are recycled; a reused one must not keep the hover state or image of its previous item.
        ResetThumbnail(image, overlay, keepImage);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnThumbnailImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            Motion.SetOpacity(image, 1, animate: true);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnThumbnailSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The image grows on hover around its centre; the cell clips it so it never covers its neighbours.
        if (sender is Grid { Children: [Image image, ..] } cell)
        {
            var size = e.NewSize;
            cell.Clip = new RectangleGeometry { Rect = new Rect(0, 0, size.Width, size.Height) };
            image.CenterPoint = new Vector3((float)(size.Width / 2), (float)(size.Height / 2), 0);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnThumbnailClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        // Releases the decoded image of an element scrolled out of view.
        ThumbnailParts(args.Element).Image?.Source = null;
    }

    // The Tag holds the wallpaper: ItemsRepeater does not set the data context of x:Bind templates.
    private void OnThumbnailClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WallpaperSummary wallpaper })
        {
            Feed.OpenCommand.Execute(wallpaper);
        }
    }

    private void OnQuickApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WallpaperSummary wallpaper })
        {
            Feed.QuickApplyCommand.Execute(wallpaper);
        }
    }

    private void OnThumbnailPointerEntered(object sender, PointerRoutedEventArgs e) => SetOverlay(sender, visible: true);

    private void OnThumbnailPointerExited(object sender, PointerRoutedEventArgs e) => SetOverlay(sender, visible: false);

    // Keyboard users see the overlay, and so reach the Set button, while the thumbnail or its button has focus.
    private void OnThumbnailGotFocus(object sender, RoutedEventArgs e) => SetOverlay(sender, visible: true);

    private void OnThumbnailLostFocus(object sender, RoutedEventArgs e) => SetOverlay(sender, visible: false);

    private void OnFeedViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        // Runs for every scrolled frame: only cheap property reads here, and no command while a page is already
        // loading or waits for the Retry button.
        if (!Feed.IsLoadingMore && !Feed.LoadMoreFailed)
        {
            LoadMoreIfNearEnd();
        }
    }

    // Also after a page arrives: a list shorter than the viewport never scrolls, so it would never load more.
    private void OnFeedSizeChanged(object sender, SizeChangedEventArgs e) => LoadMoreIfNearEnd();

    private void LoadMoreIfNearEnd()
    {
        // The command ignores the call unless results are shown, more pages exist, none is loading and the last one
        // did not fail; a failed page waits for the Retry button.
        if (FeedLayout.IsNearEnd(FeedScroller.VerticalOffset, FeedScroller.ViewportHeight, FeedScroller.ExtentHeight))
        {
            Feed.LoadMoreCommand.Execute(null);
        }
    }
}
