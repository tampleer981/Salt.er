using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Salter.Core.Downloads;
using Salter.Core.Wallhaven;

namespace Salter.Core;

/// <summary>Registers the <c>Salter.Core</c> services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The service key of the <see cref="RateLimiter"/> shared by all wallhaven API requests.</summary>
    public const string WallhavenRateLimiterKey = "wallhaven";

    /// <summary>The name of the <see cref="HttpClient"/> that downloads full-resolution images.</summary>
    internal const string WallhavenImagesClientName = "wallhaven-images";

    private static readonly Uri WallhavenBaseAddress = new("https://wallhaven.cc/api/v1/");
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ImageDownloadTimeout = TimeSpan.FromSeconds(60);
    private static readonly ProductInfoHeaderValue UserAgent = new("Salt.er", ProductVersion());

    /// <summary>
    /// Registers <see cref="IWallhavenClient"/> as a typed <see cref="HttpClient"/> with API key, retry,
    /// rate-limiting (45 requests per minute) and per-attempt timeout (20 s) handlers;
    /// <see cref="IWallpaperDownloader"/> over a separate image client (60 s timeout, no API key or rate limit,
    /// since images are served from a different host); and <see cref="TimeProvider.System"/> unless a
    /// <see cref="TimeProvider"/> is already registered.
    /// </summary>
    /// <remarks>The host must register an <see cref="IApiKeyProvider"/>; none is registered here.</remarks>
    /// <param name="services">The service collection.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddSalterCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddKeyedSingleton<RateLimiter>(
            WallhavenRateLimiterKey,
            (_, _) => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 45,
                TokensPerPeriod = 45,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));

        services.AddHttpClient<IWallhavenClient, WallhavenClient>(http =>
            {
                http.BaseAddress = WallhavenBaseAddress;
                // Each attempt is limited by AttemptTimeoutHandler instead, so rate-limit queueing and
                // Retry-After waits do not count against the timeout.
                http.Timeout = Timeout.InfiniteTimeSpan;
                http.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
            .AddHttpMessageHandler(sp => new ApiKeyHandler(sp.GetRequiredService<IApiKeyProvider>()))
            .AddHttpMessageHandler(sp => new RetryAfterHandler(sp.GetRequiredService<TimeProvider>()))
            .AddHttpMessageHandler(sp => new RateLimitingHandler(
                sp.GetRequiredKeyedService<RateLimiter>(WallhavenRateLimiterKey)))
            .AddHttpMessageHandler(sp => new AttemptTimeoutHandler(sp.GetRequiredService<TimeProvider>(), AttemptTimeout));

        services.AddHttpClient(WallhavenImagesClientName, http =>
            {
                http.Timeout = ImageDownloadTimeout;
                http.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);
        services.TryAddSingleton<IWallpaperDownloader, WallpaperDownloader>();

        return services;
    }

    // Clients may be captured by singletons; recycle connections to pick up DNS changes.
    private static SocketsHttpHandler CreatePrimaryHandler() => new() { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

    private static string ProductVersion()
    {
        var version = typeof(ServiceCollectionExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? version : version[..metadata];
    }
}
