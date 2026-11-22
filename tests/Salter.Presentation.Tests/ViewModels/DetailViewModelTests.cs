using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Tests.TestDoubles;
using Salter.Presentation.ViewModels;
using static Salter.Presentation.Tests.TestDoubles.TestData;

namespace Salter.Presentation.Tests.ViewModels;

public class DetailViewModelTests
{
    private const string Folder = "/downloads";

    private readonly FakeWallhavenClient _client = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeWallpaperService _wallpapers = new();
    private readonly FakeSettingsStore _settings = new() { Settings = AppSettings.Default with { DownloadFolder = Folder } };
    private readonly RecordingToastService _toasts = new();

    public DetailViewModelTests()
    {
        _client.Details = (id, _) => Task.FromResult(Details(id, "nature", "forest"));
        _wallpapers.Monitors.Add(Display1);
    }

    [Fact]
    public async Task Open_shows_wallpaper_and_fetched_details()
    {
        var vm = CreateViewModel();
        var wallpaper = Wallpaper("abc123");

        await vm.OpenAsync(wallpaper, TestContext.Current.CancellationToken);

        Assert.Same(wallpaper, vm.Wallpaper);
        Assert.Equal(new Uri("https://wallhaven.cc/w/abc123"), vm.WallhavenPage);
        Assert.Equal(["nature", "forest"], vm.Tags);
        Assert.Equal("uploader-abc123", vm.Uploader);
        Assert.Equal(["abc123"], _client.DetailRequests);
    }

    [Fact]
    public async Task Summary_and_attribution_describe_the_wallpaper()
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(string.Empty, vm.Summary);
        Assert.Equal("Detail_Attribution|Detail_UnknownUploader", vm.Attribution);

        await vm.OpenAsync(Wallpaper("abc123") with { FileSize = 4_299_161 }, TestContext.Current.CancellationToken);

        Assert.Equal("Detail_Summary|1920 × 1080|4.1 MB", vm.Summary);
        Assert.Equal("Detail_Attribution|uploader-abc123", vm.Attribution);
        Assert.Contains(nameof(DetailViewModel.Summary), changed);
        Assert.Contains(nameof(DetailViewModel.Attribution), changed);
    }

    [Fact]
    public async Task Unknown_uploader_is_attributed_as_unknown()
    {
        _client.Details = (id, _) => Task.FromResult(new WallpaperDetails(Wallpaper(id), [], Uploader: null));
        var vm = CreateViewModel();

        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        Assert.Equal("Detail_Attribution|Detail_UnknownUploader", vm.Attribution);
    }

    [Fact]
    public async Task Single_monitor_hides_targets()
    {
        var vm = CreateViewModel();

        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        Assert.False(vm.ShowTargets);
        var all = Assert.Single(vm.Targets);
        Assert.Equal("Detail_AllDisplays", all.Label);
        Assert.Null(all.Value);
        Assert.True(all.IsSelected);
        Assert.Same(all, vm.SelectedTarget);
    }

    [Fact]
    public async Task Multi_monitor_lists_all_plus_each()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();

        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        Assert.True(vm.ShowTargets);
        Assert.Equal(["Detail_AllDisplays", "Display 1", "Display 2"], vm.Targets.Select(t => t.Label));
        Assert.Equal([null, Display1, Display2], vm.Targets.Select(t => t.Value));
        Assert.Equal([true, false, false], vm.Targets.Select(t => t.IsSelected));
    }

    [Fact]
    public async Task Selecting_a_target_moves_the_selection()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        vm.SelectTargetCommand.Execute(vm.Targets[2]);

        Assert.Same(vm.Targets[2], vm.SelectedTarget);
        Assert.Equal([false, false, true], vm.Targets.Select(t => t.IsSelected));
    }

    [Fact]
    public async Task Reopening_rebuilds_targets_with_all_selected()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        vm.SelectTargetCommand.Execute(vm.Targets[2]);
        _wallpapers.Monitors.RemoveAt(1);

        await vm.OpenAsync(Wallpaper("b"), TestContext.Current.CancellationToken);

        Assert.False(vm.ShowTargets);
        Assert.Null(Assert.Single(vm.Targets).Value);
        Assert.Same(vm.Targets[0], vm.SelectedTarget);
    }

    [Fact]
    public async Task Apply_downloads_then_sets_on_selected_monitor()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        var wallpaper = Wallpaper("abc123");
        await vm.OpenAsync(wallpaper, TestContext.Current.CancellationToken);
        vm.SelectTargetCommand.Execute(vm.Targets[2]);

        await vm.ApplyCommand.ExecuteAsync(null);

        var download = Assert.Single(_downloader.Calls);
        Assert.Same(wallpaper, download.Wallpaper);
        Assert.Equal(Folder, download.Folder);
        var set = Assert.Single(_wallpapers.Calls);
        Assert.Equal(Path.Combine(Folder, "wallhaven-abc123.jpg"), set.FilePath);
        Assert.Equal("\\\\?\\DISPLAY2", set.MonitorId);
    }

    [Fact]
    public async Task Apply_success_shows_toast_and_closes()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        var closed = 0;
        vm.Closed += (_, _) => closed++;
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        vm.SelectTargetCommand.Execute(vm.Targets[2]);

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal([("Toast_Applied|Display 2", ToastKind.Success)], _toasts.Shown);
        Assert.Equal(1, closed);
        Assert.False(vm.IsApplying);
    }

    [Fact]
    public async Task Apply_to_all_displays_uses_AppliedAll_toast()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(_wallpapers.Calls).MonitorId);
        Assert.Equal([("Toast_AppliedAll", ToastKind.Success)], _toasts.Shown);
    }

    [Fact]
    public async Task Apply_failure_shows_error_toast()
    {
        _downloader.Failure = new IOException("Disk full");
        var vm = CreateViewModel();
        var closed = 0;
        vm.Closed += (_, _) => closed++;
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal([("Toast_Failed", ToastKind.Error)], _toasts.Shown);
        Assert.Empty(_wallpapers.Calls);
        Assert.Equal(0, closed);
        Assert.False(vm.IsApplying);
        Assert.NotNull(vm.Wallpaper);
    }

    public static TheoryData<Exception, bool> ApplyFailures => new()
    {
        { new WallhavenException(WallhavenErrorKind.Network, "offline"), false },
        { new UnauthorizedAccessException("read-only"), false },
        { new IOException("disk full"), true },
        { new InvalidOperationException("desktop wallpaper unavailable"), true },
    };

    [Theory]
    [MemberData(nameof(ApplyFailures))]
    public async Task Download_and_platform_failures_show_error_toast(Exception failure, bool failWhileSetting)
    {
        if (failWhileSetting)
        {
            _wallpapers.Failure = failure;
        }
        else
        {
            _downloader.Failure = failure;
        }

        var vm = CreateViewModel();
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal([("Toast_Failed", ToastKind.Error)], _toasts.Shown);
        Assert.False(vm.IsApplying);
    }

    [Fact]
    public async Task Apply_while_applying_is_ignored()
    {
        var vm = CreateViewModel();
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        var gate = _downloader.Hold();

        var first = vm.ApplyQuickAsync(Wallpaper("b"));
        Assert.True(vm.IsApplying);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        var second = vm.ApplyCommand.ExecuteAsync(null);
        var third = vm.ApplyQuickAsync(Wallpaper("c"));
        Assert.True(second.IsCompleted);
        Assert.True(third.IsCompleted);
        gate.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(["b"], _downloader.Calls.Select(c => c.Wallpaper.Id));
        Assert.Single(_toasts.Shown);
        Assert.False(vm.IsApplying);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ApplyQuick_targets_all_monitors()
    {
        _wallpapers.Monitors.Add(Display2);
        var vm = CreateViewModel();
        var closed = 0;
        vm.Closed += (_, _) => closed++;

        await vm.ApplyQuickAsync(Wallpaper("q1"));

        Assert.Equal("q1", Assert.Single(_downloader.Calls).Wallpaper.Id);
        var set = Assert.Single(_wallpapers.Calls);
        Assert.Equal(Path.Combine(Folder, "wallhaven-q1.jpg"), set.FilePath);
        Assert.Null(set.MonitorId);
        Assert.Equal([("Toast_AppliedAll", ToastKind.Success)], _toasts.Shown);
        Assert.Equal(0, closed);
        Assert.Empty(_client.DetailRequests);
    }

    [Fact]
    public async Task Details_fetch_failure_still_allows_apply()
    {
        _client.Details = (_, _) => Task.FromException<WallpaperDetails>(new WallhavenException(WallhavenErrorKind.Server, "boom"));
        var vm = CreateViewModel();

        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        Assert.Empty(vm.Tags);
        Assert.Null(vm.Uploader);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        await vm.ApplyCommand.ExecuteAsync(null);
        Assert.Single(_wallpapers.Calls);
        Assert.Equal([("Toast_AppliedAll", ToastKind.Success)], _toasts.Shown);
    }

    [Fact]
    public async Task Opening_another_wallpaper_cancels_the_pending_details_fetch()
    {
        var pending = new TaskCompletionSource<WallpaperDetails>();
        CancellationToken firstToken = default;
        _client.Details = (_, ct) =>
        {
            firstToken = ct;
            return pending.Task.WaitAsync(ct);
        };
        var vm = CreateViewModel();
        var first = vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        _client.Details = (id, _) => Task.FromResult(Details(id, "second"));

        await vm.OpenAsync(Wallpaper("b"), TestContext.Current.CancellationToken);
        await first;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.Equal("b", vm.Wallpaper?.Id);
        Assert.Equal(["second"], vm.Tags);
        Assert.Equal("uploader-b", vm.Uploader);
    }

    [Fact]
    public async Task Late_details_for_a_previous_wallpaper_are_ignored()
    {
        var pending = new TaskCompletionSource<WallpaperDetails>();
        _client.Details = (_, _) => pending.Task;
        var vm = CreateViewModel();
        var first = vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        _client.Details = (id, _) => Task.FromResult(Details(id, "second"));
        await vm.OpenAsync(Wallpaper("b"), TestContext.Current.CancellationToken);

        pending.SetResult(Details("a", "stale"));
        await first;

        Assert.Equal(["second"], vm.Tags);
        Assert.Equal("uploader-b", vm.Uploader);
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        _client.Details = (_, ct) => new TaskCompletionSource<WallpaperDetails>().Task.WaitAsync(ct);
        var vm = CreateViewModel();
        using var cts = new CancellationTokenSource();

        var open = vm.OpenAsync(Wallpaper("a"), cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open);
    }

    [Fact]
    public async Task Close_raises_Closed_and_cancels_the_pending_details_fetch()
    {
        CancellationToken token = default;
        _client.Details = (_, ct) =>
        {
            token = ct;
            return new TaskCompletionSource<WallpaperDetails>().Task.WaitAsync(ct);
        };
        var vm = CreateViewModel();
        var closed = 0;
        vm.Closed += (_, _) => closed++;
        var open = vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);

        vm.CloseCommand.Execute(null);
        await open;

        Assert.Equal(1, closed);
        Assert.True(token.IsCancellationRequested);
        Assert.Empty(vm.Tags);
    }

    [Fact]
    public async Task Apply_finishing_after_another_open_does_not_close()
    {
        var vm = CreateViewModel();
        var closed = 0;
        vm.Closed += (_, _) => closed++;
        await vm.OpenAsync(Wallpaper("a"), TestContext.Current.CancellationToken);
        var gate = _downloader.Hold();
        var apply = vm.ApplyCommand.ExecuteAsync(null);
        vm.CloseCommand.Execute(null);
        CancellationToken token = default;
        _client.Details = (_, ct) =>
        {
            token = ct;
            return new TaskCompletionSource<WallpaperDetails>().Task.WaitAsync(ct);
        };
        _ = vm.OpenAsync(Wallpaper("b"), TestContext.Current.CancellationToken);

        gate.SetResult();
        await apply;

        Assert.Equal(1, closed);
        Assert.False(token.IsCancellationRequested);
        Assert.Equal([("Toast_AppliedAll", ToastKind.Success)], _toasts.Shown);
    }

    [Fact]
    public void Nothing_open_cannot_apply()
    {
        var vm = CreateViewModel();

        Assert.Null(vm.Wallpaper);
        Assert.False(vm.ApplyCommand.CanExecute(null));
    }

    private DetailViewModel CreateViewModel() =>
        new(_client, _downloader, _wallpapers, _settings, _toasts, new FakeLocalizer());
}
