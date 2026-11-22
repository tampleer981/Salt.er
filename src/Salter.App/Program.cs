using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Salter.App.Services;
using Salter.Core.Settings;
using Velopack;

namespace Salter.App;

/// <summary>The entry point, replacing the one the XAML compiler would generate.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // First: Velopack runs its install, update and uninstall hooks here, and those end the process.
        VelopackApp.Build()
            // Not here: a second launch would apply it and so end the running instance. App applies a downloaded
            // update at startup once it holds the single-instance key, and UpdateService also applies one on exit.
            .SetAutoApplyOnStartup(false)
            // Uninstalling deletes the app folder; a sign-in entry left behind would start a missing executable, and a
            // stored API key would outlive the app.
            .OnBeforeUninstallFastCallback(_ =>
            {
                RemoveStartupEntry();
                RemoveApiKey();
            })
            .Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        // A second launch (for example from the Start menu while the app runs) only opens the running flyout.
        var instance = SingleInstance.Acquire();
        if (instance is null)
        {
            return 0;
        }

        // Disposed after the message loop ends, so everything logged during shutdown reaches the log file.
        using var host = AppHost.Build();
        App? app = null;
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            app = new App(instance, host.Services, args);
        });
        return app?.ExitCode ?? 1;
    }

    private static void RemoveStartupEntry()
    {
        var registration = new RunKeyStartupRegistration(
            RunKeyStartupRegistration.CurrentProcessPath, NullLogger<RunKeyStartupRegistration>.Instance);
        try
        {
            // Only this copy's entry; one written by a portable copy elsewhere stays.
            if (registration.IsEnabled)
            {
                registration.SetEnabled(false);
            }
        }
        catch (InvalidOperationException)
        {
            // Best effort: a registry failure must not stop the uninstall.
        }
    }

    private static void RemoveApiKey()
    {
        try
        {
            // The same store the app uses; an empty value deletes the credential (a missing one is not an error).
            new CredentialSecretStore(NullLogger<CredentialSecretStore>.Instance).Set(SecretNames.WallhavenApiKey, null);
        }
        catch (InvalidOperationException)
        {
            // Best effort: a Credential Manager failure must not stop the uninstall.
        }
    }
}
