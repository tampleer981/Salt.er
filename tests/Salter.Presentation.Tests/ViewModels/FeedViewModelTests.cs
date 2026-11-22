using Salter.Core.Feed;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Tests.TestDoubles;
using Salter.Presentation.ViewModels;

namespace Salter.Presentation.Tests.ViewModels;

public class FeedViewModelTests
{
    private readonly FakeWallhavenClient _client = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeLocalizer _localizer = new();
    private readonly FakeApiKeyProvider _apiKey = new();

    [Fact]
    public void Starts_idle_with_defaults_and_loads_nothing()
    {
        var vm = CreateViewModel();

        Assert.Equal(FeedState.Idle, vm.State);
        Assert.Same(GenreCatalog.Default, vm.SelectedGenre.Value);
        Assert.Equal(Sorting.Toplist, vm.SelectedSorting);
        Assert.Equal(new FeedFilters(), vm.Filters);
        Assert.False(vm.FiltersOpen);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public void Choices_are_labelled_from_resources_and_track_selection()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        _client.Enqueue(Page(1, 1, "b"));

        Assert.Equal(
            ["Genre_all", "Genre_nature", "Genre_anime", "Genre_space", "Genre_city", "Genre_architecture", "Genre_abstract", "Genre_cars", "Genre_minimal"],
            vm.Genres.Select(g => g.Label));
        Assert.Equal(["Sort_Toplist", "Sort_Latest", "Sort_Hot", "Sort_Random"], vm.Sortings.Select(s => s.Label));
        Assert.Equal([true, false, false, false], vm.Sortings.Select(s => s.IsSelected));
        Assert.True(vm.Genres[0].IsSelected);

        vm.SelectGenreCommand.Execute(vm.Genres[3]);
        vm.SelectSortingCommand.Execute(Sorting.Hot);

        Assert.Equal([false, false, false, true, false, false, false, false, false], vm.Genres.Select(g => g.IsSelected));
        Assert.Equal([false, false, true, false], vm.Sortings.Select(s => s.IsSelected));
    }

    [Fact]
    public async Task Load_populates_items_and_sets_Ready()
    {
        _settings.Settings = AppSettings.Default with { Purity = Purity.Sfw | Purity.Sketchy };
        _apiKey.Key = "key-1";
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a", "b"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Ready, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(["a", "b"], vm.Items.Select(i => i.Id));
        var query = Assert.Single(_client.Queries);
        Assert.Equal(1, query.Page);
        Assert.Equal(Sorting.Toplist, query.Sorting);
        Assert.Equal(Categories.General | Categories.Anime, query.Categories);
        Assert.Equal(Purity.Sfw | Purity.Sketchy, query.Purity);
    }

    [Fact]
    public async Task Empty_result_sets_Empty()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Empty, vm.State);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Selecting_genre_clears_search_and_reloads()
    {
        var vm = CreateViewModel();
        vm.SearchText = "forest";
        _client.Enqueue(Page(1, 1, "a"));
        await vm.SubmitSearchCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));

        vm.SelectGenreCommand.Execute(vm.Genres[1]);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Same(vm.Genres[1], vm.SelectedGenre);
        Assert.Equal(2, _client.Queries.Count);
        Assert.Equal("nature", _client.Queries[1].Text);
        Assert.Equal(Categories.General, _client.Queries[1].Categories);
        Assert.Equal(["b"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Selecting_the_current_genre_without_search_does_not_reload()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectGenreCommand.Execute(vm.Genres[0]);

        Assert.Single(_client.Queries);
    }

    [Fact]
    public async Task Submitting_search_selects_All_genre()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        vm.SelectGenreCommand.Execute(vm.Genres[3]);
        _client.Enqueue(Page(1, 1, "b"));
        vm.SearchText = "  aurora  ";

        await vm.SubmitSearchCommand.ExecuteAsync(null);

        Assert.Same(vm.Genres[0], vm.SelectedGenre);
        Assert.True(vm.Genres[0].IsSelected);
        Assert.False(vm.Genres[3].IsSelected);
        Assert.Equal(2, _client.Queries.Count);
        Assert.Equal("aurora", _client.Queries[1].Text);
        Assert.Equal(Categories.General | Categories.Anime, _client.Queries[1].Categories);
    }

    [Fact]
    public async Task Submitting_empty_search_reloads_current_genre()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        vm.SelectGenreCommand.Execute(vm.Genres[3]);
        _client.Enqueue(Page(1, 1, "b"));
        vm.SearchText = "   ";

        await vm.SubmitSearchCommand.ExecuteAsync(null);

        Assert.Same(vm.Genres[3], vm.SelectedGenre);
        Assert.Equal("space", _client.Queries[1].Text);
    }

    [Fact]
    public async Task Unsubmitted_search_text_is_not_used_by_other_reloads()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SearchText = "typing";
        _client.Enqueue(Page(1, 1, "b"));

        vm.SelectSortingCommand.Execute(Sorting.Latest);

        Assert.Null(_client.Queries[1].Text);
    }

    [Fact]
    public async Task Changing_sorting_reloads_from_page_1()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a"));
        _client.Enqueue(Page(2, 3, "b"));
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 3, "c"));

        vm.SelectSortingCommand.Execute(Sorting.Latest);

        Assert.Equal(Sorting.Latest, vm.SelectedSorting);
        Assert.Equal(3, _client.Queries.Count);
        Assert.Equal(1, _client.Queries[2].Page);
        Assert.Equal(Sorting.Latest, _client.Queries[2].Sorting);
        Assert.Equal(["c"], vm.Items.Select(i => i.Id));

        vm.SelectSortingCommand.Execute(Sorting.Latest);

        Assert.Equal(3, _client.Queries.Count);
    }

    [Fact]
    public async Task LoadMore_appends_next_page_until_last_page()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 2, "a", "b"));
        _client.Enqueue(Page(2, 2, "c"));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.LoadMoreCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal([1, 2], _client.Queries.Select(q => q.Page));
        Assert.Equal(["a", "b", "c"], vm.Items.Select(i => i.Id));
        Assert.Equal(FeedState.Ready, vm.State);
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task LoadMore_before_first_page_is_ignored()
    {
        var vm = CreateViewModel();

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Empty(_client.Queries);
    }

    [Fact]
    public async Task Next_page_is_single_flight_and_deduplicated()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a", "b"));
        await vm.LoadCommand.ExecuteAsync(null);
        var pending = _client.EnqueuePending();

        var first = vm.LoadMoreCommand.ExecuteAsync(null);
        var second = vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(2, _client.Queries.Count);
        Assert.True(vm.IsLoadingMore);
        pending.SetResult(Page(2, 3, "b", "c", "c", "d"));
        await Task.WhenAll(first, second);

        Assert.Equal(["a", "b", "c", "d"], vm.Items.Select(i => i.Id));
        Assert.False(vm.IsLoadingMore);
        Assert.Equal(2, _client.Queries[1].Page);
    }

    [Fact]
    public async Task Switching_genre_mid_load_discards_stale_page()
    {
        var vm = CreateViewModel();
        var pending = _client.EnqueuePending();
        var load = vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(FeedState.Loading, vm.State);
        _client.Enqueue(Page(1, 1, "new"));

        vm.SelectGenreCommand.Execute(vm.Genres[1]);
        pending.SetResult(Page(1, 1, "stale"));
        await load;

        Assert.True(_client.Tokens[0].IsCancellationRequested);
        Assert.False(_client.Tokens[1].IsCancellationRequested);
        Assert.Equal(["new"], vm.Items.Select(i => i.Id));
        Assert.Equal(FeedState.Ready, vm.State);
    }

    [Fact]
    public async Task Cancelled_stale_load_is_swallowed()
    {
        var vm = CreateViewModel();
        var pending = _client.EnqueuePending();
        var load = vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "new"));

        await vm.RefreshCommand.ExecuteAsync(null);
        pending.SetCanceled(_client.Tokens[0]);
        await load;

        Assert.Equal(["new"], vm.Items.Select(i => i.Id));
        Assert.Equal(FeedState.Ready, vm.State);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Stale_next_page_is_discarded()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        var pending = _client.EnqueuePending();
        var more = vm.LoadMoreCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 3, "b"));

        await vm.RefreshCommand.ExecuteAsync(null);
        pending.SetResult(Page(2, 3, "stale"));
        await more;

        Assert.True(_client.Tokens[1].IsCancellationRequested);
        Assert.Equal(["b"], vm.Items.Select(i => i.Id));
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task Refresh_on_a_sorted_feed_starts_from_another_of_the_first_ten_pages()
    {
        var vm = CreateViewModel();
        (int Pages, int Current)? asked = null;
        vm.PickRefreshPage = (pages, current) =>
        {
            asked = (pages, current);
            return 7;
        };
        _client.Enqueue(Page(1, 40, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(7, 40, "b"));
        _client.Enqueue(Page(8, 40, "c"));

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal((FeedViewModel.MaxRefreshPage, 1), asked);
        Assert.Equal([1, 7, 8], _client.Queries.Select(q => q.Page));
        Assert.Equal(["b", "c"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Refresh_of_a_single_page_feed_reloads_page_one()
    {
        var vm = CreateViewModel();
        vm.PickRefreshPage = (_, _) => throw new InvalidOperationException("Not expected for a one-page feed.");
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([1, 1], _client.Queries.Select(q => q.Page));
    }

    [Fact]
    public void Default_refresh_page_picker_never_repeats_the_current_page()
    {
        var vm = CreateViewModel();
        vm.PickRefreshPage = new FeedViewModel(
            _client, new FeedRequestFactory(() => null), _settings, _apiKey, _localizer).PickRefreshPage;

        for (var i = 0; i < 200; i++)
        {
            var page = vm.PickRefreshPage(3, 2);
            Assert.InRange(page, 1, 3);
            Assert.NotEqual(2, page);
        }
    }

    [Fact]
    public async Task Refresh_on_Random_uses_new_seed()
    {
        var vm = CreateViewModel();
        _client.Enqueue(SeededPage(1, 3, "abc123", "a"));
        vm.SelectSortingCommand.Execute(Sorting.Random);
        _client.Enqueue(SeededPage(2, 3, "abc123", "b"));
        await vm.LoadMoreCommand.ExecuteAsync(null);
        _client.Enqueue(SeededPage(1, 3, "xyz789", "c"));
        _client.Enqueue(SeededPage(2, 3, "xyz789", "d"));

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal([1, 2, 1, 2], _client.Queries.Select(q => q.Page));
        Assert.Equal([null, "abc123", null, "xyz789"], _client.Queries.Select(q => q.Seed));
        Assert.Equal(["c", "d"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Unauthorized_sets_error_state_with_key_message()
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.Unauthorized, "401"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Error, vm.State);
        Assert.Equal(_localizer.Get("Error_ApiKeyRejected"), vm.ErrorMessage);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Network_error_sets_error_state()
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.Network, "offline"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Error, vm.State);
        Assert.Equal(_localizer.Get("Error_Network"), vm.ErrorMessage);
    }

    [Fact]
    public async Task RateLimited_sets_error_state()
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.RateLimited, "429"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Error, vm.State);
        Assert.Equal(_localizer.Get("Error_RateLimited"), vm.ErrorMessage);
    }

    [Theory]
    [InlineData(WallhavenErrorKind.Server)]
    [InlineData(WallhavenErrorKind.NotFound)]
    [InlineData(WallhavenErrorKind.InvalidResponse)]
    public async Task Other_failures_set_server_error_state(WallhavenErrorKind kind)
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new WallhavenException(kind, "failed"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Error, vm.State);
        Assert.Equal(_localizer.Get("Error_Server"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Reload_after_error_clears_message()
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.Network, "offline"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "a"));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Ready, vm.State);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Unexpected_exceptions_are_not_swallowed()
    {
        var vm = CreateViewModel();
        _client.EnqueueFailure(new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadCommand.ExecuteAsync(null));
    }

    [Fact]
    public async Task LoadMore_failure_keeps_items_and_retries_same_page_only_on_request()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.Network, "offline"));

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(FeedState.Ready, vm.State);
        Assert.Equal(_localizer.Get("Error_Network"), vm.ErrorMessage);
        Assert.True(vm.LoadMoreFailed);
        Assert.Equal(["a"], vm.Items.Select(i => i.Id));
        Assert.False(vm.IsLoadingMore);

        // Scrolling (or a layout change) near the end must not retry by itself.
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal([1, 2], _client.Queries.Select(q => q.Page));
        Assert.True(vm.LoadMoreFailed);

        _client.Enqueue(Page(2, 3, "b"));
        await vm.RetryLoadMoreCommand.ExecuteAsync(null);

        Assert.Equal([1, 2, 2], _client.Queries.Select(q => q.Page));
        Assert.Equal(["a", "b"], vm.Items.Select(i => i.Id));
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.LoadMoreFailed);

        _client.Enqueue(Page(3, 3, "c"));
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["a", "b", "c"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task New_query_clears_a_failed_next_page()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.EnqueueFailure(new WallhavenException(WallhavenErrorKind.Network, "offline"));
        await vm.LoadMoreCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 3, "x"));
        _client.Enqueue(Page(2, 3, "y"));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.LoadMoreFailed);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Equal(["x", "y"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task RetryLoadMore_without_a_failure_loads_like_LoadMore()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 2, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(2, 2, "b"));

        await vm.RetryLoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["a", "b"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Purity_is_read_when_a_new_query_starts()
    {
        _apiKey.Key = "key-1";
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 3, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _settings.Settings = AppSettings.Default with { Purity = Purity.Sfw | Purity.Sketchy };
        _client.Enqueue(Page(2, 3, "b"));
        _client.Enqueue(Page(1, 3, "c"));

        await vm.LoadMoreCommand.ExecuteAsync(null);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([Purity.Sfw, Purity.Sfw, Purity.Sfw | Purity.Sketchy], _client.Queries.Select(q => q.Purity));
    }

    [Fact]
    public async Task Filters_change_triggers_reload()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));

        vm.Filters = new FeedFilters(RatioFilter.Wide16x9, ResolutionFilter.Any);

        Assert.Equal(2, _client.Queries.Count);
        Assert.Equal(RatioFilter.Wide16x9, _client.Queries[1].Ratio);
        Assert.Null(_client.Queries[1].AtLeast);
        Assert.Equal(["b"], vm.Items.Select(i => i.Id));

        vm.Filters = new FeedFilters(RatioFilter.Wide16x9, ResolutionFilter.Any);

        Assert.Equal(2, _client.Queries.Count);
    }

    [Fact]
    public async Task ResetFilters_restores_defaults()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));
        vm.Filters = new FeedFilters(RatioFilter.Portrait9x16, ResolutionFilter.Uhd, "#336600");
        _client.Enqueue(Page(1, 1, "c"));

        vm.ResetFiltersCommand.Execute(null);

        Assert.Equal(new FeedFilters(), vm.Filters);
        Assert.Equal(3, _client.Queries.Count);
        Assert.Equal(RatioFilter.Any, _client.Queries[2].Ratio);
        Assert.Null(_client.Queries[2].Color);

        vm.ResetFiltersCommand.Execute(null);

        Assert.Equal(3, _client.Queries.Count);
    }

    [Fact]
    public void Filter_choices_are_labelled_and_reflect_the_defaults()
    {
        var vm = CreateViewModel();

        Assert.Equal(["Filter_Any", "16:9", "21:9", "9:16"], vm.Ratios.Select(r => r.Label));
        Assert.Equal(
            [RatioFilter.Any, RatioFilter.Wide16x9, RatioFilter.Ultrawide21x9, RatioFilter.Portrait9x16],
            vm.Ratios.Select(r => r.Value));
        Assert.Equal(["Filter_Native", "Filter_4K", "Filter_Any"], vm.Resolutions.Select(r => r.Label));
        Assert.Equal(
            [ResolutionFilter.Native, ResolutionFilter.Uhd, ResolutionFilter.Any],
            vm.Resolutions.Select(r => r.Value));
        Assert.Equal(ColorCatalog.All.Select(c => "Color_" + c.Key), vm.Colors.Select(c => c.Label));
        Assert.Equal(ColorCatalog.All.Select(c => c.Hex), vm.Colors.Select(c => c.Value));

        Assert.Equal([true, false, false, false], vm.Ratios.Select(r => r.IsSelected));
        Assert.Equal([true, false, false], vm.Resolutions.Select(r => r.IsSelected));
        Assert.All(vm.Colors, c => Assert.False(c.IsSelected));
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public async Task Selecting_ratio_and_resolution_updates_filters_and_reloads()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));
        _client.Enqueue(Page(1, 1, "c"));

        vm.SelectRatioCommand.Execute(vm.Ratios[2]);
        vm.SelectResolutionCommand.Execute(vm.Resolutions[1]);

        Assert.Equal(new FeedFilters(RatioFilter.Ultrawide21x9, ResolutionFilter.Uhd), vm.Filters);
        Assert.Equal([false, false, true, false], vm.Ratios.Select(r => r.IsSelected));
        Assert.Equal([false, true, false], vm.Resolutions.Select(r => r.IsSelected));
        Assert.Equal(3, _client.Queries.Count);
        Assert.Equal(RatioFilter.Ultrawide21x9, _client.Queries[2].Ratio);

        vm.SelectRatioCommand.Execute(vm.Ratios[2]);
        vm.SelectRatioCommand.Execute(null);

        Assert.Equal(3, _client.Queries.Count);
    }

    [Fact]
    public async Task Selecting_a_colour_twice_clears_it()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));
        _client.Enqueue(Page(1, 1, "c"));
        _client.Enqueue(Page(1, 1, "d"));

        vm.SelectColorCommand.Execute(vm.Colors[1]);

        Assert.Equal(vm.Colors[1].Value, vm.Filters.Color);
        Assert.Equal(vm.Colors[1].Value, _client.Queries[1].Color);
        Assert.Equal([false, true, false, false, false, false, false, false], vm.Colors.Select(c => c.IsSelected));

        vm.SelectColorCommand.Execute(vm.Colors[4]);

        Assert.Equal(vm.Colors[4].Value, vm.Filters.Color);
        Assert.Equal([false, false, false, false, true, false, false, false], vm.Colors.Select(c => c.IsSelected));

        vm.SelectColorCommand.Execute(vm.Colors[4]);

        Assert.Null(vm.Filters.Color);
        Assert.Null(_client.Queries[3].Color);
        Assert.All(vm.Colors, c => Assert.False(c.IsSelected));
    }

    [Fact]
    public void Assigned_and_reset_filters_update_the_choices()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        _client.Enqueue(Page(1, 1, "b"));

        vm.Filters = new FeedFilters(RatioFilter.Portrait9x16, ResolutionFilter.Any, ColorCatalog.All[0].Hex);

        Assert.Equal([false, false, false, true], vm.Ratios.Select(r => r.IsSelected));
        Assert.Equal([false, false, true], vm.Resolutions.Select(r => r.IsSelected));
        Assert.True(vm.Colors[0].IsSelected);

        vm.ResetFiltersCommand.Execute(null);

        Assert.Equal([true, false, false, false], vm.Ratios.Select(r => r.IsSelected));
        Assert.Equal([true, false, false], vm.Resolutions.Select(r => r.IsSelected));
        Assert.All(vm.Colors, c => Assert.False(c.IsSelected));
    }

    [Fact]
    public void ToggleFilters_flips_panel_without_loading()
    {
        var vm = CreateViewModel();

        vm.ToggleFiltersCommand.Execute(null);
        Assert.True(vm.FiltersOpen);
        vm.ToggleFiltersCommand.Execute(null);

        Assert.False(vm.FiltersOpen);
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public void Open_and_quick_apply_raise_events()
    {
        var vm = CreateViewModel();
        var wallpaper = Wallpaper("a");
        WallpaperSummary? opened = null;
        WallpaperSummary? applied = null;
        vm.WallpaperOpened += (_, w) => opened = w;
        vm.QuickApplyRequested += (_, w) => applied = w;

        vm.OpenCommand.Execute(wallpaper);
        vm.QuickApplyCommand.Execute(wallpaper);

        Assert.Same(wallpaper, opened);
        Assert.Same(wallpaper, applied);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Without_a_key_only_sfw_is_requested(string? key)
    {
        _settings.Settings = AppSettings.Default with { Purity = Purity.Sfw | Purity.Sketchy | Purity.Nsfw };
        _apiKey.Key = key;
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(Purity.Sfw, Assert.Single(_client.Queries).Purity);
    }

    [Fact]
    public void Excluded_tags_start_from_the_settings()
    {
        _settings.Settings = AppSettings.Default with { ExcludedTags = ["people", "car"] };

        var vm = CreateViewModel();

        Assert.Equal(["people", "car"], vm.ExcludedTags);
        Assert.Equal(string.Empty, vm.ExclusionText);
    }

    [Fact]
    public async Task Excluded_tags_are_appended_to_every_query()
    {
        _settings.Settings = AppSettings.Default with { ExcludedTags = ["people"] };
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 2, "a"));
        _client.Enqueue(Page(2, 2, "b"));

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["-people", "-people"], _client.Queries.Select(q => q.Text));
    }

    [Fact]
    public async Task AddExclusion_saves_reloads_and_clears_the_box()
    {
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));
        vm.ExclusionText = " People ";

        vm.AddExclusionCommand.Execute(vm.ExclusionText);

        Assert.Equal(["people"], vm.ExcludedTags);
        Assert.Equal(["people"], _settings.Settings.ExcludedTags);
        Assert.Equal(string.Empty, vm.ExclusionText);
        Assert.Equal(2, _client.Queries.Count);
        Assert.Equal("-people", _client.Queries[1].Text);
        Assert.Equal(["b"], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public void AddExclusion_keeps_the_other_settings()
    {
        _settings.Settings = AppSettings.Default with { Theme = ThemePreference.Dark, Purity = Purity.Sfw | Purity.Sketchy };
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));

        vm.AddExclusionCommand.Execute("car");

        Assert.Equal(
            AppSettings.Default with { Theme = ThemePreference.Dark, Purity = Purity.Sfw | Purity.Sketchy, ExcludedTags = ["car"] },
            _settings.Settings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("digital art")]
    [InlineData("a&b")]
    public void AddExclusion_ignores_invalid_tags_and_keeps_the_box(string? tag)
    {
        var vm = CreateViewModel();
        vm.ExclusionText = tag ?? "x y";

        vm.AddExclusionCommand.Execute(tag);

        Assert.Empty(vm.ExcludedTags);
        Assert.Equal(tag ?? "x y", vm.ExclusionText);
        Assert.Equal(0, _settings.SaveAttempts);
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public void AddExclusion_of_an_excluded_tag_only_clears_the_box()
    {
        _settings.Settings = AppSettings.Default with { ExcludedTags = ["people"] };
        var vm = CreateViewModel();
        vm.ExclusionText = "#people";

        vm.AddExclusionCommand.Execute(vm.ExclusionText);

        Assert.Equal(["people"], vm.ExcludedTags);
        Assert.Equal(string.Empty, vm.ExclusionText);
        Assert.Equal(0, _settings.SaveAttempts);
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public void AddExclusion_stops_at_the_maximum()
    {
        var full = Enumerable.Range(0, TagExclusions.MaxCount).Select(i => "tag" + i).ToArray();
        _settings.Settings = AppSettings.Default with { ExcludedTags = full };
        var vm = CreateViewModel();
        vm.ExclusionText = "more";

        vm.AddExclusionCommand.Execute("more");

        Assert.Equal(full, vm.ExcludedTags);
        Assert.Equal("more", vm.ExclusionText);
        Assert.Empty(_client.Queries);
    }

    [Fact]
    public void RemoveExclusion_saves_and_reloads()
    {
        _settings.Settings = AppSettings.Default with { ExcludedTags = ["people", "car"] };
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));

        vm.RemoveExclusionCommand.Execute("people");

        Assert.Equal(["car"], vm.ExcludedTags);
        Assert.Equal(["car"], _settings.Settings.ExcludedTags);
        Assert.Equal("-car", Assert.Single(_client.Queries).Text);

        vm.RemoveExclusionCommand.Execute("people");
        vm.RemoveExclusionCommand.Execute(null);

        Assert.Single(_client.Queries);
    }

    [Fact]
    public void Exclusions_apply_for_the_session_when_saving_fails()
    {
        _settings.SaveFailure = new IOException("disk full");
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));

        vm.AddExclusionCommand.Execute("people");

        Assert.Equal(["people"], vm.ExcludedTags);
        Assert.Empty(_settings.Settings.ExcludedTags);
        Assert.Equal("-people", Assert.Single(_client.Queries).Text);
    }

    [Fact]
    public async Task ResetFilters_keeps_excluded_tags()
    {
        _settings.Settings = AppSettings.Default with { ExcludedTags = ["people"] };
        var vm = CreateViewModel();
        _client.Enqueue(Page(1, 1, "a"));
        await vm.LoadCommand.ExecuteAsync(null);
        _client.Enqueue(Page(1, 1, "b"));
        vm.Filters = new FeedFilters(RatioFilter.Wide16x9);
        _client.Enqueue(Page(1, 1, "c"));

        vm.ResetFiltersCommand.Execute(null);

        Assert.Equal(["people"], vm.ExcludedTags);
        Assert.Equal("-people", _client.Queries[^1].Text);
    }

    private FeedViewModel CreateViewModel() =>
        new(_client, new FeedRequestFactory(() => (1920, 1080)), _settings, _apiKey, _localizer)
        {
            // Deterministic: a refresh of a sorted feed starts from page 1 unless a test says otherwise.
            PickRefreshPage = (_, _) => 1,
        };

    private static SearchPage Page(int current, int last, params string[] ids) =>
        new([.. ids.Select(Wallpaper)], current, last, Seed: null);

    private static SearchPage SeededPage(int current, int last, string seed, params string[] ids) =>
        new([.. ids.Select(Wallpaper)], current, last, seed);

    private static WallpaperSummary Wallpaper(string id) =>
        new(
            id,
            new Uri($"https://wallhaven.cc/w/{id}"),
            new Uri($"https://w.wallhaven.cc/full/{id}.jpg"),
            new Uri($"https://th.wallhaven.cc/lg/{id}.jpg"),
            1920,
            1080,
            1024,
            "image/jpeg",
            "1.78",
            []);
}
