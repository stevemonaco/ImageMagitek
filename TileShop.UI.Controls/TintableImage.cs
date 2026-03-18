using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace TileShop.UI.Controls;

/// <summary>
/// An Image control that supports tinting via OpacityMask.
/// When <see cref="TintBrush"/> is set, renders the tint color masked by the image shape
/// instead of the original image colors.
/// </summary>
public class TintableImage : Control
{
    public static readonly StyledProperty<DrawingImage?> SourceProperty =
        AvaloniaProperty.Register<TintableImage, DrawingImage?>(nameof(Source));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<TintableImage, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<IBrush?> TintBrushProperty =
        AvaloniaProperty.Register<TintableImage, IBrush?>(nameof(TintBrush));

    public DrawingImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public IBrush? TintBrush
    {
        get => GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    static TintableImage()
    {
        AffectsRender<TintableImage>(SourceProperty, StretchProperty, TintBrushProperty);
        AffectsMeasure<TintableImage>(SourceProperty, StretchProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var source = Source;
        if (source is null)
            return default;

        var sourceSize = source.Size;

        return Stretch switch
        {
            Stretch.None => sourceSize,
            Stretch.Fill => availableSize,
            Stretch.Uniform => CalculateUniformSize(sourceSize, availableSize),
            Stretch.UniformToFill => CalculateUniformToFillSize(sourceSize, availableSize),
            _ => sourceSize
        };
    }

    public override void Render(DrawingContext context)
    {
        var source = Source;
        if (source is null)
            return;

        var destRect = CalculateDestRect(source.Size, Bounds.Size, Stretch);

        if (TintBrush is { } tint && source.Drawing is { } drawing)
        {
            var opacityMask = new DrawingBrush(drawing)
            {
                Stretch = Stretch.Uniform
            };

            using (context.PushOpacityMask(opacityMask, destRect))
            {
                context.DrawRectangle(tint, null, destRect);
            }
        }
        else
        {
            var sourceRect = new Rect(source.Size);
            context.DrawImage(source, sourceRect, destRect);
        }
    }

    private static Rect CalculateDestRect(Size sourceSize, Size boundsSize, Stretch stretch)
    {
        if (stretch == Stretch.None)
        {
            return new Rect(
                (boundsSize.Width - sourceSize.Width) / 2,
                (boundsSize.Height - sourceSize.Height) / 2,
                sourceSize.Width,
                sourceSize.Height);
        }

        if (stretch == Stretch.Uniform)
        {
            var scale = Math.Min(boundsSize.Width / sourceSize.Width, boundsSize.Height / sourceSize.Height);
            var scaledWidth = sourceSize.Width * scale;
            var scaledHeight = sourceSize.Height * scale;

            return new Rect(
                (boundsSize.Width - scaledWidth) / 2,
                (boundsSize.Height - scaledHeight) / 2,
                scaledWidth,
                scaledHeight);
        }

        return new Rect(boundsSize);
    }

    private static Size CalculateUniformSize(Size sourceSize, Size availableSize)
    {
        var widthRatio = availableSize.Width / sourceSize.Width;
        var heightRatio = availableSize.Height / sourceSize.Height;
        var scale = Math.Min(widthRatio, heightRatio);

        return new Size(sourceSize.Width * scale, sourceSize.Height * scale);
    }

    private static Size CalculateUniformToFillSize(Size sourceSize, Size availableSize)
    {
        var widthRatio = availableSize.Width / sourceSize.Width;
        var heightRatio = availableSize.Height / sourceSize.Height;
        var scale = Math.Max(widthRatio, heightRatio);

        return new Size(sourceSize.Width * scale, sourceSize.Height * scale);
    }
}
