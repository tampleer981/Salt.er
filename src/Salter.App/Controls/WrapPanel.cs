using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Salter.App.Controls;

/// <summary>Lays out children left to right and starts a new line when the next child does not fit.</summary>
public sealed partial class WrapPanel : Panel
{
    private double _horizontalSpacing;
    private double _verticalSpacing;

    /// <summary>Gets or sets the gap between children on a line.</summary>
    public double HorizontalSpacing
    {
        get => _horizontalSpacing;
        set
        {
            _horizontalSpacing = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Gets or sets the gap between lines.</summary>
    public double VerticalSpacing
    {
        get => _verticalSpacing;
        set
        {
            _verticalSpacing = value;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var lineWidth = 0.0;
        var lineHeight = 0.0;
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (lineWidth > 0 && lineWidth + _horizontalSpacing + size.Width > availableSize.Width)
            {
                width = Math.Max(width, lineWidth);
                height += lineHeight + _verticalSpacing;
                lineWidth = 0;
                lineHeight = 0;
            }

            lineWidth += (lineWidth > 0 ? _horizontalSpacing : 0) + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return new Size(Math.Max(width, lineWidth), height + lineHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var y = 0.0;
        var lineHeight = 0.0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                x = 0;
                y += lineHeight + _verticalSpacing;
                lineHeight = 0;
            }

            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + _horizontalSpacing;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return finalSize;
    }
}
