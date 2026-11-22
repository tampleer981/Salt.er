using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Salter.Core.Downloads;
using Salter.Core.Tests.TestDoubles;
using Salter.Core.Wallhaven;

namespace Salter.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    private static readonly SearchQuery Query =
        new(null, Categories.General, Purity.Sfw, Sorting.Latest, RatioFilter.Any, null, null);

    [Fact]
    public async Task AddSalterCore_resolves_client()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Fixture("search.json"));
        var services = new ServiceCollection();
        services.AddSingleton<IApiKeyProvider>(new FixedApiKeyProvider("secret"));
        services.AddSalterCore();
        services.AddHttpClient(nameof(IWallhavenClient)).ConfigurePrimaryHttpMessageHandler(() => stub);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var client = provider.GetRequiredService<IWallhavenClient>();
        await client.SearchAsync(Query, TestContext.Current.CancellationToken);

        var request = Assert.Single(stub.Requests);
        Assert.Equal(new Uri("https://wallhaven.cc/api/v1/" + QueryStringBuilder.Build(Query)), request.RequestUri);
        Assert.Equal(["secret"], request.Headers.GetValues("X-API-Key"));
        var agent = Assert.Single(request.Headers.UserAgent);
        Assert.Equal("Salt.er", agent.Product!.Name);
        Assert.False(string.IsNullOrEmpty(agent.Product.Version));
        Assert.DoesNotContain('+', agent.Product.Version);
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddSalterCore_registers_wallhaven_rate_limiter_with_45_tokens()
    {
        var services = new ServiceCollection();
        services.AddSalterCore();
        using var provider = services.BuildServiceProvider();

        var limiter = provider.GetRequiredKeyedService<RateLimiter>(ServiceCollectionExtensions.WallhavenRateLimiterKey);

        var stats = limiter.GetStatistics();
        Assert.NotNull(stats);
        Assert.Equal(45, stats.CurrentAvailablePermits);
        Assert.Same(limiter, provider.GetRequiredKeyedService<RateLimiter>(ServiceCollectionExtensions.WallhavenRateLimiterKey));
    }

    [Fact]
    public void AddSalterCore_leaves_timeouts_to_the_attempt_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApiKeyProvider>(new FixedApiKeyProvider(null));
        services.AddSalterCore();
        using var provider = services.BuildServiceProvider();

        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IWallhavenClient));

        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
        Assert.Equal(new Uri("https://wallhaven.cc/api/v1/"), http.BaseAddress);
    }

    [Fact]
    public void AddSalterCore_builds_pipeline_in_order_over_pooled_sockets_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApiKeyProvider>(new FixedApiKeyProvider(null));
        services.AddSalterCore();
        using var provider = services.BuildServiceProvider();

        using var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(IWallhavenClient));

        var chain = new List<HttpMessageHandler>();
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(current);
        }

        Type[] ours = [typeof(ApiKeyHandler), typeof(RetryAfterHandler), typeof(RateLimitingHandler), typeof(AttemptTimeoutHandler)];
        Assert.Equal(ours, chain.Select(h => h.GetType()).Where(ours.Contains));
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.Equal(TimeSpan.FromMinutes(5), primary.PooledConnectionLifetime);
    }

    [Fact]
    public async Task AddSalterCore_downloads_images_without_api_key_or_rate_limiter()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3]),
        });
        var services = new ServiceCollection();
        services.AddSingleton<IApiKeyProvider>(new FixedApiKeyProvider("secret"));
        services.AddSalterCore();
        services.AddHttpClient(ServiceCollectionExtensions.WallhavenImagesClientName)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var temp = new TempDirectory();
        var wallpaper = new WallpaperSummary(
            "abc123",
            new Uri("https://wallhaven.cc/w/abc123"),
            new Uri("https://w.wallhaven.cc/full/ab/wallhaven-abc123.png"),
            new Uri("https://th.wallhaven.cc/lg/ab/abc123.jpg"),
            1920,
            1080,
            3,
            "image/png",
            "1.78",
            []);

        var downloader = provider.GetRequiredService<IWallpaperDownloader>();
        var path = await downloader.DownloadAsync(wallpaper, temp.Path, TestContext.Current.CancellationToken);

        Assert.Same(downloader, provider.GetRequiredService<IWallpaperDownloader>());
        Assert.Equal(temp.Combine("wallhaven-abc123.png"), path);
        var request = Assert.Single(stub.Requests);
        Assert.False(request.Headers.Contains("X-API-Key"));
        Assert.Equal("Salt.er", Assert.Single(request.Headers.UserAgent).Product!.Name);
        var limiter = provider.GetRequiredKeyedService<RateLimiter>(ServiceCollectionExtensions.WallhavenRateLimiterKey);
        Assert.Equal(45, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public void AddSalterCore_images_client_has_60s_timeout_over_pooled_sockets_handler_only()
    {
        var services = new ServiceCollection();
        services.AddSalterCore();
        using var provider = services.BuildServiceProvider();

        using var http = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(ServiceCollectionExtensions.WallhavenImagesClientName);
        using var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(ServiceCollectionExtensions.WallhavenImagesClientName);

        Assert.Equal(TimeSpan.FromSeconds(60), http.Timeout);
        Assert.Null(http.BaseAddress);
        var chain = new List<HttpMessageHandler>();
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(current);
        }

        Type[] apiOnly = [typeof(ApiKeyHandler), typeof(RetryAfterHandler), typeof(RateLimitingHandler), typeof(AttemptTimeoutHandler)];
        Assert.DoesNotContain(chain, h => apiOnly.Contains(h.GetType()));
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.Equal(TimeSpan.FromMinutes(5), primary.PooledConnectionLifetime);
    }
}
