using Microsoft.Extensions.Time.Testing;
using Salter.Presentation.Abstractions;
using Salter.Presentation.ViewModels;

namespace Salter.Presentation.Tests.ViewModels;

public class ToastViewModelTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Show_makes_the_toast_visible()
    {
        using var vm = CreateViewModel();

        vm.Show("Saved", ToastKind.Error);

        Assert.True(vm.IsVisible);
        Assert.Equal("Saved", vm.Message);
        Assert.Equal(ToastKind.Error, vm.Kind);
    }

    [Fact]
    public void Hides_after_2400ms()
    {
        using var vm = CreateViewModel();
        vm.Show("Saved", ToastKind.Success);

        _time.Advance(TimeSpan.FromMilliseconds(2399));
        Assert.True(vm.IsVisible);
        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void A_second_Show_restarts_the_timer()
    {
        using var vm = CreateViewModel();
        vm.Show("First", ToastKind.Success);
        _time.Advance(TimeSpan.FromMilliseconds(2000));

        vm.Show("Second", ToastKind.Error);
        _time.Advance(TimeSpan.FromMilliseconds(2000));
        Assert.True(vm.IsVisible);
        Assert.Equal("Second", vm.Message);
        _time.Advance(TimeSpan.FromMilliseconds(400));

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void Hiding_is_posted_to_the_context_captured_at_construction()
    {
        var context = new QueueingSynchronizationContext();
        ToastViewModel vm;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            vm = new ToastViewModel(_time);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        using (vm)
        {
            vm.Show("Saved", ToastKind.Success);
            _time.Advance(TimeSpan.FromMilliseconds(2400));
            Assert.True(vm.IsVisible);

            context.RunAll();

            Assert.False(vm.IsVisible);
        }
    }

    [Fact]
    public void A_hide_queued_before_a_new_Show_is_ignored()
    {
        var context = new QueueingSynchronizationContext();
        using var vm = CreateViewModel(context);
        vm.Show("First", ToastKind.Success);
        _time.Advance(TimeSpan.FromMilliseconds(2400));

        vm.Show("Second", ToastKind.Error);
        context.RunAll();

        Assert.True(vm.IsVisible);
        Assert.Equal("Second", vm.Message);
        _time.Advance(TimeSpan.FromMilliseconds(2400));
        context.RunAll();
        Assert.False(vm.IsVisible);
    }

    private ToastViewModel CreateViewModel(SynchronizationContext? context = null)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return new ToastViewModel(_time);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class QueueingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void RunAll()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
