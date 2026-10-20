using System;
using System.Drawing;
using SkiaSharp;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws the Blocks mode block palette and the brush's selection
/// </summary>
public sealed class BlockPaletteRenderer : IDisposable
{
    private readonly CheckerboardPaint _checkerboard = new();

    public void Render(BlockPaletteViewModel state, SKCanvas canvas, double zoom)
    {
        var palette = state.Palette;
        TilePaletteRenderer.RenderCells(canvas, state.Bitmap.Bitmap, _checkerboard.Get(state.GridSettings), state.BlockPixelSize,
            new Size(palette.Width, palette.Height), state.SelectionRect, null, zoom);
    }

    public void Dispose() => _checkerboard.Dispose();
}
