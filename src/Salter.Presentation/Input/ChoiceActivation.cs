using Salter.Presentation.ViewModels;

namespace Salter.Presentation.Input;

/// <summary>
/// Decides when a choice control (a chip, tab or segment showing a <see cref="ChoiceItem"/>) runs its action, so a
/// mouse click, the keyboard and UI Automation all behave alike and the action runs once per activation.
/// </summary>
/// <remarks>
/// A click on an unchecked control raises Checked and then Click; a click on the checked control raises only Click;
/// UI Automation's Select and keyboard selection may raise only Checked; and the view model selecting a choice
/// checks its control through the binding. The view reports both events here and, after a Checked that ran the
/// action, calls <see cref="EndActivation"/> once the input has been handled (for example on the next dispatcher
/// turn), so a Checked without a Click cannot swallow a later click.
/// </remarks>
public sealed class ChoiceActivation
{
    private bool _ranOnChecked;

    /// <summary>Called when a choice's control becomes checked.</summary>
    /// <param name="item">The choice shown by the control.</param>
    /// <returns>
    /// <see langword="true"/> if the action should run: the choice is not selected yet, so the user checked it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public bool OnChecked(ChoiceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsSelected)
        {
            return false;
        }

        _ranOnChecked = true;
        return true;
    }

    /// <summary>Called when a choice's control is clicked.</summary>
    /// <returns>
    /// <see langword="true"/> if the action should run: the click was not already handled when it checked the control.
    /// </returns>
    public bool OnClick()
    {
        if (_ranOnChecked)
        {
            _ranOnChecked = false;
            return false;
        }

        return true;
    }

    /// <summary>Ends the activation in progress, so the next click runs the action.</summary>
    public void EndActivation() => _ranOnChecked = false;
}
