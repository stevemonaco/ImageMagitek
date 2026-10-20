using System;
using SkiaSharp;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

public class ArrangerRenderer
{
    private const float _handleScreenSize = 8f;

    private static readonly SKPaint _backdropPaint = new() { Color = new SKColor(0, 0, 0) };

    private readonly CheckerboardPaint _checkerboard = new();

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

    private static readonly SKPaint _pixelPasteFillPaint = new()
    {
        Color = new SKColor(0, 180, 255, 64),
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKPaint _pixelPasteStrokePaint = new()
    {
        Color = new SKColor(0, 180, 255, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        IsAntialias = false,
    };

    // Zero width is a hairline: always one device pixel regardless of zoom
    private static readonly SKPaint _gridlinePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0,
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

    private static readonly SKPaint _hoverOuterPaint = new()
    {
        Color = new SKColor(0, 0, 0, 200),
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private static readonly SKPaint _hoverInnerPaint = new()
    {
        Color = new SKColor(255, 255, 255, 230),
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    public void Render(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        canvas.Save();

        var bitmap = state.EditorBitmap.Bitmap;
        var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        // DrawBitmap copies a mutable bitmap's pixels into a new SKImage on every call; FromPixels wraps without copying
        using var pixmap = bitmap.PeekPixels();
        using var image = SKImage.FromPixels(pixmap);

        SKRect? clipRect = state.IsDrawClipActive && state.DrawClipRect is { } clip
            ? new SKRect(clip.SnappedLeft, clip.SnappedTop, clip.SnappedRight, clip.SnappedBottom)
            : null;
        SKRect? selectionRect = state.Selection.HasSelection && state.Selection.SelectionRect is { } sel
            ? new SKRect(sel.SnappedLeft, sel.SnappedTop, sel.SnappedRight, sel.SnappedBottom)
            : null;
        var checkerboardPaint = _checkerboard.Get(state.GridSettings);

        if (clipRect is { } hiddenRect && state.DrawClipEffect == DrawClipEffect.Hidden)
        {
            canvas.Save();
            canvas.ClipRect(hiddenRect);
            canvas.DrawRect(rect, checkerboardPaint);
            canvas.DrawImage(image, rect);
            canvas.Restore();
        }
        else if ((clipRect ?? selectionRect) is { } colorRect)
        {
            // A selection inside a clip is already within the clip's color region, so one color pass covers both
            canvas.DrawRect(rect, checkerboardPaint);

            canvas.Save();
            canvas.ClipRect(colorRect, SKClipOperation.Difference);
            canvas.DrawImage(image, rect, _greyscalePaint);
            canvas.Restore();

            canvas.Save();
            canvas.ClipRect(colorRect);
            canvas.DrawImage(image, rect);
            canvas.Restore();
        }
        else
        {
            canvas.DrawRect(rect, checkerboardPaint);
            canvas.DrawImage(image, rect);
        }

        RenderGridlines(state.GridSettings, canvas);
        RenderSelection(state, canvas);
        RenderSelectionHandles(state, canvas);
        RenderPaste(state, canvas);
        RenderHoverTarget(state.HoverTarget, state.Zoom, canvas);

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

        var arranger = state.WorkingArranger;
        var clipRect = new SKRect(0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);

        canvas.Save();
        canvas.ClipRect(clipRect);

        if (paste.OverlayImage is { } overlayImage)
        {
            var bitmap = overlayImage.Bitmap;
            using var pixmap = bitmap.PeekPixels();
            using var image = SKImage.FromPixels(pixmap);

            var destRect = new SKRect(paste.Rect.SnappedLeft, paste.Rect.SnappedTop,
                paste.Rect.SnappedLeft + bitmap.Width,
                paste.Rect.SnappedTop + bitmap.Height);
            canvas.DrawImage(image, destRect);
        }

        var pasteRect = new SKRect(paste.Rect.SnappedLeft, paste.Rect.SnappedTop, paste.Rect.SnappedRight, paste.Rect.SnappedBottom);
        var fillPaint = state.IsElementPasteActive ? _pasteFillPaint : _pixelPasteFillPaint;
        var strokePaint = state.IsElementPasteActive ? _pasteStrokePaint : _pixelPasteStrokePaint;
        canvas.DrawRect(pasteRect, fillPaint);
        canvas.DrawRect(pasteRect, strokePaint);

        canvas.Restore();
    }

    internal static void RenderHoverTarget(System.Drawing.Rectangle? hoverTarget, double zoom, SKCanvas canvas)
    {
        if (hoverTarget is not { } target)
            return;

        // Dark line outside, light line inside, each one screen pixel wide so it reads over any color at any zoom
        var screenPixel = 1f / (float)Math.Max(zoom, 0.01);
        _hoverOuterPaint.StrokeWidth = screenPixel;
        _hoverInnerPaint.StrokeWidth = screenPixel;

        var rect = new SKRect(target.Left, target.Top, target.Right, target.Bottom);
        rect.Inflate(screenPixel / 2f, screenPixel / 2f);
        canvas.DrawRect(rect, _hoverOuterPaint);

        rect.Inflate(-screenPixel, -screenPixel);
        canvas.DrawRect(rect, _hoverInnerPaint);
    }

    internal static void RenderGridlines(GridSettingsViewModel gridSettings, SKCanvas canvas)
    {
        if (!gridSettings.ShowGridlines)
            return;

        _gridlinePaint.Color = gridSettings.LineColor.ToSKColor();

        foreach (var gridline in gridSettings.Gridlines)
        {
            canvas.DrawLine(gridline.X1, gridline.Y1, gridline.X2, gridline.Y2, _gridlinePaint);
        }
    }
}
