using Microsoft.Windows.AppLifecycle;
using Salter.App.Interop;
using Salter.Presentation.Startup;
using Windows.ApplicationModel.Activation;

namespace Salter.App.Services;

/// <summary>
/// Keeps one running instance per user: a later launch hands its activation to the running instance and exits.
/// </summary>
internal sealed class SingleInstance
{
    private const string InstanceKey = "Salt.er";

    private readonly Lock _gate = new();
    private readonly List<bool> _pending = [];
    private Action<bool>? _listener;

    private SingleInstance(AppInstance instance) => instance.Activated += OnActivated;

    /// <summary>Registers this process as the running instance, or redirects to the one already running.</summary>
    /// <returns>The registration, or <see langword="null"/> if another instance runs and this process should exit.</returns>
    public static SingleInstance? Acquire()
    {
        var primary = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (primary.IsCurrent)
        {
            return new SingleInstance(primary);
        }

        Redirect(primary);
        return null;
    }

    /// <summary>
    /// Starts delivering redirected activations. Activations that arrived earlier are delivered first, on the
    /// calling thread; later ones arrive on a background thread.
    /// </summary>
    /// <param name="listener">
    /// Receives each redirected activation; the argument is <see langword="true"/> if that launch asked to start in
    /// the background.
    /// </param>
    public void Listen(Action<bool> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        bool[] pending;
        lock (_gate)
        {
            _listener = listener;
            pending = [.. _pending];
            _pending.Clear();
        }

        foreach (var background in pending)
        {
            listener(background);
        }
    }

    private static void Redirect(AppInstance primary)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();

        // This launch owns the foreground; let the running instance take it to show its flyout.
        _ = NativeMethods.AllowSetForegroundWindow(primary.ProcessId);

        // The redirect is awaited on a worker thread. Blocking waits on this STA thread keep pumping COM calls,
        // so the cross-process call cannot deadlock. If the running instance exits meanwhile the redirect fails,
        // and this launch exits either way.
        using var done = new ManualResetEvent(initialState: false);
        _ = Task.Run(async () =>
        {
            try
            {
                await primary.RedirectActivationToAsync(activation);
            }
            finally
            {
                done.Set();
            }
        });
        done.WaitOne();
    }

    private static bool IsBackground(AppActivationArguments args) =>
        args.Kind == ExtendedActivationKind.Launch
        && args.Data is ILaunchActivatedEventArgs launch
        && LaunchArguments.IsBackground(launch.Arguments ?? string.Empty);

    private void OnActivated(object? sender, AppActivationArguments args)
    {
        var background = IsBackground(args);
        Action<bool>? listener;
        lock (_gate)
        {
            listener = _listener;
            if (listener is null)
            {
                // The app has not started listening yet; keep the activation until it does.
                _pending.Add(background);
            }
        }

        listener?.Invoke(background);
    }
}
