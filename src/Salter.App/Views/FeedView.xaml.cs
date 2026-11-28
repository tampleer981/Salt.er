using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
using Windows.System;
using Windows.UI;

namespace Salter.App.Views;

/// <summary>
/// The feed screen: header, search, filters, genres, sort tabs and the wallpaper cards, which load further pages as
/// the list nears its end.
/// </summary>
public sealed partial class FeedView : UserControl
{
    private static readonly TimeSpan OverlayFadeDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ImageFadeDuration = TimeSpan.FromMilliseconds(200);

    private readonly Storyboard _shimmer;
    private readonly ChoiceInput _choices = new();
    private readonly Dictionary<SelectorBarItem, ChoiceItem<Sorting>> _sortItems = [];
    private bool _showingSorting;

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
        CreateSortItems();
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

    // The card's image layer (a border whose ImageBrush the rounded corners clip) and its hover overlay.
    private static (Border? Image, UIElement? Overlay) ThumbnailParts(object element) =>
        element is Button { Content: Grid grid } && grid.Children.Count >= 2
            ? (grid.Children[0] as Border, grid.Children[1])
            : (null, null);

    private static void SetOverlay(object thumbnail, bool visible)
    {
        var (_, overlay) = ThumbnailParts(thumbnail);
        if (overlay is not null)
        {
            Motion.Fade(overlay, visible ? 1 : 0, OverlayFadeDuration);
            // A hidden Set button must not take clicks meant for the thumbnail.
            overlay.IsHitTestVisible = visible;
        }
    }

    private static void ResetThumbnail(Border? image, UIElement? overlay, bool keepImage)
    {
        // At once, without transitions, so a recycled element does not fade out the state of its previous item.
        if (overlay is not null)
        {
            Motion.SetOpacity(overlay, 0);
            overlay.IsHitTestVisible = false;
        }

        if (image is not null && !keepImage)
        {
            // Shown again by ImageOpened once the new image is decoded.
            Motion.SetOpacity(image, 0);
        }
    }

    private static ImageBrush ImageBrushOf(Border image)
    {
        if (image.Background is ImageBrush brush)
        {
            return brush;
        }

        // One brush per card for its lifetime: recycled cards only swap its source.
        brush = new ImageBrush { Stretch = Stretch.UniformToFill };
        brush.ImageOpened += (_, _) => Motion.Fade(image, 1, ImageFadeDuration);
        image.Background = brush;
        return brush;
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

    private void OnRatioChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Feed.SelectRatioCommand);

    private void OnRatioClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Feed.SelectRatioCommand);

    private void OnResolutionChecked(object sender, RoutedEventArgs e) =>
        _choices.Checked(sender, Feed.SelectResolutionCommand);

    private void OnResolutionClick(object sender, RoutedEventArgs e) =>
        _choices.Clicked(sender, Feed.SelectResolutionCommand);

    // Choosing the selected swatch again clears the colour filter; the command handles both.
    private void OnColorChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Feed.SelectColorCommand);

    private void OnColorClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Feed.SelectColorCommand);

    private void CreateSortItems()
    {
        foreach (var sorting in Feed.Sortings)
        {
            var item = new SelectorBarItem { Text = sorting.Label };
            AutomationProperties.SetName(item, sorting.Label);
            _sortItems.Add(item, sorting);
            SortBar.Items.Add(item);
            sorting.PropertyChanged += OnSortingPropertyChanged;
        }

        ShowSorting();
    }

    private void OnSortingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChoiceItem.IsSelected))
        {
            ShowSorting();
        }
    }

    // The selector bar shows the view model's sorting; a change made here does not run the command again.
    private void ShowSorting()
    {
        var selected = _sortItems.FirstOrDefault(pair => pair.Value.IsSelected).Key;
        if (selected is null || ReferenceEquals(SortBar.SelectedItem, selected))
        {
            return;
        }

        _showingSorting = true;
        try
        {
            SortBar.SelectedItem = selected;
        }
        finally
        {
            _showingSorting = false;
        }
    }

    private void OnSortSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!_showingSorting && sender.SelectedItem is { } item && _sortItems.TryGetValue(item, out var sorting))
        {
            Feed.SelectSortingCommand.Execute(sorting.Value);
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
        var wallpaper = sender.ItemsSourceView.GetAt(args.Index) as WallpaperSummary;
        var brush = image is null ? null : ImageBrushOf(image);
        // An element prepared again for the image it still shows keeps it, instead of downloading and decoding it
        // once more.
        var keepImage = brush?.ImageSource is BitmapImage current && current.UriSource == wallpaper?.ThumbnailUrl;

        // Elements are recycled; a reused one must not keep the hover state or image of its previous item. Reset
        // before the new image is set, so its ImageOpened always fades it in.
        ResetThumbnail(image, overlay, keepImage);
        if (brush is not null && wallpaper is not null && !keepImage)
        {
            brush.ImageSource = Ui.Thumbnail(wallpaper.ThumbnailUrl, XamlRoot);
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML connects event handlers to the instance.")]
    private void OnThumbnailClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        // Releases the decoded image of an element scrolled out of view.
        if (ThumbnailParts(args.Element).Image is { Background: ImageBrush brush })
        {
            brush.ImageSource = null;
        }
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
