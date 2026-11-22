using System.Net;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Time.Testing;
using Salter.Core.Tests.TestDoubles;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests.Wallhaven;

public class HandlersTests
{
    private static readonly Uri Endpoint = new("https://wallhaven.cc/api/v1/search?page=1");

    private static Task<HttpResponseMessage> Get(HttpMessageInvoker invoker, CancellationToken ct = default) =>
        invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Endpoint), ct);

    // Replenishment is manual; the bucket refills in proportion to elapsed time, so a short period
    // makes each TryReplenish() add a full period's tokens.
    private static TokenBucketRateLimiter ManualLimiter(int tokens, int queue) => new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = tokens,
        TokensPerPeriod = tokens,
        ReplenishmentPeriod = TimeSpan.FromMilliseconds(1),
        QueueLimit = queue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = false,
    });

    [Fact]
    public async Task RateLimiter_blocks_46th_request_within_a_minute()
    {
        using var limiter = ManualLimiter(tokens: 2, queue: 10);
        var stub = new StubHttpHandler(_ => StubHttpHandler.Status(HttpStatusCode.OK));
        using var invoker = new HttpMessageInvoker(new RateLimitingHandler(limiter) { InnerHandler = stub });
        var ct = TestContext.Current.CancellationToken;

        using var first = await Get(invoker, ct);
        using var second = await Get(invoker, ct);
        var third = Get(invoker, ct);
        await Task.Delay(100, ct);

        Assert.False(third.IsCompleted);
        Assert.Equal(2, stub.Requests.Count);

        Assert.True(limiter.TryReplenish());
        using var response = await third.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public async Task RateLimiter_rejection_throws_RateLimited()
    {
        using var limiter = ManualLimiter(tokens: 1, queue: 0);
        var stub = new StubHttpHandler(_ => StubHttpHandler.Status(HttpStatusCode.OK));
        using var invoker = new HttpMessageInvoker(new RateLimitingHandler(limiter) { InnerHandler = stub });
        var ct = TestContext.Current.CancellationToken;

        using var first = await Get(invoker, ct);
        var ex = await Assert.ThrowsAsync<WallhavenException>(() => Get(invoker, ct));

        Assert.Equal(WallhavenErrorKind.RateLimited, ex.Kind);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task RetryAfter_uses_http_date_relative_to_time_provider()
    {
        var time = new FakeTimeProvider();
        var stub = StubHttpHandler.Sequence(
            () =>
            {
                var response = StubHttpHandler.TooManyRequests();
                response.Headers.RetryAfter = new RetryConditionHeaderValue(time.GetUtcNow().AddSeconds(5));
                return response;
            },
            () => StubHttpHandler.Status(HttpStatusCode.OK));

        await AssertRetriesAfter(time, stub, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RetryAfter_defaults_to_ten_seconds_when_header_missing()
    {
        var time = new FakeTimeProvider();
        var stub = StubHttpHandler.Sequence(
            () => StubHttpHandler.TooManyRequests(),
            () => StubHttpHandler.Status(HttpStatusCode.OK));

        await AssertRetriesAfter(time, stub, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RetryAfter_caps_delay_at_sixty_seconds()
    {
        var time = new FakeTimeProvider();
        var stub = StubHttpHandler.Sequence(
            () => StubHttpHandler.TooManyRequests(TimeSpan.FromHours(1)),
            () => StubHttpHandler.Status(HttpStatusCode.OK));

        await AssertRetriesAfter(time, stub, TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task RetryAfter_returns_last_429_and_disposes_discarded_ones()
    {
        var time = new FakeTimeProvider();
        var contents = new List<TrackingContent>();
        var stub = new StubHttpHandler(_ =>
        {
            var content = new TrackingContent();
            contents.Add(content);
            var response = StubHttpHandler.TooManyRequests(TimeSpan.Zero);
            response.Content = content;
            return response;
        });
        using var invoker = new HttpMessageInvoker(new RetryAfterHandler(time) { InnerHandler = stub });

        using var response = await Get(invoker, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(3, stub.Requests.Count);
        Assert.Equal([true, true, false], contents.Select(c => c.Disposed));
    }

    [Fact]
    public async Task RetryAfter_wait_honours_cancellation()
    {
        var time = new FakeTimeProvider();
        var stub = new StubHttpHandler(_ => StubHttpHandler.TooManyRequests(TimeSpan.FromSeconds(30)));
        using var invoker = new HttpMessageInvoker(new RetryAfterHandler(time) { InnerHandler = stub });
        using var cts = new CancellationTokenSource();

        var send = Get(invoker, cts.Token);
        await stub.WaitForRequestsAsync(1);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task AttemptTimeout_throws_TaskCanceled_with_TimeoutException_on_expiry()
    {
        var time = new FakeTimeProvider();
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Status(HttpStatusCode.OK);
        });
        using var invoker = new HttpMessageInvoker(
            new AttemptTimeoutHandler(time, TimeSpan.FromSeconds(20)) { InnerHandler = stub });

        var send = Get(invoker, TestContext.Current.CancellationToken);
        await stub.WaitForRequestsAsync(1);
        time.Advance(TimeSpan.FromSeconds(20));

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => send.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task AttemptTimeout_passes_caller_cancellation_through()
    {
        var time = new FakeTimeProvider();
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Status(HttpStatusCode.OK);
        });
        using var invoker = new HttpMessageInvoker(
            new AttemptTimeoutHandler(time, TimeSpan.FromSeconds(20)) { InnerHandler = stub });
        using var cts = new CancellationTokenSource();

        var send = Get(invoker, cts.Token);
        await stub.WaitForRequestsAsync(1);
        await cts.CancelAsync();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.False(ex.InnerException is TimeoutException);
        Assert.Single(stub.Requests);
    }

    private static async Task AssertRetriesAfter(FakeTimeProvider time, StubHttpHandler stub, TimeSpan expected)
    {
        using var invoker = new HttpMessageInvoker(new RetryAfterHandler(time) { InnerHandler = stub });
        var ct = TestContext.Current.CancellationToken;

        var send = Get(invoker, ct);
        await stub.WaitForRequestsAsync(1);
        time.Advance(expected - TimeSpan.FromMilliseconds(1));
        await Task.Delay(50, ct);

        Assert.False(send.IsCompleted);
        Assert.Single(stub.Requests);

        time.Advance(TimeSpan.FromMilliseconds(1));
        using var response = await send.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
    }

    private sealed class TrackingContent : ByteArrayContent
    {
        public TrackingContent()
            : base([])
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
