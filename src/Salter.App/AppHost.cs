using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Salter.App.Services;
using Salter.Core;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation;
using Salter.Presentation.Abstractions;
using Serilog;
using Serilog.Events;

namespace Salter.App;

/// <summary>Builds the app's dependency injection container and logging.</summary>
internal static class AppHost
{
    /// <summary>Builds the host. Nothing is resolved yet.</summary>
    /// <returns>The host; dispose it on exit to flush the log.</returns>
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            // No appsettings.json, environment variables or console loggers: a tray app has no use for them.
            DisableDefaults = true,
        });

        builder.Logging.AddSerilog(CreateLogger(), dispose: true);

        builder.Services
            .AddSalterCore()
            .AddSalterPresentation()
            .AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(AppPaths.SettingsFile))
            .AddSingleton<ISecretStore, CredentialSecretStore>()
            .AddSingleton<IApiKeyProvider, SecretStoreApiKeyProvider>()
            .AddSingleton<ILocalizer, ResourceLocalizer>()
            // Both are first resolved on the UI thread, when the app starts: a window, and a picker it owns.
            .AddSingleton<FlyoutWindow>()
            .AddSingleton<IFolderPicker, FolderPicker>()
            .AddSingleton<IWallpaperService, DesktopWallpaperService>()
            .AddSingleton<UpdateService>()
            .AddSingleton<IStartupRegistration>(sp => new RunKeyStartupRegistration(
                RunKeyStartupRegistration.CurrentProcessPath,
                sp.GetRequiredService<ILogger<RunKeyStartupRegistration>>()));

        return builder.Build();
    }

    private static Serilog.Core.Logger CreateLogger() => new LoggerConfiguration()
        .MinimumLevel.Information()
        // Request logs carry URLs only (the API key travels in a header), but one line per thumbnail is noise.
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .WriteTo.File(
            AppPaths.LogFile,
            formatProvider: CultureInfo.InvariantCulture,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7)
        .CreateLogger();
}
