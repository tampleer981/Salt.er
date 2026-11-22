using CommunityToolkit.Mvvm.ComponentModel;

namespace Salter.Presentation.ViewModels;

/// <summary>One option of a single-choice list, such as a genre chip or a sort tab.</summary>
/// <remarks>
/// The non-generic base lets views template options without naming the value type, which XAML cannot express.
/// </remarks>
/// <param name="label">The user-visible text.</param>
public abstract partial class ChoiceItem(string label) : ObservableObject
{
    /// <summary>The user-visible text.</summary>
    public string Label { get; } = label;

    /// <summary>Whether this option is the selected one.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>One option of a single-choice list, with the value it stands for.</summary>
/// <typeparam name="T">The type of the option's value.</typeparam>
/// <param name="value">The value the option stands for.</param>
/// <param name="label">The user-visible text.</param>
public sealed class ChoiceItem<T>(T value, string label) : ChoiceItem(label)
{
    /// <summary>The value the option stands for.</summary>
    public T Value { get; } = value;
}
