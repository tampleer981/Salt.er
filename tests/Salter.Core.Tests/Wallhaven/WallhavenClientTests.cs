using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Time.Testing;
using Salter.Core.Tests.TestDoubles;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Wallhaven;

public sealed class WallhavenClientTests : IDisposable
{
    private static readonly Uri BaseAddress = new("https://wallhaven.cc/api/v1/");

    private static readonly SearchQuery Query =
        new("nature", Categories.General, Purity.Sfw, Sorting.Toplist, RatioFilter.Any, null, null);

    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(20);

    private readonly FakeTimeProvider _time = new();
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private WallhavenClient CreateClient(StubHttpHandler stub, string? apiKey = null)
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = false,
        });
        var pipeline = new ApiKeyHandler(new FixedApiKeyProvider(apiKey))
        {
            InnerHandler = new RetryAfterHandler(_time)
            {
                InnerHandler = new RateLimitingHandler(limiter)
                {
                    InnerHandler = new AttemptTimeoutHandler(_time, AttemptTimeout) { InnerHandler = stub },
                },
            },
        };
        var http = new HttpClient(pipeline) { BaseAddress = BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        _disposables.Add(http);
        _disposables.Add(limiter);
        return new WallhavenClient(http);
    }

    [Fact]
    public async Task Search_requests_expected_url()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Fixture("search.json"));
        var client = CreateClient(stub);

        await client.SearchAsync(Query, TestContext.Current.CancellationToken);

        var request = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(BaseAddress, QueryStringBuilder.Build(Query)), request.RequestUri);
    }

    [Fact]
    public async Task Search_returns_parsed_page()
    {
        var client = CreateClient(new StubHttpHandler(_ => StubHttpHandler.Fixture("search.json")));

        var page = await client.SearchAsync(Query, TestContext.Current.CancellationToken);

        Assert.Equal(["94x38z", "x8d2l3"], page.Items.Select(i => i.Id));
        Assert.Equal(40, page.LastPage);
    }

    [Fact]
    public async Task GetWallpaper_requests_escaped_id_and_returns_details()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Fixture("wallpaper.json"));
        var client = CreateClient(stub);

        var details = await client.GetWallpaperAsync("94x38z", TestContext.Current.CancellationToken);
        await client.GetWallpaperAsync("a/b?c", TestContext.Current.CancellationToken);

        Assert.Equal("94x38z", details.Summary.Id);
        Assert.Equal("https://wallhaven.cc/api/v1/w/94x38z", stub.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("https://wallhaven.cc/api/v1/w/a%2Fb%3Fc", stub.Requests[1].RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetWallpaper_rejects_blank_id(string id)
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Fixture("wallpaper.json"));
        var client = CreateClient(stub);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetWallpaperAsync(id, TestContext.Current.CancellationToken));
        Assert.Empty(stub.Requests);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("k", "k")]
    public async Task ApiKey_header_sent_only_when_configured(string? key, string? expected)
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Fixture("search.json"));
        var client = CreateClient(stub, key);

        await client.SearchAsync(Query, TestContext.Current.CancellationToken);

        var request = Assert.Single(stub.Requests);
        if (expected is null)
        {
            Assert.False(request.Headers.Contains("X-API-Key"));
        }
        else
        {
            Assert.Equal([expected], request.Headers.GetValues("X-API-Key"));
        }
    }

    [Fact]
    public async Task RetryAfter_retries_then_succeeds()
    {
        var stub = StubHttpHandler.Sequence(
            () => StubHttpHandler.TooManyRequests(TimeSpan.FromSeconds(2)),
            () => StubHttpHandler.Fixture("search.json"));
        var client = CreateClient(stub);

        var search = client.SearchAsync(Query, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        _time.Advance(TimeSpan.FromSeconds(2));
        var page = await search.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task RetryAfter_longer_than_attempt_timeout_is_still_retried()
    {
        var stub = StubHttpHandler.Sequence(
            () => StubHttpHandler.TooManyRequests(TimeSpan.FromSeconds(30)),
            () => StubHttpHandler.Fixture("search.json"));
        var client = CreateClient(stub);

        var search = client.SearchAsync(Query, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        _time.Advance(TimeSpan.FromSeconds(30));
        var page = await search.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task Unanswered_attempt_times_out_as_Network_after_twenty_seconds()
    {
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Status(HttpStatusCode.OK);
        });
        var client = CreateClient(stub);

        var search = client.SearchAsync(Query, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        _time.Advance(AttemptTimeout - TimeSpan.FromMilliseconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(search.IsCompleted);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => search.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
        Assert.IsType<TimeoutException>(ex.InnerException?.InnerException);
    }

    [Fact]
    public async Task RetryAfter_gives_up_after_two_retries()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.TooManyRequests(TimeSpan.FromSeconds(1)));
        var client = CreateClient(stub);

        var search = client.SearchAsync(Query, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        _time.Advance(TimeSpan.FromSeconds(1));
        await stub.WaitForRequestsAsync(2);
        _time.Advance(TimeSpan.FromSeconds(1));
        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => search.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.RateLimited, ex.Kind);
        Assert.Equal(3, stub.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Unauthorized_maps_to_Unauthorized_kind(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpHandler(_ => StubHttpHandler.Status(status)), "revoked");

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.SearchAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Unauthorized, ex.Kind);
    }

    [Fact]
    public async Task NotFound_maps()
    {
        var client = CreateClient(new StubHttpHandler(_ => StubHttpHandler.Status(HttpStatusCode.NotFound)));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.GetWallpaperAsync("missing", TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.NotFound, ex.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Server_maps(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpHandler(_ => StubHttpHandler.Status(status)));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.SearchAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Server, ex.Kind);
    }

    [Fact]
    public async Task Network_maps()
    {
        var failure = new HttpRequestException("connection refused");
        var client = CreateClient(new StubHttpHandler(_ => throw failure));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.SearchAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
        Assert.Same(failure, ex.InnerException);
    }

    [Fact]
    public async Task Timeout_maps_to_Network()
    {
        var client = CreateClient(new StubHttpHandler(
            _ => throw new TaskCanceledException("timed out", new TimeoutException())));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.SearchAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Status(HttpStatusCode.OK);
        });
        var client = CreateClient(stub);

        var search = client.SearchAsync(Query, cts.Token);
        await stub.WaitForRequestsAsync(1);
        await cts.CancelAsync();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
        Assert.Equal(cts.Token, ex.CancellationToken);
    }

    [Fact]
    public async Task Invalid_json_maps_to_InvalidResponse()
    {
        var client = CreateClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<html>")),
        }));

        var ex = await Assert.ThrowsAsync<WallhavenException>(
            () => client.SearchAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(WallhavenErrorKind.InvalidResponse, ex.Kind);
    }
}
