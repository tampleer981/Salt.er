using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Salter.App.Services;

/// <summary>
/// Keeps an installed copy of the app up to date from the GitHub releases of the repository: checks once in the
/// background after startup, downloads a newer stable release, and applies it silently once the app exits.
/// </summary>
/// <remarks>
/// A copy that Velopack did not install (the portable ZIP, a development build) never checks. Update failures are
/// logged and never shown: the app keeps working on the current version.
/// </remarks>
internal sealed partial class UpdateService : IDisposable
{
    private const string RepositoryUrl = "https://github.com/tampleer981/salt.er";

    private readonly ILogger<UpdateService> _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private UpdateManager? _manager;
    private VelopackAsset? _downloaded;

    /// <summary>Creates the service; nothing is checked until <see cref="Start"/>.</summary>
    /// <param name="logger">Receives update progress and failures.</param>
    public UpdateService(ILogger<UpdateService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Starts the background check and download, if this copy was installed. Never throws.</summary>
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
            _ = Task.Run(() => CheckAndDownloadAsync(manager, cancellationToken), cancellationToken);
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
        // A download that completes after this read is not applied now; the next session finds it pending.
        var target = Volatile.Read(ref _downloaded);
        if (_manager is null || target is null)
        {
            return;
        }

        try
        {
            // Started at exit rather than after the download: the updater gives up if the app has not exited within
            // a minute. An update left unapplied (sign-out ends the app) is applied when the next session exits.
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
        Message = "Downloaded update {Version}; it is applied once Salt.er is exited from the tray menu.")]
    private partial void LogDownloaded(SemanticVersion? version);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Update {Version} was downloaded earlier; it is applied once Salt.er is exited from the tray menu.")]
    private partial void LogPending(SemanticVersion? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying update {Version} after exit.")]
    private partial void LogApplying(SemanticVersion? version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking for or applying an update failed.")]
    private partial void LogUpdateFailed(Exception exception);
}
