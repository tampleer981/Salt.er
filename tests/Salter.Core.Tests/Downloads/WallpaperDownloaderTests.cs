using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Salter.Core.Downloads;
using Salter.Core.Tests.TestDoubles;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Downloads;

public sealed class WallpaperDownloaderTests : IDisposable
{
    private static readonly byte[] Image = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static WallpaperSummary Wallpaper(string id = "abc123", string fileType = "image/jpeg") => new(
        id,
        new Uri($"https://wallhaven.cc/w/{id}"),
        new Uri($"https://w.wallhaven.cc/full/ab/wallhaven-{id}.jpg"),
        new Uri($"https://th.wallhaven.cc/lg/ab/{id}.jpg"),
        1920,
        1080,
        Image.Length,
        fileType,
        "1.78",
        []);

    private static HttpResponseMessage Ok(HttpContent content) => new(HttpStatusCode.OK) { Content = content };

    private static (WallpaperDownloader Downloader, StubHttpClientFactory Factory) Create(
        StubHttpHandler stub, TimeProvider? time = null, ILogger<WallpaperDownloader>? logger = null)
    {
        var factory = new StubHttpClientFactory(stub);
        return (new WallpaperDownloader(factory, time ?? TimeProvider.System, logger), factory);
    }

    private static string[] FilesIn(string folder) =>
        Directory.Exists(folder) ? [.. Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>()] : [];

    [Fact]
    public async Task Downloads_to_wallhaven_id_ext()
    {
        var stub = new StubHttpHandler(_ => Ok(new ByteArrayContent(Image)));
        var (downloader, factory) = Create(stub);

        var path = await downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken);

        Assert.Equal(_temp.Combine("wallhaven-abc123.jpg"), path);
        Assert.Equal(Image, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(["wallhaven-abc123.jpg"], FilesIn(_temp.Path));
        var request = Assert.Single(stub.Requests);
        Assert.Equal(new Uri("https://w.wallhaven.cc/full/ab/wallhaven-abc123.jpg"), request.RequestUri);
        Assert.Equal([ServiceCollectionExtensions.WallhavenImagesClientName], factory.RequestedNames);
    }

    [Fact]
    public async Task Uses_extension_from_file_type()
    {
        var (downloader, _) = Create(new StubHttpHandler(_ => Ok(new ByteArrayContent(Image))));

        var path = await downloader.DownloadAsync(
            Wallpaper(fileType: "image/png"), _temp.Path, TestContext.Current.CancellationToken);

        Assert.Equal("wallhaven-abc123.png", Path.GetFileName(path));
    }

    [Fact]
    public async Task Existing_file_is_not_downloaded_again()
    {
        var existing = _temp.Combine("wallhaven-abc123.jpg");
        await File.WriteAllBytesAsync(existing, [42], TestContext.Current.CancellationToken);
        var stub = new StubHttpHandler(_ => Ok(new ByteArrayContent(Image)));
        var (downloader, _) = Create(stub);

        var path = await downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken);

        Assert.Equal(existing, path);
        Assert.Empty(stub.Requests);
        Assert.Equal([42], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Creates_missing_folder()
    {
        var folder = _temp.Combine("nested", "Salt.er");
        var (downloader, _) = Create(new StubHttpHandler(_ => Ok(new ByteArrayContent(Image))));

        var path = await downloader.DownloadAsync(Wallpaper(), folder, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, "wallhaven-abc123.jpg"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Failed_download_leaves_no_partial_file()
    {
        var stub = new StubHttpHandler(_ => Ok(new StreamContent(new FailingStream(Image, new IOException("disk full")))));
        var (downloader, _) = Create(stub);

        var ex = await Record.ExceptionAsync(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.True(
            ex is IOException || ex is WallhavenException { Kind: WallhavenErrorKind.Network },
            $"Unexpected exception: {ex}");
        Assert.Empty(FilesIn(_temp.Path));
    }

    [Fact]
    public async Task Connection_lost_mid_body_maps_to_Network_and_leaves_no_partial_file()
    {
        var failure = new HttpIOException(HttpRequestError.ResponseEnded, "connection lost");
        var stub = new StubHttpHandler(_ => Ok(new StreamContent(new FailingStream(Image, failure))));
        var (downloader, _) = Create(stub);

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
        Assert.Empty(FilesIn(_temp.Path));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, WallhavenErrorKind.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, WallhavenErrorKind.Server)]
    [InlineData(HttpStatusCode.Forbidden, WallhavenErrorKind.Server)]
    public async Task Error_status_maps_to_kind(HttpStatusCode status, WallhavenErrorKind kind)
    {
        var (downloader, _) = Create(new StubHttpHandler(_ => StubHttpHandler.Status(status)));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.Equal(kind, ex.Kind);
        Assert.Empty(FilesIn(_temp.Path));
    }

    [Fact]
    public async Task Unreachable_host_maps_to_Network()
    {
        var (downloader, _) = Create(new StubHttpHandler(_ => throw new HttpRequestException("no route")));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Timeout_maps_to_Network()
    {
        var (downloader, _) = Create(new StubHttpHandler(
            (_, _) => throw new TaskCanceledException("timed out", new TimeoutException())));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_mid_body_throws_OperationCanceled_and_leaves_no_partial_file()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stub = new StubHttpHandler(_ =>
        {
            cts.Cancel();
            return Ok(new StreamContent(new FailingStream(Image, new OperationCanceledException(cts.Token))));
        });
        var (downloader, _) = Create(stub);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, cts.Token));

        Assert.Empty(FilesIn(_temp.Path));
    }

    [Fact]
    public async Task File_created_concurrently_wins_and_temp_file_is_removed()
    {
        var target = _temp.Combine("wallhaven-abc123.jpg");
        var stub = new StubHttpHandler(_ =>
        {
            File.WriteAllBytes(target, [42]);
            return Ok(new ByteArrayContent(Image));
        });
        var (downloader, _) = Create(stub);

        var path = await downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken);

        Assert.Equal(target, path);
        Assert.Equal([42], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(["wallhaven-abc123.jpg"], FilesIn(_temp.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_folder_is_rejected(string folder)
    {
        var (downloader, _) = Create(new StubHttpHandler(_ => Ok(new ByteArrayContent(Image))));

        await Assert.ThrowsAsync<ArgumentException>(
            () => downloader.DownloadAsync(Wallpaper(), folder, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task Unsafe_id_is_rejected(string id)
    {
        var stub = new StubHttpHandler(_ => Ok(new ByteArrayContent(Image)));
        var (downloader, _) = Create(stub);

        await Assert.ThrowsAsync<ArgumentException>(
            () => downloader.DownloadAsync(Wallpaper(id), _temp.Path, TestContext.Current.CancellationToken));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Stalled_download_times_out_after_120_seconds_as_Network_and_leaves_no_partial_file()
    {
        var time = new FakeTimeProvider();
        var stub = new StubHttpHandler(_ => Ok(new StreamContent(new StallingStream())));
        var (downloader, _) = Create(stub, time);

        var download = downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        time.Advance(TimeSpan.FromSeconds(119));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(download.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        var ex = await Assert.ThrowsAsync<WallhavenException>(() => download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
        Assert.Empty(FilesIn(_temp.Path));
    }

    [Fact]
    public async Task Caller_cancellation_of_stalled_download_throws_OperationCanceled_without_warning()
    {
        var logger = new RecordingLogger<WallpaperDownloader>();
        var stub = new StubHttpHandler(_ => Ok(new StreamContent(new StallingStream())));
        var (downloader, _) = Create(stub, new FakeTimeProvider(), logger);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var download = downloader.DownloadAsync(Wallpaper(), _temp.Path, cts.Token);
        await stub.WaitForRequestsAsync(1);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Empty(FilesIn(_temp.Path));
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Failed_download_logs_a_warning()
    {
        var logger = new RecordingLogger<WallpaperDownloader>();
        var (downloader, _) = Create(new StubHttpHandler(_ => StubHttpHandler.Status(HttpStatusCode.NotFound)), logger: logger);

        await Assert.ThrowsAsync<WallhavenException>(
            () => downloader.DownloadAsync(Wallpaper(), _temp.Path, TestContext.Current.CancellationToken));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("abc123", StringComparison.Ordinal));
    }
}
