using CommunityToolkit.Mvvm.ComponentModel;
using Salter.Presentation.Abstractions;

namespace Salter.Presentation.ViewModels;

/// <summary>The transient message shown at the bottom of the flyout; hides itself 2400 ms after each toast.</summary>
/// <remarks>
/// Construct on the UI thread: the <see cref="SynchronizationContext"/> current at construction receives the
/// auto-hide, since timers fire on the thread pool. Without one the toast is hidden on the timer's thread.
/// <see cref="Show"/> must be called from the UI thread.
/// </remarks>
public sealed partial class ToastViewModel : ObservableObject, IToastService, IDisposable
{
    /// <summary>How long a toast stays visible.</summary>
    public static readonly TimeSpan DisplayDuration = TimeSpan.FromMilliseconds(2400);

    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _context;

    private ITimer? _timer;
    private int _generation;
    private bool _disposed;

    /// <summary>Creates the toast view model, capturing the current <see cref="SynchronizationContext"/>.</summary>
    /// <param name="time">Drives the auto-hide timer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is <see langword="null"/>.</exception>
    public ToastViewModel(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _context = SynchronizationContext.Current;
    }

    /// <summary>The text of the current or last toast.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>The severity of the current or last toast.</summary>
    [ObservableProperty]
    public partial ToastKind Kind { get; private set; }

    /// <summary>Whether a toast is showing.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <inheritdoc/>
    /// <remarks>Replaces a toast that is still showing and restarts the 2400 ms countdown.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The view model has been disposed.</exception>
    public void Show(string message, ToastKind kind)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Message = message;
        Kind = kind;
        IsVisible = true;

        _timer?.Dispose();
        var generation = ++_generation;
        _timer = _time.CreateTimer(_ => OnElapsed(generation), null, DisplayDuration, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Stops the auto-hide timer.</summary>
    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }

    private void OnElapsed(int generation)
    {
        if (_context is null)
        {
            Hide(generation);
        }
        else
        {
            _context.Post(_ => Hide(generation), null);
        }
    }

    private void Hide(int generation)
    {
        // A newer toast restarted the countdown after this timer fired; it hides itself.
        if (generation == _generation && !_disposed)
        {
            IsVisible = false;
        }
    }
}
