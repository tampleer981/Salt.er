using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Salter.Core.Feed;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;

namespace Salter.Presentation.ViewModels;

/// <summary>
/// The wallpaper feed: genre, sorting, search and filter selection, and the infinitely scrolling result list.
/// </summary>
/// <remarks>
/// Every change of selection starts a new query generation that cancels the previous one; results that arrive for
/// an older generation are discarded. Members must be used from the UI thread.
/// </remarks>
public sealed partial class FeedViewModel : ObservableObject
{
    /// <summary>How many leading pages of a sorted feed <see cref="RefreshCommand"/> shuffles.</summary>
    public const int MaxShufflePages = 20;

    private static readonly Sorting[] SortingOrder = [Sorting.Toplist, Sorting.Latest, Sorting.Hot, Sorting.Random];

    private static readonly (RatioFilter Value, string? Label)[] RatioOrder =
    [
        (RatioFilter.Any, null),
        (RatioFilter.Wide16x9, "16:9"),
        (RatioFilter.Ultrawide21x9, "21:9"),
        (RatioFilter.Portrait9x16, "9:16"),
    ];

    private static readonly (ResolutionFilter Value, string Key)[] ResolutionOrder =
    [
        (ResolutionFilter.Native, "Filter_Native"),
        (ResolutionFilter.Uhd, "Filter_4K"),
        (ResolutionFilter.Any, "Filter_Any"),
    ];

    private readonly IWallhavenClient _client;
    private readonly FeedRequestFactory _requests;
    private readonly ISettingsStore _settings;
    private readonly IApiKeyProvider _apiKey;
    private readonly ILocalizer _localizer;
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly bool _constructed;

    private QueryGeneration? _current;
    private string _submittedSearch = string.Empty;

    /// <summary>Creates the feed view model. Nothing is loaded until <see cref="LoadCommand"/> runs.</summary>
    /// <param name="client">Runs the searches.</param>
    /// <param name="requests">Builds the search for the current selection.</param>
    /// <param name="settings">
    /// Provides the purity levels, read whenever a new query starts, and stores the excluded tags.
    /// </param>
    /// <param name="apiKey">
    /// Tells whether an API key is configured; without one only SFW wallpapers are requested, whatever the stored
    /// purity levels say.
    /// </param>
    /// <param name="localizer">Provides labels and error messages.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FeedViewModel(
        IWallhavenClient client,
        FeedRequestFactory requests,
        ISettingsStore settings,
        IApiKeyProvider apiKey,
        ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(localizer);
        _client = client;
        _requests = requests;
        _settings = settings;
        _apiKey = apiKey;
        _localizer = localizer;

        Genres = [.. GenreCatalog.All.Select(g => new ChoiceItem<Genre>(g, localizer.Get("Genre_" + g.Key)))];
        Sortings = [.. SortingOrder.Select(s => new ChoiceItem<Sorting>(s, localizer.Get("Sort_" + s)))];
        SelectedGenre = Genres.First(g => g.Value == GenreCatalog.Default);
        SelectedSorting = Sorting.Toplist;
        Sortings[0].IsSelected = true;
        // Ratios are numbers and need no translation; "Any" does.
        Ratios = [.. RatioOrder.Select(r => new ChoiceItem<RatioFilter>(r.Value, r.Label ?? localizer.Get("Filter_Any")))];
        Resolutions = [.. ResolutionOrder.Select(r => new ChoiceItem<ResolutionFilter>(r.Value, localizer.Get(r.Key)))];
        Colors = [.. ColorCatalog.All.Select(c => new ChoiceItem<string>(c.Hex, localizer.Get("Color_" + c.Key)))];
        Filters = new FeedFilters();
        ExcludedTags = [.. TagExclusions.NormalizeAll(settings.Load().ExcludedTags)];
        SearchText = string.Empty;
        ExclusionText = string.Empty;
        State = FeedState.Idle;
        _constructed = true;
    }

    /// <summary>Raised when the user opens a wallpaper's details.</summary>
    public event EventHandler<WallpaperSummary>? WallpaperOpened;

    /// <summary>Raised when the user asks to set a wallpaper straight from the feed.</summary>
    public event EventHandler<WallpaperSummary>? QuickApplyRequested;

    /// <summary>The wallpapers loaded so far for the current query, without duplicates.</summary>
    public ObservableCollection<WallpaperSummary> Items { get; } = [];

    /// <summary>The genres offered, in display order.</summary>
    public IReadOnlyList<ChoiceItem<Genre>> Genres { get; }

    /// <summary>The sort tabs offered, in display order.</summary>
    public IReadOnlyList<ChoiceItem<Sorting>> Sortings { get; }

    /// <summary>The aspect ratio filter options, in display order; the one in <see cref="Filters"/> is selected.</summary>
    public IReadOnlyList<ChoiceItem<RatioFilter>> Ratios { get; }

    /// <summary>The minimum resolution options, in display order; the one in <see cref="Filters"/> is selected.</summary>
    public IReadOnlyList<ChoiceItem<ResolutionFilter>> Resolutions { get; }

    /// <summary>
    /// The colour swatches, in display order, valued as <c>#rrggbb</c>; the one in <see cref="Filters"/>, if any, is
    /// selected.
    /// </summary>
    public IReadOnlyList<ChoiceItem<string>> Colors { get; }

    /// <summary>
    /// The tags whose wallpapers are left out of every query, in the order they were added; at most
    /// <see cref="TagExclusions.MaxCount"/>. Changed by <see cref="AddExclusionCommand"/> and
    /// <see cref="RemoveExclusionCommand"/>, which also save them; <see cref="ResetFiltersCommand"/> keeps them.
    /// </summary>
    public ObservableCollection<string> ExcludedTags { get; }

    /// <summary>The selected genre; <c>all</c> while a search is shown.</summary>
    [ObservableProperty]
    public partial ChoiceItem<Genre> SelectedGenre { get; private set; }

    /// <summary>The selected result ordering.</summary>
    [ObservableProperty]
    public partial Sorting SelectedSorting { get; private set; }

    /// <summary>The ratio, resolution and colour filters. Assigning a different value reloads the feed.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    [ObservableProperty]
    public partial FeedFilters Filters { get; set; }

    /// <summary>Whether the filter panel is open.</summary>
    [ObservableProperty]
    public partial bool FiltersOpen { get; set; }

    /// <summary>The search box text; applied by <see cref="SubmitSearchCommand"/>.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>The text of the box that adds an excluded tag; cleared once its tag is excluded.</summary>
    [ObservableProperty]
    public partial string ExclusionText { get; set; }

    /// <summary>What the feed is currently showing.</summary>
    [ObservableProperty]
    public partial FeedState State { get; private set; }

    /// <summary>
    /// The message for the last failure, or <see langword="null"/>. Set with <see cref="FeedState.Error"/> when the
    /// first page fails, or alongside <see cref="FeedState.Ready"/> when loading a further page fails.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Whether a further page is being loaded.</summary>
    [ObservableProperty]
    public partial bool IsLoadingMore { get; private set; }

    /// <summary>
    /// Whether loading a further page failed. <see cref="LoadMoreCommand"/> then does nothing until
    /// <see cref="RetryLoadMoreCommand"/> runs or a new query starts, so a failure never retries by itself.
    /// </summary>
    [ObservableProperty]
    public partial bool LoadMoreFailed { get; private set; }

    /// <summary>
    /// Returns a random order of the pages 1..<c>count</c>, each exactly once, for a shuffled refresh.
    /// Replaceable so tests are deterministic.
    /// </summary>
    internal Func<int, IReadOnlyList<int>> ShufflePages { get; set; } = static count =>
    {
        var pages = Enumerable.Range(1, count).ToArray();
        Shuffle(pages);
        return pages;
    };

    /// <summary>
    /// Puts the wallpapers of one page in a random order before they are shown, in a shuffled refresh and with
    /// <see cref="Sorting.Random"/>. Replaceable so tests are deterministic.
    /// </summary>
    internal Action<IList<WallpaperSummary>> ShuffleItems { get; set; } = static items => Shuffle(items);

    /// <summary>Shuffles a list in place (Fisher–Yates) with a cryptographically strong generator.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list to shuffle.</param>
    internal static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Starts a new query for the current selection.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task LoadAsync() => ReloadAsync();

    /// <summary>
    /// Shows the current selection in a new random order. <see cref="Sorting.Random"/> starts over with a new seed.
    /// Any other sorting shuffles its first pages (up to <see cref="MaxShufflePages"/>, as far as the last query
    /// reported): they load in a random order as the list scrolls, each once, and the wallpapers of each page are
    /// shuffled too. Before any page has loaded it reloads in the usual order.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync()
    {
        var previous = _current;
        return previous is null || previous.Sorting == Sorting.Random || previous.LastPage < 1
            ? ReloadAsync()
            : ReloadAsync(ShufflePages(Math.Min(previous.LastPage, MaxShufflePages)));
    }

    /// <summary>Searches for <see cref="SearchText"/>, or shows the current genre when the text is blank.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SubmitSearchAsync()
    {
        _submittedSearch = SearchText?.Trim() ?? string.Empty;
        if (_submittedSearch.Length > 0)
        {
            SelectedGenre = Genres[0];
        }

        return ReloadAsync();
    }

    /// <summary>
    /// Loads the next page of the current query, unless it is the last, one is already loading, or the last attempt
    /// failed (see <see cref="LoadMoreFailed"/>).
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task LoadMoreAsync() => LoadMoreFailed ? Task.CompletedTask : LoadNextPageAsync();

    /// <summary>Loads the next page again after a failure, at the user's request.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RetryLoadMoreAsync() => LoadNextPageAsync();

    private async Task LoadNextPageAsync()
    {
        var generation = _current;
        if (generation is null || State != FeedState.Ready || !generation.HasMore || generation.PageInFlight)
        {
            return;
        }

        generation.PageInFlight = true;
        IsLoadingMore = true;
        var page = generation.NextPage;
        try
        {
            var result = await _client.SearchAsync(Query(generation, page), generation.Token);
            if (generation != _current)
            {
                return;
            }

            ErrorMessage = null;
            LoadMoreFailed = false;
            Accept(generation, result, page);
        }
        catch (OperationCanceledException) when (generation.Token.IsCancellationRequested)
        {
            // Superseded by a newer query.
        }
        catch (WallhavenException ex)
        {
            if (generation == _current)
            {
                ErrorMessage = MessageFor(ex.Kind);
                LoadMoreFailed = true;
            }
        }
        finally
        {
            generation.PageInFlight = false;
            if (generation == _current)
            {
                IsLoadingMore = false;
            }
        }
    }

    /// <summary>Shows the given genre, clearing any search.</summary>
    /// <param name="genre">The genre to show.</param>
    [RelayCommand]
    private void SelectGenre(ChoiceItem<Genre>? genre)
    {
        if (genre is null
            || (genre == SelectedGenre && string.IsNullOrEmpty(SearchText) && _submittedSearch.Length == 0))
        {
            return;
        }

        SearchText = string.Empty;
        _submittedSearch = string.Empty;
        SelectedGenre = genre;
        BeginReload();
    }

    /// <summary>Changes the result ordering.</summary>
    /// <param name="sorting">The ordering to use.</param>
    [RelayCommand]
    private void SelectSorting(Sorting sorting)
    {
        if (sorting == SelectedSorting)
        {
            return;
        }

        SelectedSorting = sorting;
        BeginReload();
    }

    /// <summary>Opens or closes the filter panel.</summary>
    [RelayCommand]
    private void ToggleFilters() => FiltersOpen = !FiltersOpen;

    /// <summary>Restores the default filters.</summary>
    [RelayCommand]
    private void ResetFilters() => Filters = new FeedFilters();

    /// <summary>Filters by an aspect ratio.</summary>
    /// <param name="ratio">An entry of <see cref="Ratios"/>.</param>
    [RelayCommand]
    private void SelectRatio(ChoiceItem<RatioFilter>? ratio)
    {
        if (ratio is not null)
        {
            Filters = Filters with { Ratio = ratio.Value };
        }
    }

    /// <summary>Filters by a minimum resolution.</summary>
    /// <param name="resolution">An entry of <see cref="Resolutions"/>.</param>
    [RelayCommand]
    private void SelectResolution(ChoiceItem<ResolutionFilter>? resolution)
    {
        if (resolution is not null)
        {
            Filters = Filters with { Resolution = resolution.Value };
        }
    }

    /// <summary>Filters by a colour, or removes the colour filter when the selected swatch is chosen again.</summary>
    /// <param name="color">An entry of <see cref="Colors"/>.</param>
    [RelayCommand]
    private void SelectColor(ChoiceItem<string>? color)
    {
        if (color is not null)
        {
            var selected = string.Equals(Filters.Color, color.Value, StringComparison.OrdinalIgnoreCase);
            Filters = Filters with { Color = selected ? null : color.Value };
        }
    }

    /// <summary>
    /// Leaves a tag's wallpapers out of the feed, saves the exclusions and reloads. Invalid tags (see
    /// <see cref="TagExclusions.Normalize"/>), tags already excluded and tags beyond
    /// <see cref="TagExclusions.MaxCount"/> are ignored.
    /// </summary>
    /// <param name="tag">The tag, as typed or as shown on a wallpaper.</param>
    [RelayCommand]
    private void AddExclusion(string? tag)
    {
        if (TagExclusions.Normalize(tag) is not { } value)
        {
            return;
        }

        var added = false;
        if (!ExcludedTags.Contains(value))
        {
            if (ExcludedTags.Count >= TagExclusions.MaxCount)
            {
                return;
            }

            ExcludedTags.Add(value);
            added = true;
        }

        // The box is done with once its tag is excluded, now or before.
        if (TagExclusions.Normalize(ExclusionText) == value)
        {
            ExclusionText = string.Empty;
        }

        if (added)
        {
            ExclusionsChanged();
        }
    }

    /// <summary>Shows a tag's wallpapers again, saves the exclusions and reloads.</summary>
    /// <param name="tag">An entry of <see cref="ExcludedTags"/>.</param>
    [RelayCommand]
    private void RemoveExclusion(string? tag)
    {
        if (TagExclusions.Normalize(tag) is { } value && ExcludedTags.Remove(value))
        {
            ExclusionsChanged();
        }
    }

    private void ExclusionsChanged()
    {
        try
        {
            _settings.Save(_settings.Load() with { ExcludedTags = [.. ExcludedTags] });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Still applied for this session; the next successful save stores them.
        }

        BeginReload();
    }

    /// <summary>Opens a wallpaper's details.</summary>
    /// <param name="wallpaper">The wallpaper to open.</param>
    [RelayCommand]
    private void Open(WallpaperSummary? wallpaper)
    {
        if (wallpaper is not null)
        {
            WallpaperOpened?.Invoke(this, wallpaper);
        }
    }

    /// <summary>Asks to set a wallpaper straight from the feed.</summary>
    /// <param name="wallpaper">The wallpaper to set.</param>
    [RelayCommand]
    private void QuickApply(WallpaperSummary? wallpaper)
    {
        if (wallpaper is not null)
        {
            QuickApplyRequested?.Invoke(this, wallpaper);
        }
    }

    partial void OnSelectedGenreChanged(ChoiceItem<Genre> oldValue, ChoiceItem<Genre> newValue)
    {
        oldValue?.IsSelected = false;
        newValue.IsSelected = true;
    }

    partial void OnSelectedSortingChanged(Sorting value)
    {
        foreach (var item in Sortings)
        {
            item.IsSelected = item.Value == value;
        }
    }

    partial void OnFiltersChanging(FeedFilters value) => ArgumentNullException.ThrowIfNull(value);

    partial void OnFiltersChanged(FeedFilters value)
    {
        foreach (var item in Ratios)
        {
            item.IsSelected = item.Value == value.Ratio;
        }

        foreach (var item in Resolutions)
        {
            item.IsSelected = item.Value == value.Resolution;
        }

        foreach (var item in Colors)
        {
            item.IsSelected = string.Equals(item.Value, value.Color, StringComparison.OrdinalIgnoreCase);
        }

        if (_constructed)
        {
            BeginReload();
        }
    }

    // Reloads started by property changes and synchronous commands have no caller to await them; this surfaces
    // unexpected failures on the UI context the same way an async command would.
    private async void BeginReload() => await ReloadAsync();

    // Without a page order the query is the ordinary one, page after page; with one it is a shuffled refresh.
    private async Task ReloadAsync(IReadOnlyList<int>? pageOrder = null)
    {
        _current?.Cancel();
        var generation = new QueryGeneration(
            SelectedGenre.Value,
            _submittedSearch,
            SelectedSorting,
            Filters,
            [.. ExcludedTags],
            CurrentPurity(),
            pageOrder);
        _current = generation;

        Items.Clear();
        _ids.Clear();
        ErrorMessage = null;
        IsLoadingMore = false;
        LoadMoreFailed = false;
        State = FeedState.Loading;

        var startPage = generation.NextPage;
        SearchPage result;
        try
        {
            result = await _client.SearchAsync(Query(generation, startPage), generation.Token);
        }
        catch (OperationCanceledException) when (generation.Token.IsCancellationRequested)
        {
            // Superseded by a newer query.
            return;
        }
        catch (WallhavenException ex)
        {
            if (generation == _current)
            {
                ErrorMessage = MessageFor(ex.Kind);
                State = FeedState.Error;
            }

            return;
        }

        if (generation != _current)
        {
            return;
        }

        Accept(generation, result, startPage);
        State = Items.Count == 0 ? FeedState.Empty : FeedState.Ready;
    }

    // wallhaven serves sketchy and NSFW content only with an API key; never ask for it without one.
    private Purity CurrentPurity() =>
        string.IsNullOrWhiteSpace(_apiKey.GetApiKey()) ? Purity.Sfw : _settings.Load().Purity;

    private SearchQuery Query(QueryGeneration generation, int page) =>
        _requests.Create(
            generation.Genre,
            generation.SearchText,
            generation.Sorting,
            generation.Filters,
            generation.Purity,
            page,
            generation.Seed,
            generation.ExcludedTags);

    private void Accept(QueryGeneration generation, SearchPage result, int page)
    {
        IEnumerable<WallpaperSummary> items = result.Items;
        if (generation.PageOrder is not null || generation.Sorting == Sorting.Random)
        {
            var shuffled = result.Items.ToList();
            ShuffleItems(shuffled);
            items = shuffled;
        }

        foreach (var item in items)
        {
            if (_ids.Add(item.Id))
            {
                Items.Add(item);
            }
        }

        generation.CurrentPage = page;
        generation.PagesLoaded++;
        // A shuffled refresh ends with its last page; the ordinary query when the server says so.
        generation.HasMore = generation.PageOrder is { } order ? generation.PagesLoaded < order.Count : result.HasMore;
        generation.LastPage = Math.Max(generation.LastPage, result.LastPage);
        generation.Seed = result.Seed ?? generation.Seed;
    }

    private string MessageFor(WallhavenErrorKind kind) => _localizer.Get(kind switch
    {
        WallhavenErrorKind.Unauthorized => "Error_ApiKeyRejected",
        WallhavenErrorKind.Network => "Error_Network",
        WallhavenErrorKind.RateLimited => "Error_RateLimited",
        _ => "Error_Server",
    });

    /// <summary>The fixed parameters and paging state of one query, cancelled when a newer query starts.</summary>
    [SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "The source is disposed by Cancel once a newer query supersedes this one; the current query lives as long as the view model.")]
    private sealed class QueryGeneration
    {
        private readonly CancellationTokenSource _cancellation = new();

        public QueryGeneration(
            Genre genre,
            string searchText,
            Sorting sorting,
            FeedFilters filters,
            IReadOnlyList<string> excludedTags,
            Purity purity,
            IReadOnlyList<int>? pageOrder)
        {
            PageOrder = pageOrder is { Count: > 0 } ? pageOrder : null;
            Genre = genre;
            SearchText = searchText;
            Sorting = sorting;
            Filters = filters;
            ExcludedTags = excludedTags;
            Purity = purity;
            // Captured up front: the token stays usable after the source is disposed, the source's Token property does not.
            Token = _cancellation.Token;
        }

        public Genre Genre { get; }

        public string SearchText { get; }

        public Sorting Sorting { get; }

        public FeedFilters Filters { get; }

        public IReadOnlyList<string> ExcludedTags { get; }

        public Purity Purity { get; }

        public CancellationToken Token { get; }

        /// <summary>The random seed returned by the server; <see langword="null"/> until page 1 arrives.</summary>
        public string? Seed { get; set; }

        /// <summary>
        /// The order a shuffled refresh loads its pages in, or <see langword="null"/> for the ordinary page order.
        /// </summary>
        public IReadOnlyList<int>? PageOrder { get; }

        public int CurrentPage { get; set; }

        /// <summary>How many pages have been accepted.</summary>
        public int PagesLoaded { get; set; }

        /// <summary>The page to request next: the next of <see cref="PageOrder"/>, or the one after the current.</summary>
        public int NextPage => PageOrder is { } order ? order[Math.Min(PagesLoaded, order.Count - 1)] : CurrentPage + 1;

        /// <summary>The last page the server reported for this query.</summary>
        public int LastPage { get; set; }

        public bool HasMore { get; set; }

        public bool PageInFlight { get; set; }

        public void Cancel()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
