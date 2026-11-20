using System.Windows;
using System.Windows.Controls;

namespace TrainerDeck.UI.Controls;

public sealed class CardGridPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(CardGridPanel), new FrameworkPropertyMetadata(170.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxItemWidthProperty = DependencyProperty.Register(
        nameof(MaxItemWidth), typeof(double), typeof(CardGridPanel), new FrameworkPropertyMetadata(230.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(CardGridPanel), new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(
        nameof(AspectRatio), typeof(double), typeof(CardGridPanel), new FrameworkPropertyMetadata(1.4, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private double _itemWidth;
    private double _itemHeight;

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double MaxItemWidth
    {
        get => (double)GetValue(MaxItemWidthProperty);
        set => SetValue(MaxItemWidthProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double AspectRatio
    {
        get => (double)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public int Columns { get; private set; } = 1;

    public double RowPitch => _itemHeight + Spacing;

    public event EventHandler? GridMetricsChanged;

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? 1200 : available.Width;
        int cols = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
        _itemWidth = Math.Min(MaxItemWidth, (width - (Spacing * (cols - 1))) / cols);
        _itemHeight = Math.Round(_itemWidth * AspectRatio);

        var itemSize = new Size(_itemWidth, _itemHeight);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(itemSize);
        }

        if (cols != Columns)
        {
            Columns = cols;
            Dispatcher.BeginInvoke(() => GridMetricsChanged?.Invoke(this, EventArgs.Empty));
        }

        int rows = (InternalChildren.Count + cols - 1) / cols;
        double height = rows == 0 ? 0 : (rows * _itemHeight) + ((rows - 1) * Spacing);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int cols = Columns;
        double blockWidth = (cols * _itemWidth) + ((cols - 1) * Spacing);
        double offsetX = Math.Max(0, (finalSize.Width - blockWidth) / 2);

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int row = i / cols, col = i % cols;
            InternalChildren[i].Arrange(new Rect(
                offsetX + (col * (_itemWidth + Spacing)),
                row * (_itemHeight + Spacing),
                _itemWidth,
                _itemHeight));
        }

        return finalSize;
    }
}
