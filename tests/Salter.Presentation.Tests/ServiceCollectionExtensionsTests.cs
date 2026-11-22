using Microsoft.Extensions.DependencyInjection;
using Salter.Core.Downloads;
using Salter.Core.Feed;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Tests.TestDoubles;
using Salter.Presentation.ViewModels;
using static Salter.Presentation.Tests.TestDoubles.TestData;

namespace Salter.Presentation.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Resolves_the_shell_with_shared_singletons()
    {
        using var provider = CreateServices(new FakeWallpaperService()).BuildServiceProvider(validateScopes: true);

        var shell = provider.GetRequiredService<ShellViewModel>();

        Assert.Same(shell, provider.GetRequiredService<ShellViewModel>());
        Assert.Same(provider.GetRequiredService<FeedViewModel>(), shell.Feed);
        Assert.Same(provider.GetRequiredService<DetailViewModel>(), shell.Detail);
        Assert.Same(provider.GetRequiredService<SettingsViewModel>(), shell.Settings);
        Assert.Same(provider.GetRequiredService<ToastViewModel>(), shell.Toast);
        Assert.Same(shell.Toast, provider.GetRequiredService<IToastService>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Feed_requests_use_the_primary_monitor_resolution()
    {
        var wallpapers = new FakeWallpaperService();
        wallpapers.Monitors.AddRange([Display2, Display1]);
        using var provider = CreateServices(wallpapers).BuildServiceProvider();
        var factory = provider.GetRequiredService<FeedRequestFactory>();

        var query = factory.Create(GenreCatalog.Default, null, Sorting.Toplist, new FeedFilters { Resolution = ResolutionFilter.Native }, Purity.Sfw, 1, null);

        Assert.Equal("2560x1440", query.AtLeast);
    }

    [Fact]
    public void Feed_requests_without_a_primary_monitor_have_no_minimum_resolution()
    {
        using var provider = CreateServices(new FakeWallpaperService()).BuildServiceProvider();
        var factory = provider.GetRequiredService<FeedRequestFactory>();

        var query = factory.Create(GenreCatalog.Default, null, Sorting.Toplist, new FeedFilters { Resolution = ResolutionFilter.Native }, Purity.Sfw, 1, null);

        Assert.Null(query.AtLeast);
    }

    private static ServiceCollection CreateServices(FakeWallpaperService wallpapers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWallhavenClient>(new FakeWallhavenClient());
        services.AddSingleton<IWallpaperDownloader>(new FakeDownloader());
        services.AddSingleton<ISettingsStore>(new FakeSettingsStore());
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddSingleton<IApiKeyProvider>(new FakeApiKeyProvider());
        services.AddSingleton<IWallpaperService>(wallpapers);
        services.AddSingleton<IStartupRegistration>(new FakeStartupRegistration());
        services.AddSingleton<IFolderPicker>(new FakeFolderPicker());
        services.AddSingleton<ILocalizer>(new FakeLocalizer());
        services.AddSalterPresentation();
        return services;
    }
}
