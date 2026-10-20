using SkiaSharp;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws a metatile map editor's composite, its gridlines, the block-bits overlay and the Blocks mode hover target
/// </summary>
public sealed class MetatileMapRenderer
{
    private static readonly SKFont _bitsFont = new() { Size = 7, Edging = SKFontEdging.Antialias };
    private static readonly SKPaint _bitsFillPaint = new() { Color = SKColors.White, IsAntialias = true };

    private static readonly SKPaint _bitsStrokePaint = new()
    {
        Color = new SKColor(0, 0, 0, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        StrokeJoin = SKStrokeJoin.Round,
        IsAntialias = true,
    };

    public void Render(MetatileMapEditorViewModel state, SKCanvas canvas, double zoom)
    {
        var bitmap = state.Bitmap.Bitmap;
        var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        using var pixmap = bitmap.PeekPixels();
        using var image = SKImage.FromPixels(pixmap);

        canvas.DrawImage(image, rect);
        ArrangerRenderer.RenderGridlines(state.GridSettings, canvas);

        if (state.ShowBlockBits)
            RenderBlockBits(state, canvas);

        ArrangerRenderer.RenderHoverTarget(state.HoverTarget, zoom, canvas);
    }

    private static void RenderBlockBits(MetatileMapEditorViewModel state, SKCanvas canvas)
    {
        var map = state.Map;
        var size = map.BlockPixelSize;
        var baseline = (size.Height - _bitsFont.Metrics.Ascent - _bitsFont.Metrics.Descent) / 2f;

        for (int by = 0; by < map.Height; by++)
        {
            for (int bx = 0; bx < map.Width; bx++)
            {
                if (map.BlockBitsValue(bx, by) is not { } bits)
                    continue;

                var text = bits.ToString();
                var x = bx * size.Width + size.Width / 2f;
                var y = by * size.Height + baseline;
                canvas.DrawText(text, x, y, SKTextAlign.Center, _bitsFont, _bitsStrokePaint);
                canvas.DrawText(text, x, y, SKTextAlign.Center, _bitsFont, _bitsFillPaint);
            }
        }
    }
}
