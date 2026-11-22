using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Salter.Core.Feed;
using Salter.Presentation.Abstractions;
using Salter.Presentation.ViewModels;

namespace Salter.Presentation;

/// <summary>Registers the <c>Salter.Presentation</c> view models.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shell, feed, detail, settings and toast view models as singletons, with the toast view model
    /// also serving as <see cref="IToastService"/>; a <see cref="FeedRequestFactory"/> that reads the primary
    /// monitor's resolution from <see cref="IWallpaperService"/>; and <see cref="TimeProvider.System"/> unless a
    /// <see cref="TimeProvider"/> is already registered.
    /// </summary>
    /// <remarks>
    /// The host must register the Core services and the platform abstractions (<see cref="IWallpaperService"/>,
    /// <see cref="IStartupRegistration"/>, <see cref="IFolderPicker"/>, <see cref="ILocalizer"/>,
    /// <see cref="Salter.Core.Settings.ISettingsStore"/>, <see cref="Salter.Core.Settings.ISecretStore"/> and
    /// <see cref="Salter.Core.Wallhaven.IApiKeyProvider"/>).
    /// Resolve <see cref="ToastViewModel"/> (or anything depending on it) on the UI thread first, since it captures
    /// the current <see cref="SynchronizationContext"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddSalterPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(sp =>
        {
            var wallpapers = sp.GetRequiredService<IWallpaperService>();
            return new FeedRequestFactory(() =>
                wallpapers.GetMonitors().FirstOrDefault(m => m.IsPrimary) is { } primary
                    ? (primary.Width, primary.Height)
                    : null);
        });
        services.TryAddSingleton<FeedViewModel>();
        services.TryAddSingleton<DetailViewModel>();
        services.TryAddSingleton<SettingsViewModel>();
        services.TryAddSingleton<ToastViewModel>();
        services.TryAddSingleton<IToastService>(sp => sp.GetRequiredService<ToastViewModel>());
        services.TryAddSingleton<ShellViewModel>();

        return services;
    }
}
