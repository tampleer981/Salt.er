using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Salter.Presentation.ViewModels;

namespace Salter.App.Views;

/// <summary>
/// The detail screen of one wallpaper: preview, tags, the display to apply it to, and the apply button. The preview
/// uses the thumbnail; the original is downloaded only when the wallpaper is applied.
/// </summary>
public sealed partial class DetailView : UserControl
{
    private readonly ChoiceInput _choices = new();

    /// <summary>Creates the detail screen.</summary>
    /// <param name="detail">The detail view model shown.</param>
    public DetailView(DetailViewModel detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        Detail = detail;
        InitializeComponent();

        Detail.PropertyChanged += OnDetailPropertyChanged;
        UpdatePreview();
    }

    /// <summary>Gets the detail view model shown.</summary>
    public DetailViewModel Detail { get; }

    /// <summary>Moves keyboard focus to the back button.</summary>
    public void FocusFirst() => _ = BackButton.Focus(FocusState.Programmatic);

    private void OnDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DetailViewModel.Wallpaper))
        {
            UpdatePreview();
        }
    }

    private void UpdatePreview() =>
        Preview.Background = Detail.Wallpaper is { } wallpaper
            ? new ImageBrush { ImageSource = Ui.Thumbnail(wallpaper.ThumbnailUrl, XamlRoot), Stretch = Stretch.UniformToFill }
            : null;

    private void OnTargetChecked(object sender, RoutedEventArgs e) => _choices.Checked(sender, Detail.SelectTargetCommand);

    private void OnTargetClick(object sender, RoutedEventArgs e) => _choices.Clicked(sender, Detail.SelectTargetCommand);
}
