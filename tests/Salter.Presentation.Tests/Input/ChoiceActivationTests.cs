using Salter.Presentation.Input;
using Salter.Presentation.ViewModels;

namespace Salter.Presentation.Tests.Input;

public class ChoiceActivationTests
{
    [Fact]
    public void Click_on_an_unselected_choice_runs_once_on_checked()
    {
        var activation = new ChoiceActivation();
        var item = new ChoiceItem<int>(1, "One");

        // A click first checks the control, then raises Click.
        Assert.True(activation.OnChecked(item));
        Assert.False(activation.OnClick());
    }

    [Fact]
    public void Click_on_the_selected_choice_runs_on_click()
    {
        var activation = new ChoiceActivation();

        // The control is already checked, so only Click is raised.
        Assert.True(activation.OnClick());
    }

    [Fact]
    public void Checked_from_the_view_model_does_not_run()
    {
        var activation = new ChoiceActivation();
        var item = new ChoiceItem<int>(1, "One") { IsSelected = true };

        // The binding checks the control of the choice the view model has just selected.
        Assert.False(activation.OnChecked(item));
        Assert.True(activation.OnClick());
    }

    [Fact]
    public void Checked_without_click_does_not_swallow_the_next_click()
    {
        var activation = new ChoiceActivation();
        var item = new ChoiceItem<int>(1, "One");

        // UI Automation or keyboard selection checks the control without a Click; the view ends the activation.
        Assert.True(activation.OnChecked(item));
        activation.EndActivation();

        Assert.True(activation.OnClick());
    }

    [Fact]
    public void OnChecked_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ChoiceActivation().OnChecked(null!));
    }
}
