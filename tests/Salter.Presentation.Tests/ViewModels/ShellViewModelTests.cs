using Microsoft.Extensions.Time.Testing;
using Salter.Core.Feed;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Tests.TestDoubles;
using Salter.Presentation.ViewModels;
using static Salter.Presentation.Tests.TestDoubles.TestData;

namespace Salter.Presentation.Tests.ViewModels;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly FakeWallhavenClient _client = new();
    private readonly FakeWallpaperService _wallpapers = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeSecretStore _secrets = new();
    private readonly FakeDownloader _downloader = new();
    private readonly ToastViewModel _toast = new(new FakeTimeProvider());
    private readonly ShellViewModel _shell;

    public ShellViewModelTests()
    {
        _client.Details = (id, _) => Task.FromResult(Details(id, "tag"));
        _wallpapers.Monitors.Add(Display1);
        var localizer = new FakeLocalizer();
        var feed = new FeedViewModel(_client, new FeedRequestFactory(() => null), _settings, new SecretStoreApiKeyProvider(_secrets), localizer);
        var detail = new DetailViewModel(_client, _downloader, _wallpapers, _settings, _toast, localizer);
        var settings = new SettingsViewModel(_settings, _secrets, new FakeStartupRegistration(), new FakeFolderPicker(), _toast, localizer);
        _shell = new ShellViewModel(feed, detail, settings, _toast);
    }

    public void Dispose() => _toast.Dispose();

    [Fact]
    public void Starts_on_the_feed()
    {
        Assert.Equal(ShellPage.Feed, _shell.Page);
        Assert.Same(_toast, _shell.Toast);
    }

    [Fact]
    public void Open_wallpaper_navigates_to_Detail()
    {
        var wallpaper = Wallpaper("abc123");

        _shell.Feed.OpenCommand.Execute(wallpaper);

        Assert.Equal(ShellPage.Detail, _shell.Page);
        Assert.Same(wallpaper, _shell.Detail.Wallpaper);
        Assert.Equal(["tag"], _shell.Detail.Tags);
    }

    [Fact]
    public void Escape_from_Detail_returns_to_Feed()
    {
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));

        Assert.True(_shell.HandleEscape());

        Assert.Equal(ShellPage.Feed, _shell.Page);
    }

    [Fact]
    public void Escape_from_Settings_returns_to_Feed()
    {
        _shell.ShowSettingsCommand.Execute(null);
        Assert.Equal(ShellPage.Settings, _shell.Page);

        Assert.True(_shell.HandleEscape());

        Assert.Equal(ShellPage.Feed, _shell.Page);
    }

    [Fact]
    public void Escape_from_Feed_returns_false()
    {
        Assert.False(_shell.HandleEscape());
        Assert.Equal(ShellPage.Feed, _shell.Page);
    }

    [Fact]
    public void Back_returns_to_Feed()
    {
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));
        _shell.BackCommand.Execute(null);
        Assert.Equal(ShellPage.Feed, _shell.Page);

        _shell.ShowSettingsCommand.Execute(null);
        _shell.BackCommand.Execute(null);
        Assert.Equal(ShellPage.Feed, _shell.Page);
    }

    [Fact]
    public async Task Successful_apply_returns_to_Feed()
    {
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));

        await _shell.Detail.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(ShellPage.Feed, _shell.Page);
        Assert.Equal("Toast_AppliedAll", _toast.Message);
    }

    [Fact]
    public async Task Late_apply_does_not_close_a_wallpaper_opened_since()
    {
        var gate = _downloader.Hold();
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));
        var apply = _shell.Detail.ApplyCommand.ExecuteAsync(null);
        _shell.BackCommand.Execute(null);
        var pending = new TaskCompletionSource<WallpaperDetails>();
        _client.Details = (_, ct) => pending.Task.WaitAsync(ct);
        _shell.Feed.OpenCommand.Execute(Wallpaper("b"));

        gate.SetResult();
        await apply;
        pending.SetResult(Details("b", "b-tag"));

        Assert.Equal(ShellPage.Detail, _shell.Page);
        Assert.Equal("b", _shell.Detail.Wallpaper?.Id);
        Assert.Equal(["b-tag"], _shell.Detail.Tags);
        Assert.Equal("Toast_AppliedAll", _toast.Message);
    }

    [Fact]
    public async Task Late_apply_does_not_leave_the_settings()
    {
        var gate = _downloader.Hold();
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));
        var apply = _shell.Detail.ApplyCommand.ExecuteAsync(null);
        _shell.ShowSettingsCommand.Execute(null);

        gate.SetResult();
        await apply;

        Assert.Equal(ShellPage.Settings, _shell.Page);
        Assert.Equal("Toast_AppliedAll", _toast.Message);
    }

    [Fact]
    public void Quick_apply_from_the_feed_sets_on_all_monitors_and_stays_on_Feed()
    {
        _shell.Feed.QuickApplyCommand.Execute(Wallpaper("q1"));

        Assert.Null(Assert.Single(_wallpapers.Calls).MonitorId);
        Assert.Equal(ShellPage.Feed, _shell.Page);
        Assert.Equal(ToastKind.Success, _toast.Kind);
    }

    [Fact]
    public void Settings_change_reloads_feed()
    {
        _client.Enqueue(new SearchPage([Wallpaper("a")], 1, 1, null));
        _shell.Settings.ApiKey = "key-1";

        _shell.Settings.ApplyApiKeyCommand.Execute(null);

        Assert.Equal(1, Assert.Single(_client.Queries).Page);
        Assert.Equal(FeedState.Ready, _shell.Feed.State);
        Assert.Equal("key-1", _secrets.Get(SecretNames.WallhavenApiKey));
    }

    [Fact]
    public async Task Excluding_a_tag_from_the_details_returns_to_the_reloaded_feed()
    {
        var wallpaper = Wallpaper("a");
        _shell.Feed.OpenCommand.Execute(wallpaper);
        await _shell.Detail.OpenAsync(wallpaper, TestContext.Current.CancellationToken);
        _client.Enqueue(new SearchPage([], 1, 1, Seed: null));

        _shell.Detail.ExcludeTagCommand.Execute("Tag");

        Assert.Equal(ShellPage.Feed, _shell.Page);
        Assert.Equal(["tag"], _shell.Feed.ExcludedTags);
        Assert.Equal(["tag"], _settings.Settings.ExcludedTags);
        Assert.Equal("-tag", _client.Queries[^1].Text);
        Assert.Equal("Toast_TagExcluded|tag", _toast.Message);
        Assert.Equal(ToastKind.Success, _toast.Kind);
    }

    [Fact]
    public void Tags_that_cannot_be_excluded_are_ignored()
    {
        _shell.Feed.OpenCommand.Execute(Wallpaper("a"));

        _shell.Detail.ExcludeTagCommand.Execute("digital art");

        Assert.Equal(ShellPage.Detail, _shell.Page);
        Assert.Empty(_shell.Feed.ExcludedTags);
        Assert.Null(_toast.Message);
    }
}
