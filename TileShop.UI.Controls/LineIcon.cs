using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TileShop.UI.Controls;

/// <summary>
/// Renders stroke-based icon geometry authored on a square design grid, scaling the geometry to the
/// control's bounds while keeping the stroke at a fixed device-independent thickness.
/// </summary>
public class LineIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<LineIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LineIcon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LineIcon, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<LineIcon, double>(nameof(GridSize), 24);

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Stroke width in device-independent pixels, independent of the rendered icon size.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Edge length of the square grid the geometry was authored on.</summary>
    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    static LineIcon()
    {
        AffectsRender<LineIcon>(DataProperty, ForegroundProperty, StrokeThicknessProperty, GridSizeProperty);
        AffectsMeasure<LineIcon>(GridSizeProperty);
    }

    protected override Size MeasureOverride(Size availableSize) => new(GridSize, GridSize);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } geometry || Foreground is not { } brush || GridSize <= 0)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / GridSize;
        if (scale <= 0)
            return;

        var offsetX = (Bounds.Width - GridSize * scale) / 2;
        var offsetY = (Bounds.Height - GridSize * scale) / 2;
        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);

        var pen = new Pen(brush, StrokeThickness / scale)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        using (context.PushTransform(transform))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
