using System;
using ImageMagitek;
using SkiaSharp;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

public class ArrangerRenderer
{
    private const float _handleScreenSize = 8f;

    private static readonly SKPaint _backdropPaint = new() { Color = new SKColor(0, 0, 0) };

    private readonly record struct CheckerboardKey(int CellWidth, int CellHeight, int OriginX, int OriginY, SKColor Primary, SKColor Secondary);

    private SKPaint? _checkerboardPaint;
    private CheckerboardKey _checkerboardKey;

    /// <summary>
    /// Returns a paint tiling the checkerboard at the grid's spacing and origin, rebuilt only when those settings change
    /// </summary>
    private SKPaint GetCheckerboardPaint(GridSettingsViewModel grid)
    {
        var key = new CheckerboardKey(Math.Max(1, grid.WidthSpacing), Math.Max(1, grid.HeightSpacing),
            grid.OriginX, grid.OriginY, ToSKColor(grid.PrimaryColor), ToSKColor(grid.SecondaryColor));

        if (_checkerboardPaint is not null && key == _checkerboardKey)
            return _checkerboardPaint;

        _checkerboardPaint?.Dispose();
        _checkerboardPaint = CreateCheckerboardPaint(key);
        _checkerboardKey = key;
        return _checkerboardPaint;
    }

    private static SKPaint CreateCheckerboardPaint(CheckerboardKey key)
    {
        var bitmap = new SKBitmap(key.CellWidth * 2, key.CellHeight * 2);
        using (var canvas = new SKCanvas(bitmap))
        using (var secondaryPaint = new SKPaint { Color = key.Secondary, BlendMode = SKBlendMode.Src })
        {
            canvas.Clear(key.Primary);
            canvas.DrawRect(0, 0, key.CellWidth, key.CellHeight, secondaryPaint);
            canvas.DrawRect(key.CellWidth, key.CellHeight, key.CellWidth, key.CellHeight, secondaryPaint);
        }

        var origin = SKMatrix.CreateTranslation(key.OriginX, key.OriginY);
        var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        var shader = bitmap.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, origin);
        return new SKPaint { Shader = shader };
    }

    private static SKColor ToSKColor(Avalonia.Media.Color color) => new(color.R, color.G, color.B, color.A);

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

    public ArrangerRenderer(Arranger arranger)
    {
    }

    public void Render(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        canvas.Save();

        var bitmap = state.BitmapAdapter.Bitmap;
        var rect = new SKRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        bool hasClip = state.IsDrawClipActive && state.DrawClipRect is not null;
        bool hasSel = state.Selection.HasSelection;
        bool isHiddenClip = hasClip && state.DrawClipEffect == DrawClipEffect.Hidden;
        var checkerboardPaint = GetCheckerboardPaint(state.GridSettings);

        // Clip the checkerboard backdrop when using Hidden draw clip effect
        if (isHiddenClip)
        {
            var clip = state.DrawClipRect!;
            var clipRect = new SKRect(clip.SnappedLeft, clip.SnappedTop, clip.SnappedRight, clip.SnappedBottom);
            canvas.Save();
            canvas.ClipRect(clipRect);
            canvas.DrawRect(rect, checkerboardPaint);
            canvas.Restore();
        }
        else
        {
            canvas.DrawRect(rect, checkerboardPaint);
        }

        using (var pixels = bitmap.Lock())
        {
            var imageInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
            using var image = SKImage.FromPixels(imageInfo, pixels.Address, pixels.RowBytes);

            if (hasClip || hasSel)
            {
                if (isHiddenClip)
                {
                    // Draw only the clipped region, nothing outside
                    var clip = state.DrawClipRect!;
                    var clipRect = new SKRect(clip.SnappedLeft, clip.SnappedTop, clip.SnappedRight, clip.SnappedBottom);
                    canvas.Save();
                    canvas.ClipRect(clipRect);
                    canvas.DrawImage(image, rect);
                    canvas.Restore();
                }
                else
                {
                    canvas.DrawImage(image, rect, _greyscalePaint);

                    if (hasClip)
                    {
                        var clip = state.DrawClipRect!;
                        var clipRect = new SKRect(clip.SnappedLeft, clip.SnappedTop, clip.SnappedRight, clip.SnappedBottom);
                        canvas.Save();
                        canvas.ClipRect(clipRect);
                        canvas.DrawImage(image, rect);
                        canvas.Restore();
                    }
                }

                if (hasSel)
                {
                    var sel = state.Selection.SelectionRect;
                    var selectionClip = new SKRect(sel.SnappedLeft, sel.SnappedTop, sel.SnappedRight, sel.SnappedBottom);
                    canvas.Save();
                    if (hasClip)
                    {
                        var clip = state.DrawClipRect!;
                        canvas.ClipRect(new SKRect(clip.SnappedLeft, clip.SnappedTop, clip.SnappedRight, clip.SnappedBottom));
                    }
                    canvas.ClipRect(selectionClip);
                    canvas.DrawImage(image, rect);
                    canvas.Restore();
                }
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
        RenderHoverTarget(state, canvas);

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
        var fillPaint = state.IsElementPasteActive ? _pasteFillPaint : _pixelPasteFillPaint;
        var strokePaint = state.IsElementPasteActive ? _pasteStrokePaint : _pixelPasteStrokePaint;
        canvas.DrawRect(pasteRect, fillPaint);
        canvas.DrawRect(pasteRect, strokePaint);

        canvas.Restore();
    }

    private static void RenderHoverTarget(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        if (state.HoverTarget is not { } target)
            return;

        // Dark line outside, light line inside, each one screen pixel wide so it reads over any color at any zoom
        var screenPixel = 1f / (float)Math.Max(state.Zoom, 0.01);
        _hoverOuterPaint.StrokeWidth = screenPixel;
        _hoverInnerPaint.StrokeWidth = screenPixel;

        var rect = new SKRect(target.Left, target.Top, target.Right, target.Bottom);
        rect.Inflate(screenPixel / 2f, screenPixel / 2f);
        canvas.DrawRect(rect, _hoverOuterPaint);

        rect.Inflate(-screenPixel, -screenPixel);
        canvas.DrawRect(rect, _hoverInnerPaint);
    }

    private static void RenderGridlines(GraphicsEditorViewModel state, SKCanvas canvas)
    {
        var gridSettings = state.GridSettings;
        if (!gridSettings.ShowGridlines)
            return;

        _gridlinePaint.Color = ToSKColor(gridSettings.LineColor);

        foreach (var gridline in gridSettings.Gridlines)
        {
            canvas.DrawLine(gridline.X1, gridline.Y1, gridline.X2, gridline.Y2, _gridlinePaint);
        }
    }
}
