using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Salter.Presentation.Input;
using Salter.Presentation.ViewModels;

namespace Salter.App.Views;

/// <summary>
/// Routes the Checked and Click events of the choice controls in a list (chips, tabs, segments) to their action, once
/// per activation, whether the mouse, the keyboard or UI Automation activated them.
/// </summary>
/// <remarks>
/// Each control's <see cref="FrameworkElement.Tag"/> holds its <see cref="ChoiceItem"/> (<c>Tag="{x:Bind}"</c>):
/// ItemsRepeater does not set the data context of elements whose template uses x:Bind.
/// </remarks>
internal sealed class ChoiceInput
{
    private readonly ChoiceActivation _activation = new();
    private readonly DispatcherQueue _dispatcher;

    /// <summary>Creates the router for the current (UI) thread.</summary>
    public ChoiceInput() => _dispatcher = DispatcherQueue.GetForCurrentThread();

    /// <summary>Handles a control's Checked event by running a command with the choice.</summary>
    /// <param name="sender">The control.</param>
    /// <param name="command">The command; its parameter is the choice.</param>
    public void Checked(object sender, ICommand command) => Checked(sender, command.Execute);

    /// <summary>Handles a control's Click event by running a command with the choice.</summary>
    /// <param name="sender">The control.</param>
    /// <param name="command">The command; its parameter is the choice.</param>
    public void Clicked(object sender, ICommand command) => Clicked(sender, command.Execute);

    /// <summary>Handles a control's Checked event.</summary>
    /// <param name="sender">The control.</param>
    /// <param name="action">Runs the choice.</param>
    public void Checked(object sender, Action<ChoiceItem> action)
    {
        if (sender is FrameworkElement { Tag: ChoiceItem item } && _activation.OnChecked(item))
        {
            // A Click raised by the same activation follows synchronously; this runs after it.
            _ = _dispatcher.TryEnqueue(_activation.EndActivation);
            action(item);
        }
    }

    /// <summary>Handles a control's Click event.</summary>
    /// <param name="sender">The control.</param>
    /// <param name="action">Runs the choice.</param>
    public void Clicked(object sender, Action<ChoiceItem> action)
    {
        if (_activation.OnClick() && sender is FrameworkElement { Tag: ChoiceItem item })
        {
            action(item);
        }
    }
}
