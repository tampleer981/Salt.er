using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Salter.App.Services;

/// <summary>
/// Keeps an installed copy of the app up to date from the GitHub releases of the repository, with no user action:
/// it checks in the background after startup and every <see cref="CheckInterval"/>, downloads a newer stable release,
/// and applies it silently the next time the app starts (restarting straight into the new version) or when it exits
/// from the tray menu, whichever comes first.
/// </summary>
/// <remarks>
/// A copy that Velopack did not install (the portable ZIP, a development build) never checks. Update failures are
/// logged and never shown: the app keeps working on the current version.
/// </remarks>
internal sealed partial class UpdateService : IDisposable
{
    private const string RepositoryUrl = "https://github.com/tampleer981/salt.er";

    /// <summary>How often a running copy looks for a newer release.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly ILogger<UpdateService> _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private UpdateManager? _manager;
    private VelopackAsset? _downloaded;
    private bool _applying;

    /// <summary>Creates the service; nothing is checked until <see cref="Start"/>.</summary>
    /// <param name="logger">Receives update progress and failures.</param>
    public UpdateService(ILogger<UpdateService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Applies an update that an earlier session downloaded, if any: starts the updater, which waits for this process
    /// to exit, applies the update silently and starts the new version with the same launch mode. Call it first thing
    /// at startup, once this process is known to be the only instance; when it returns <see langword="true"/>, exit
    /// right away without showing any UI. Never throws.
    /// </summary>
    /// <param name="background">Whether this launch started in the background; the restarted app does the same.</param>
    /// <returns><see langword="true"/> if the updater was started and the app must exit now.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Updates are best effort; a failure must never stop the app from starting.")]
    public bool ApplyPendingAndRestart(bool background)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            if (!manager.IsInstalled || manager.UpdatePendingRestart is not { } pending)
            {
                return false;
            }

            string[] restartArgs = background ? ["--background"] : [];
            manager.WaitExitThenApplyUpdates(pending, silent: true, restart: true, restartArgs);
            _applying = true;
            LogApplyingOnStart(pending.Version);
            return true;
        }
        catch (Exception ex)
        {
            LogUpdateFailed(ex);
            return false;
        }
    }

    /// <summary>Starts the background checks and downloads, if this copy was installed. Never throws.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Updates are best effort; no failure may affect the running app.")]
    public void Start()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            if (!manager.IsInstalled)
            {
                LogNotInstalled();
                return;
            }

            _manager = manager;
            // Downloaded by an earlier session that ended without applying it (sign-out, or the exit race below).
            if (manager.UpdatePendingRestart is { } pending)
            {
                Volatile.Write(ref _downloaded, pending);
                LogPending(pending.Version);
            }

            var cancellationToken = _cancellation.Token;
            _ = Task.Run(() => CheckPeriodicallyAsync(manager, cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            LogUpdateFailed(ex);
        }
    }

    /// <summary>
    /// Stops a check or download still in progress and, if an update was downloaded, starts the updater, which waits
    /// for this process to exit and then applies the update without restarting the app. Call it just before exiting.
    /// Never throws.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Updates are best effort; no failure may block exiting.")]
    public void ApplyOnExit()
    {
        _cancellation.Cancel();
        if (_applying)
        {
            // The updater was already started at launch.
            return;
        }

        // A download that completes after this read is not applied now; the next session finds it pending.
        var target = Volatile.Read(ref _downloaded);
        if (_manager is null || target is null)
        {
            return;
        }

        try
        {
            // Started at exit rather than after the download: the updater gives up if the app has not exited within
            // a minute. An update left unapplied (sign-out ends the app) is applied when the next session starts.
            _manager.WaitExitThenApplyUpdates(target, silent: true, restart: false);
            LogApplying(target.Version);
        }
        catch (Exception ex)
        {
            LogUpdateFailed(ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _cancellation.Dispose();

    private async Task CheckPeriodicallyAsync(UpdateManager manager, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            // Once an update is downloaded it waits for the next start or exit; checking again would only fetch it again.
            if (Volatile.Read(ref _downloaded) is null)
            {
                await CheckAndDownloadAsync(manager, cancellationToken).ConfigureAwait(false);
            }
        }
        while (await NextTickAsync(timer, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Updates are best effort; failures are logged, never surfaced.")]
    private async Task CheckAndDownloadAsync(UpdateManager manager, CancellationToken cancellationToken)
    {
        try
        {
            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                LogUpToDate();
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await manager.DownloadUpdatesAsync(update, progress: null, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _downloaded, update.TargetFullRelease);
            LogDownloaded(update.TargetFullRelease.Version);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is exiting.
        }
        catch (Exception ex)
        {
            LogUpdateFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update checks are off: this copy was not installed by Setup.")]
    private partial void LogNotInstalled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Salt.er is up to date.")]
    private partial void LogUpToDate();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Downloaded update {Version}; it is applied at the next start or when Salt.er exits.")]
    private partial void LogDownloaded(SemanticVersion? version);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Update {Version} was downloaded earlier; it is applied when Salt.er exits.")]
    private partial void LogPending(SemanticVersion? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying update {Version} after exit.")]
    private partial void LogApplying(SemanticVersion? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying update {Version} and restarting.")]
    private partial void LogApplyingOnStart(SemanticVersion? version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking for or applying an update failed.")]
    private partial void LogUpdateFailed(Exception exception);
}
