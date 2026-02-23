using System;
using ImageMagitek;
using SkiaSharp;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

public class ArrangerRenderer
{
    private const float _handleScreenSize = 8f;

    private static readonly SKPaint _backdropPaint = new() { Color = new SKColor(0, 0, 0) };

    private static readonly SKPaint _greyscalePaint = new()
    {
        ColorFilter = SKColorFilter.CreateColorMatrix(
        [
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0,      0,      0,      1, 0
        ])
    };

    private static readonly SKPaint _selectionFillPaint = new()
    {
        Color = new SKColor(0x7C, 0xFC, 0, 64),
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKPaint _selectionStrokePaint = new()
    {
        Color = new SKColor(0x7C, 0xFC, 0, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        IsAntialias = false,
    };

    private static readonly SKPaint _pasteFillPaint = new()
    {
        Color = new SKColor(255, 0, 255, 64),
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKPaint _pasteStrokePaint = new()
    {
        Color = new SKColor(255, 0, 255, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        IsAntialias = false,
    };

    private static readonly SKPaint _gridlinePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.40f,
        IsAntialias = false,
    };

    private static readonly SKPaint _handleFillPaint = new()
    {
        Color = SKColors.White,
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKPaint _handleStrokePaint = new()
    {
        Color = new SKColor(0x7C, 0xFC, 0, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        IsAntialias = false,
    };

    public ArrangerRenderer(Arranger arranger)
    {
    }

    public void Render(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        canvas.Save();

        var bitmap = state.BitmapAdapter.Bitmap;
        var rect = new SKRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        canvas.DrawRect(rect, _backdropPaint);

        using (var pixels = bitmap.Lock())
        {
            var imageInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
            using var image = SKImage.FromPixels(imageInfo, pixels.Address, pixels.RowBytes);

            if (state.Selection.HasSelection)
            {
                canvas.DrawImage(image, rect, _greyscalePaint);

                var sel = state.Selection.SelectionRect;
                var selectionClip = new SKRect(sel.SnappedLeft, sel.SnappedTop, sel.SnappedRight, sel.SnappedBottom);

                canvas.Save();
                canvas.ClipRect(selectionClip);
                canvas.DrawImage(image, rect);
                canvas.Restore();
            }
            else
            {
                canvas.DrawImage(image, rect);
            }
        }

        RenderGridlines(state, canvas);
        RenderSelection(state, canvas);
        RenderSelectionHandles(state, canvas);
        RenderPaste(state, canvas);

        canvas.Restore();
    }

    private static void RenderSelection(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        if (!state.Selection.HasSelection)
            return;

        var sel = state.Selection.SelectionRect;
        var selectionRect = new SKRect(sel.SnappedLeft, sel.SnappedTop, sel.SnappedRight, sel.SnappedBottom);

        canvas.DrawRect(selectionRect, _selectionFillPaint);
        canvas.DrawRect(selectionRect, _selectionStrokePaint);
    }

    private static void RenderSelectionHandles(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        if (!state.Selection.HasSelection || state.IsViewMode)
            return;

        var sel = state.Selection.SelectionRect;
        var zoom = Math.Max(state.Zoom, 0.01);
        var handleSize = _handleScreenSize / (float)zoom;
        var halfHandle = handleSize / 2f;

        float left = sel.SnappedLeft;
        float right = sel.SnappedRight;
        float top = sel.SnappedTop;
        float bottom = sel.SnappedBottom;
        float midX = (left + right) / 2f;
        float midY = (top + bottom) / 2f;

        DrawHandle(canvas, left, top, halfHandle);
        DrawHandle(canvas, midX, top, halfHandle);
        DrawHandle(canvas, right, top, halfHandle);
        DrawHandle(canvas, right, midY, halfHandle);
        DrawHandle(canvas, right, bottom, halfHandle);
        DrawHandle(canvas, midX, bottom, halfHandle);
        DrawHandle(canvas, left, bottom, halfHandle);
        DrawHandle(canvas, left, midY, halfHandle);
    }

    private static void DrawHandle(SKCanvas canvas, float cx, float cy, float halfHandle)
    {
        var handleRect = new SKRect(cx - halfHandle, cy - halfHandle, cx + halfHandle, cy + halfHandle);
        canvas.DrawRect(handleRect, _handleFillPaint);
        canvas.DrawRect(handleRect, _handleStrokePaint);
    }

    private static void RenderPaste(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        if (state.Paste is not { } paste)
            return;

        if (paste.OverlayImage is { } overlayImage)
        {
            var pasteBitmap = overlayImage.Bitmap;
            using var pixels = pasteBitmap.Lock();
            var imageInfo = new SKImageInfo(pasteBitmap.PixelSize.Width, pasteBitmap.PixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
            using var image = SKImage.FromPixels(imageInfo, pixels.Address, pixels.RowBytes);

            var destRect = new SKRect(paste.Rect.SnappedLeft, paste.Rect.SnappedTop,
                paste.Rect.SnappedLeft + pasteBitmap.PixelSize.Width,
                paste.Rect.SnappedTop + pasteBitmap.PixelSize.Height);
            canvas.DrawImage(image, destRect);
        }

        var pasteRect = new SKRect(paste.Rect.SnappedLeft, paste.Rect.SnappedTop, paste.Rect.SnappedRight, paste.Rect.SnappedBottom);
        canvas.DrawRect(pasteRect, _pasteFillPaint);
        canvas.DrawRect(pasteRect, _pasteStrokePaint);
    }

    private static void RenderGridlines(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        var gridSettings = state.GridSettings;
        if (!gridSettings.ShowGridlines)
            return;

        var lineColor = gridSettings.LineColor;
        _gridlinePaint.Color = new SKColor(lineColor.R, lineColor.G, lineColor.B, lineColor.A);

        foreach (var gridline in gridSettings.Gridlines)
        {
            canvas.DrawLine(gridline.X1, gridline.Y1, gridline.X2, gridline.Y2, _gridlinePaint);
        }
    }
}
