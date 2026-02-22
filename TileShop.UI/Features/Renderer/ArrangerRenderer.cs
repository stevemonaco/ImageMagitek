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

        // Render Arranger
        using (var pixels = bitmap.Lock())
        {
            var imageInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
            var image = SKImage.FromPixels(imageInfo, pixels.Address, pixels.RowBytes);
        
            canvas.DrawImage(image, rect);
        }
        
        // Render Overlays
        using var selectionPaint = new SKPaint
        {
            Color = new SKColor(0x7C, 0xFC, 0, 127),
            //Color = new SKColor(0xFF7CFC00),
            Style = SKPaintStyle.Fill,
        };
        
        var pastePaint = new SKPaint
        {
            //Color = new SKColor(0xFFFF00FF),
            Color = new SKColor(255, 0, 255, 127),
            Style = SKPaintStyle.Fill,
        };

        if (state.Selection.HasSelection)
        {
            var stateSelection = state.Selection.SelectionRect;
            var selectionRect = new SKRect(stateSelection.SnappedLeft, stateSelection.SnappedTop, stateSelection.SnappedRight, stateSelection.SnappedBottom);
            canvas.DrawRect(selectionRect, selectionPaint);
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