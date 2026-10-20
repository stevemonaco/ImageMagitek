using System;
using System.Drawing;
using SkiaSharp;
using TileShop.UI.Features.Graphics;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws the Tiles mode tile palette: its tiles over a checkerboard, refused cells dimmed, and the brush's selection
/// </summary>
public sealed class TilePaletteRenderer : IDisposable
{
    private static readonly SKPaint _refusedPaint = new() { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };

    private static readonly SKPaint _selectionFillPaint = new()
    {
        Color = new SKColor(0x7C, 0xFC, 0, 64),
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKPaint _selectionStrokePaint = new()
    {
        Color = new SKColor(0x7C, 0xFC, 0, 220),
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private readonly CheckerboardPaint _checkerboard = new();

    public void Render(TilePaletteViewModel state, SKCanvas canvas, double zoom)
    {
        var palette = state.Palette;
        RenderCells(canvas, state.Bitmap.Bitmap, _checkerboard.Get(state.GridSettings), palette.Arranger.ElementPixelSize,
            new Size(palette.Width, palette.Height), state.SelectionRect, (x, y) => palette.GetRefusal(x, y) is not null, zoom);
    }

    /// <summary>
    /// Draws a palette of <paramref name="cellCount"/> cells over <paramref name="checkerboard"/>, dimming refused cells and outlining the selection
    /// </summary>
    internal static void RenderCells(SKCanvas canvas, SKBitmap bitmap, SKPaint checkerboard, Size cellSize, Size cellCount,
        Rectangle? selection, Func<int, int, bool>? isRefused, double zoom)
    {
        var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        using var pixmap = bitmap.PeekPixels();
        using var image = SKImage.FromPixels(pixmap);

        canvas.DrawRect(rect, checkerboard);
        canvas.DrawImage(image, rect);

        var (cellWidth, cellHeight) = (cellSize.Width, cellSize.Height);

        if (isRefused is not null)
        {
            for (int y = 0; y < cellCount.Height; y++)
            {
                for (int x = 0; x < cellCount.Width; x++)
                {
                    if (isRefused(x, y))
                        canvas.DrawRect(x * cellWidth, y * cellHeight, cellWidth, cellHeight, _refusedPaint);
                }
            }
        }

        if (selection is { } cells)
        {
            var selectionRect = new SKRect(cells.Left * cellWidth, cells.Top * cellHeight, cells.Right * cellWidth, cells.Bottom * cellHeight);
            _selectionStrokePaint.StrokeWidth = 1f / (float)Math.Max(zoom, 0.01);
            canvas.DrawRect(selectionRect, _selectionFillPaint);
            canvas.DrawRect(selectionRect, _selectionStrokePaint);
        }
    }

    public void Dispose() => _checkerboard.Dispose();
}
