using SkiaSharp;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws a metatile map editor's composite and its gridlines
/// </summary>
public sealed class MetatileMapRenderer
{
    public void Render(MetatileMapEditorViewModel state, SKCanvas canvas)
    {
        var bitmap = state.Bitmap.Bitmap;
        var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        using var pixmap = bitmap.PeekPixels();
        using var image = SKImage.FromPixels(pixmap);

        canvas.DrawImage(image, rect);
        ArrangerRenderer.RenderGridlines(state.GridSettings, canvas);
    }
}
