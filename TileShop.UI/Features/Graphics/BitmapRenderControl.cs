using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TileShop.UI.Imaging;

namespace TileShop.UI.Features.Graphics;

public class BitmapRenderControl : Control
{
    public static readonly StyledProperty<BitmapAdapter?> BitmapAdapterProperty =
        AvaloniaProperty.Register<BitmapRenderControl, BitmapAdapter?>(nameof(BitmapAdapter));

    public BitmapAdapter? BitmapAdapter
    {
        get => GetValue(BitmapAdapterProperty);
        set => SetValue(BitmapAdapterProperty, value);
    }

    static BitmapRenderControl()
    {
        AffectsMeasure<BitmapRenderControl>(BitmapAdapterProperty);
        AffectsRender<BitmapRenderControl>(BitmapAdapterProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var adapter = BitmapAdapter;
        if (adapter?.Bitmap is null)
            return default;

        return new Size(adapter.Width, adapter.Height);
    }

    public override void Render(DrawingContext context)
    {
        var adapter = BitmapAdapter;
        if (adapter?.Bitmap is null)
            return;

        var destRect = new Rect(0, 0, adapter.Width, adapter.Height);

        using (context.PushRenderOptions(new RenderOptions
        {
            BitmapInterpolationMode = BitmapInterpolationMode.None
        }))
        {
            context.DrawImage(adapter.Bitmap, destRect);
        }

        RenderOverlays(context);
    }

    protected virtual void RenderOverlays(DrawingContext context)
    {
    }
}
