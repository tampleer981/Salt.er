using System.Net;

namespace Salter.Core.Wallhaven;

/// <summary>
/// Retries a request answered with HTTP 429 after the delay in its <c>Retry-After</c> header
/// (10 s when absent, at most 60 s), up to two times; the last 429 is returned unchanged.
/// </summary>
internal sealed class RetryAfterHandler(TimeProvider time) : DelegatingHandler
{
    internal const int MaxRetries = 2;
    internal static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxRetries)
            {
                return response;
            }

            var delay = DelayFor(response);
            response.Dispose();
            await Task.Delay(delay, time, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan DelayFor(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - time.GetUtcNow() : DefaultDelay);
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxDelay ? MaxDelay : delay;
    }
}
