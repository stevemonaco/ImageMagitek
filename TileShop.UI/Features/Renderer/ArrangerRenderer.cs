using ImageMagitek;
using SkiaSharp;
using TileShop.UI.Features.Graphics;

namespace TileShop.UI.Renderer;

public class ArrangerRenderer
{
    public ArrangerRenderer(Arranger arranger)
    {
    }
    
    public void Render(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        // using var context = new RenderContext(state);
        
        canvas.Save();

        var bitmap = state.BitmapAdapter.Bitmap;
        var rect = new SKRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        var backdropPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0)
        };
        
        canvas.DrawRect(rect, backdropPaint);

        using (var pixels = bitmap.Lock())
        {
            var imageInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
            var image = SKImage.FromPixels(imageInfo, pixels.Address, pixels.RowBytes);
        
            canvas.DrawImage(image, rect);
        }

        
        //canvas.ClipRect(rect);

        // foreach (var node in state.Elements)
        // {
        //     DrawElement(node, canvas, context);
        // }
        //
        // if (state.ElementSelectionTool.IsActive)
        // {
        //     DrawSelectionTool(state.ElementSelectionTool, canvas, context);
        // }
        //
        // if (state.PathPenTool.IsActive)
        // {
        //     DrawPenTool(state.PathPenTool, canvas, context);
        // }
        
        canvas.Restore();
    }
}