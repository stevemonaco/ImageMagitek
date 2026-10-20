using System;
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
        var bitmap = state.Bitmap.Bitmap;
        var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        using var pixmap = bitmap.PeekPixels();
        using var image = SKImage.FromPixels(pixmap);

        canvas.DrawRect(rect, _checkerboard.Get(state.GridSettings));
        canvas.DrawImage(image, rect);

        var palette = state.Palette;
        var cellWidth = palette.Arranger.ElementPixelSize.Width;
        var cellHeight = palette.Arranger.ElementPixelSize.Height;

        for (int y = 0; y < palette.Height; y++)
        {
            for (int x = 0; x < palette.Width; x++)
            {
                if (palette.GetRefusal(x, y) is not null)
                    canvas.DrawRect(x * cellWidth, y * cellHeight, cellWidth, cellHeight, _refusedPaint);
            }
        }

        if (state.SelectionRect is { } selection)
        {
            var selectionRect = new SKRect(selection.Left * cellWidth, selection.Top * cellHeight, selection.Right * cellWidth, selection.Bottom * cellHeight);
            _selectionStrokePaint.StrokeWidth = 1f / (float)Math.Max(zoom, 0.01);
            canvas.DrawRect(selectionRect, _selectionFillPaint);
            canvas.DrawRect(selectionRect, _selectionStrokePaint);
        }
    }

    public void Dispose() => _checkerboard.Dispose();
}
