using System.Threading.RateLimiting;

namespace Salter.Core.Wallhaven;

/// <summary>Waits for a permit from the shared wallhaven rate limiter before every request attempt.</summary>
internal sealed class RateLimitingHandler(RateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            throw new WallhavenException(
                WallhavenErrorKind.RateLimited, "Too many wallhaven requests are waiting; try again shortly.");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
