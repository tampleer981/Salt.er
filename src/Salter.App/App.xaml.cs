using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Salter.App.Services;
using Salter.App.Views;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Startup;
using Salter.Presentation.ViewModels;

namespace Salter.App;

/// <summary>
/// The application: owns the tray icon and the flyout. Started with <c>--background</c> (at sign-in) it only shows
/// the tray icon; otherwise it also opens the flyout.
/// </summary>
/// <remarks>Public because the XAML compiler declares its part of the class public.</remarks>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The application lives as long as the process; the tray icon is disposed on exit.")]
public sealed partial class App : Application
{
    private readonly SingleInstance _instance;
    private readonly IServiceProvider _services;
    private readonly bool _startInBackground;
    private readonly ILogger<App> _logger;
    private readonly DispatcherQueue _dispatcher;
    private FlyoutWindow? _flyout;
    private ShellViewModel? _shell;
    private ShellView? _shellView;
    private TrayIconService? _tray;
    private UpdateService? _updates;
    private bool _started;

    /// <summary>Creates the application on the UI thread.</summary>
    /// <param name="instance">This process's single-instance registration.</param>
    /// <param name="services">The host's services; the host outlives the application.</param>
    /// <param name="args">The command line arguments.</param>
    internal App(SingleInstance instance, IServiceProvider services, string[] args)
    {
        _instance = instance;
        _services = services;
        _startInBackground = LaunchArguments.IsBackground(args);
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _logger = services.GetRequiredService<ILogger<App>>();

        UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnProcessUnhandledException;

        // Before any markup loads: a missing resource key or a failed binding is then named in the log. Tracing has a
        // cost, so release builds turn it on only when SALTER_XAML_TRACE=1 (for example to diagnose a user's report).
        if (IsXamlTracingEnabled())
        {
            DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
            DebugSettings.IsBindingTracingEnabled = true;
        }

        DebugSettings.XamlResourceReferenceFailed += OnXamlResourceReferenceFailed;
        DebugSettings.BindingFailed += OnBindingFailed;

        InitializeComponent();
    }

    /// <summary>Gets the process exit code: 0, or 1 if startup failed.</summary>
    public int ExitCode { get; private set; }

    /// <inheritdoc/>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            Start();
        }
        catch (Exception ex)
        {
            // Any startup failure must end the process; otherwise it keeps the single-instance key without a UI.
            LogStartupFailed(_logger, new HResultChain(ex), ex);
            ExitCode = 1;
            ExitApp();
        }
    }

    private void Start()
    {
        // First, and before any UI: this process holds the single-instance key, so applying an update that an
        // earlier session downloaded cannot end another running copy. The updater restarts into the new version.
        _updates = _services.GetRequiredService<UpdateService>();
        if (_updates.ApplyPendingAndRestart(_startInBackground))
        {
            ExitApp();
            return;
        }

        _flyout = _services.GetRequiredService<FlyoutWindow>();
        SetBackdrop(_flyout);
        // Resolved here, on the UI thread: the toast view model captures this thread's synchronization context.
        var shell = _services.GetRequiredService<ShellViewModel>();
        _shell = shell;
        var theme = new ThemeService(_flyout.RootElement);
        theme.Apply(shell.Settings.Theme);
        shell.Settings.ThemeChanged += (_, _) => theme.Apply(shell.Settings.Theme);

        var view = new ShellView(shell);
        _shellView = view;
        _flyout.SetContent(view);
        _flyout.EscapeHandler = shell.HandleEscape;
        _flyout.SearchFocusRequested += (_, _) => view.FocusSearch();
        _flyout.Opened += (_, _) => OnFlyoutOpened();

        _tray = new TrayIconService(_services.GetRequiredService<ILocalizer>());
        // Deferred so a tray menu has closed before the flyout takes the foreground.
        _tray.ToggleRequested += (_, _) => Post(ToggleFlyout);
        _tray.OpenRequested += (_, _) => Post(ShowFlyout);
        _tray.SettingsRequested += (_, _) => Post(ShowSettings);
        _tray.ExitRequested += (_, _) => Post(ExitApp);
        _tray.Show();

        LogStarted(_logger, _startInBackground);
        if (!_startInBackground)
        {
            ShowFlyout();
        }

        // Launches redirected before this point were kept by the registration and are delivered now.
        _instance.Listen(background =>
        {
            if (!background)
            {
                Post(ShowFlyout);
            }
        });
        _started = true;

        // Last, so a slow or failing update check can never delay or break startup.
        _updates.Start();
    }

    private static bool IsXamlTracingEnabled()
    {
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable("SALTER_XAML_TRACE") == "1";
#endif
    }

    private void SetBackdrop(FlyoutWindow flyout)
    {
        try
        {
            if (!flyout.TrySetAcrylicBackdrop())
            {
                LogBackdropUnsupported(_logger);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NotImplementedException)
        {
            // Cosmetic only: the glass fill still shows, so the app starts without the acrylic.
            LogBackdropFailed(_logger, ex);
        }
    }

    private void Post(DispatcherQueueHandler action) => _ = _dispatcher.TryEnqueue(action);

    private void ShowFlyout() => _flyout?.ShowAt(FlyoutPositioner.Compute(_tray?.GetIconBounds()));

    private void ShowSettings()
    {
        _shell?.ShowSettingsCommand.Execute(null);
        ShowFlyout();
    }

    private void OnFlyoutOpened()
    {
        // The feed loads the first time the flyout is shown, not at sign-in when the app starts in the background.
        if (_shell?.Feed.State == FeedState.Idle)
        {
            _shell.Feed.LoadCommand.Execute(null);
        }

        _shellView?.FocusCurrentPage();
    }

    private void ToggleFlyout() => _flyout?.Toggle(() => FlyoutPositioner.Compute(_tray?.GetIconBounds()));

    private void ExitApp()
    {
        LogExiting(_logger);
        _tray?.Dispose();
        _flyout?.CloseForExit();
        _updates?.ApplyOnExit();
        // Program disposes the host once the message loop has ended.
        Exit();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogUnhandledUiException(_logger, e.Exception);
        // Once running, async void handlers rethrow here; one failed action should not take the tray app down.
        // During startup the exception propagates, so a broken start never leaves a running process without a UI.
        e.Handled = _started;
    }

    private void OnXamlResourceReferenceFailed(DebugSettings sender, XamlResourceReferenceFailedEventArgs args) =>
        LogXamlResourceReferenceFailed(_logger, args.Message);

    private void OnBindingFailed(object sender, BindingFailedEventArgs e) => LogBindingFailed(_logger, e.Message);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnobservedTaskException(_logger, e.Exception);
        e.SetObserved();
    }

    private void OnProcessUnhandledException(object sender, System.UnhandledExceptionEventArgs e) =>
        LogFatalException(_logger, e.ExceptionObject as Exception, e.IsTerminating);

    [LoggerMessage(Level = LogLevel.Information, Message = "Salt.er started (background: {Background}).")]
    private static partial void LogStarted(ILogger logger, bool background);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Salt.er failed to start (HRESULT: {HResults}).")]
    private static partial void LogStartupFailed(ILogger logger, HResultChain hResults, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Desktop acrylic is not supported; the flyout has no backdrop.")]
    private static partial void LogBackdropUnsupported(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not set the flyout's acrylic backdrop.")]
    private static partial void LogBackdropFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "XAML resource reference failed: {Details}")]
    private static partial void LogXamlResourceReferenceFailed(ILogger logger, string details);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Binding failed: {Details}")]
    private static partial void LogBindingFailed(ILogger logger, string details);

    [LoggerMessage(Level = LogLevel.Information, Message = "Salt.er is exiting.")]
    private static partial void LogExiting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on the UI thread.")]
    private static partial void LogUnhandledUiException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception.")]
    private static partial void LogUnobservedTaskException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception (terminating: {IsTerminating}).")]
    private static partial void LogFatalException(ILogger logger, Exception? exception, bool isTerminating);

    /// <summary>
    /// The HRESULT of each exception in a chain, outermost first: WinRT failures often differ only in it. Formatted
    /// only when the log entry is written.
    /// </summary>
    private readonly struct HResultChain(Exception exception)
    {
        public override string ToString()
        {
            var text = new StringBuilder();
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (text.Length > 0)
                {
                    _ = text.Append(" <- ");
                }

                _ = text.Append(CultureInfo.InvariantCulture, $"{current.GetType().Name} 0x{current.HResult:X8}");
            }

            return text.ToString();
        }
    }
}
