using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Salter.Core.Downloads;
using Salter.Core.Settings;
using Salter.Core.Wallhaven;
using Salter.Presentation.Abstractions;
using Salter.Presentation.Formatting;

namespace Salter.Presentation.ViewModels;

/// <summary>
/// The detail screen of one wallpaper: its tags and uploader, the target display, and applying it as the wallpaper.
/// Also applies wallpapers straight from the feed via <see cref="ApplyQuickAsync"/>.
/// </summary>
/// <remarks>
/// Only one wallpaper is applied at a time; a request made while <see cref="IsApplying"/> is <see langword="true"/>
/// is ignored. Members must be used from the UI thread.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Each details request source is cancelled and disposed when superseded or closed; the last one lives as long as the view model.")]
public sealed partial class DetailViewModel : ObservableObject
{
    private readonly IWallhavenClient _client;
    private readonly IWallpaperDownloader _downloader;
    private readonly IWallpaperService _wallpapers;
    private readonly ISettingsStore _settings;
    private readonly IToastService _toasts;
    private readonly ILocalizer _localizer;

    private CancellationTokenSource? _details;

    // Incremented by every open and close, so an apply that finishes late can tell whether its wallpaper is still
    // the one shown.
    private int _openGeneration;

    /// <summary>Creates the detail view model. Nothing is shown until <see cref="OpenAsync"/> runs.</summary>
    /// <param name="client">Fetches the wallpaper's tags and uploader.</param>
    /// <param name="downloader">Downloads the full-resolution image.</param>
    /// <param name="wallpapers">Lists the displays and sets the wallpaper.</param>
    /// <param name="settings">Provides the download folder.</param>
    /// <param name="toasts">Reports whether applying succeeded.</param>
    /// <param name="localizer">Provides labels and toast messages.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public DetailViewModel(
        IWallhavenClient client,
        IWallpaperDownloader downloader,
        IWallpaperService wallpapers,
        ISettingsStore settings,
        IToastService toasts,
        ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(wallpapers);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(localizer);
        _client = client;
        _downloader = downloader;
        _wallpapers = wallpapers;
        _settings = settings;
        _toasts = toasts;
        _localizer = localizer;

        Tags = [];
        var all = AllDisplaysTarget();
        Targets = [all];
        SelectedTarget = all;
    }

    /// <summary>Raised when the detail screen should close: on <see cref="CloseCommand"/> or after a successful apply.</summary>
    public event EventHandler? Closed;

    /// <summary>The wallpaper shown, or <see langword="null"/> before the first <see cref="OpenAsync"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial WallpaperSummary? Wallpaper { get; private set; }

    /// <summary>
    /// The resolution and file size of <see cref="Wallpaper"/>, for example <c>3840 × 2160 · 4.1 MB</c>; empty before
    /// the first <see cref="OpenAsync"/>.
    /// </summary>
    public string Summary => Wallpaper is { } wallpaper
        ? _localizer.Format("Detail_Summary", wallpaper.Resolution, FileSizeText.Format(wallpaper.FileSize))
        : string.Empty;

    /// <summary>The wallpaper's page on wallhaven.</summary>
    [ObservableProperty]
    public partial Uri? WallhavenPage { get; private set; }

    /// <summary>The wallpaper's tags; empty until the details arrive, or when they could not be fetched.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Tags { get; private set; }

    /// <summary>The uploader's username; <see langword="null"/> until the details arrive, or when unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Attribution))]
    public partial string? Uploader { get; private set; }

    /// <summary>The credit line, for example <c>by name · via wallhaven.cc</c>; names "unknown" without an uploader.</summary>
    public string Attribution =>
        _localizer.Format("Detail_Attribution", Uploader ?? _localizer.Get("Detail_UnknownUploader"));

    /// <summary>
    /// The displays the wallpaper can be applied to: first "All displays" (value <see langword="null"/>), then one
    /// entry per monitor when more than one is attached.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<ChoiceItem<MonitorInfo?>> Targets { get; private set; }

    /// <summary>The selected entry of <see cref="Targets"/>.</summary>
    [ObservableProperty]
    public partial ChoiceItem<MonitorInfo?> SelectedTarget { get; private set; }

    /// <summary>Whether the display choice is shown, that is, whether more than one monitor is attached.</summary>
    [ObservableProperty]
    public partial bool ShowTargets { get; private set; }

    /// <summary>Whether a wallpaper is being downloaded or applied.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsApplying { get; private set; }

    /// <summary>
    /// Shows a wallpaper: resets the tags and uploader, lists the attached displays with "All displays" selected,
    /// then fetches the details. A details request still pending for a previously opened wallpaper is cancelled.
    /// </summary>
    /// <param name="wallpaper">The wallpaper to show.</param>
    /// <param name="ct">Cancels fetching the details.</param>
    /// <returns>
    /// A task that completes when the details have been shown, could not be fetched (the tags then stay empty), or
    /// were superseded by another wallpaper or by closing.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="wallpaper"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async Task OpenAsync(WallpaperSummary wallpaper, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(wallpaper);

        CancelDetails();
        _openGeneration++;
        var details = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _details = details;
        // Captured up front: the token stays usable after the source is disposed, the source's Token property does not.
        var token = details.Token;

        Wallpaper = wallpaper;
        WallhavenPage = wallpaper.PageUrl;
        Tags = [];
        Uploader = null;
        RebuildTargets();

        WallpaperDetails result;
        try
        {
            result = await _client.GetWallpaperAsync(wallpaper.Id, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Superseded by another wallpaper or closed.
            return;
        }
        catch (WallhavenException)
        {
            // The details are optional; the wallpaper can still be applied.
            return;
        }

        if (details == _details)
        {
            Tags = result.Tags;
            Uploader = result.Uploader;
        }
    }

    /// <summary>
    /// Downloads a wallpaper and applies it to all displays without opening the detail screen, then shows a toast.
    /// Does nothing while another wallpaper is being applied.
    /// </summary>
    /// <param name="wallpaper">The wallpaper to apply.</param>
    /// <returns>A task that completes when the wallpaper has been applied or the failure has been reported.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wallpaper"/> is <see langword="null"/>.</exception>
    public Task ApplyQuickAsync(WallpaperSummary wallpaper)
    {
        ArgumentNullException.ThrowIfNull(wallpaper);
        return ApplyCoreAsync(wallpaper, monitor: null);
    }

    /// <summary>
    /// Downloads the shown wallpaper and applies it to the selected display, then shows a toast. On success the
    /// detail screen closes, unless it was closed or another wallpaper was opened in the meantime; on failure it
    /// stays open.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var generation = _openGeneration;
        if (Wallpaper is { } wallpaper
            && await ApplyCoreAsync(wallpaper, SelectedTarget.Value)
            && generation == _openGeneration)
        {
            Close();
        }
    }

    private bool CanApply() => Wallpaper is not null && !IsApplying;

    /// <summary>Selects the display to apply the wallpaper to.</summary>
    /// <param name="target">An entry of <see cref="Targets"/>.</param>
    [RelayCommand]
    private void SelectTarget(ChoiceItem<MonitorInfo?>? target)
    {
        if (target is not null && Targets.Contains(target))
        {
            SelectedTarget = target;
        }
    }

    /// <summary>Closes the detail screen, cancelling a pending details request.</summary>
    [RelayCommand]
    private void Close()
    {
        CancelDetails();
        _openGeneration++;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedTargetChanged(ChoiceItem<MonitorInfo?> oldValue, ChoiceItem<MonitorInfo?> newValue)
    {
        oldValue?.IsSelected = false;
        newValue.IsSelected = true;
    }

    private async Task<bool> ApplyCoreAsync(WallpaperSummary wallpaper, MonitorInfo? monitor)
    {
        if (IsApplying)
        {
            return false;
        }

        IsApplying = true;
        try
        {
            // The downloader enforces its own deadline, and a wallpaper half-applied on cancellation would be worse
            // than one applied late, so neither step is cancellable.
            var path = await _downloader.DownloadAsync(wallpaper, _settings.Load().DownloadFolder, CancellationToken.None);
            await _wallpapers.SetAsync(path, monitor?.Id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is WallhavenException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _toasts.Show(_localizer.Get("Toast_Failed"), ToastKind.Error);
            return false;
        }
        finally
        {
            IsApplying = false;
        }

        var message = monitor is null
            ? _localizer.Format("Toast_AppliedAll")
            : _localizer.Format("Toast_Applied", monitor.DisplayName);
        _toasts.Show(message, ToastKind.Success);
        return true;
    }

    private void RebuildTargets()
    {
        var monitors = _wallpapers.GetMonitors();
        var all = AllDisplaysTarget();
        List<ChoiceItem<MonitorInfo?>> targets = [all];
        if (monitors.Count > 1)
        {
            targets.AddRange(monitors.Select(m => new ChoiceItem<MonitorInfo?>(m, m.DisplayName)));
        }

        Targets = targets;
        SelectedTarget = all;
        ShowTargets = monitors.Count > 1;
    }

    private ChoiceItem<MonitorInfo?> AllDisplaysTarget() => new(null, _localizer.Get("Detail_AllDisplays"));

    private void CancelDetails()
    {
        if (_details is { } details)
        {
            _details = null;
            details.Cancel();
            details.Dispose();
        }
    }
}
