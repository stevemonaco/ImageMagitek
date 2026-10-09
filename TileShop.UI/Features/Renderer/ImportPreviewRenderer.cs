using System;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using SkiaSharp;
using TileShop.UI.Features.Graphics;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws the import dialog's canvas: a blend from the current arranger to the staged result, or a diff of the two
/// </summary>
public sealed class ImportPreviewRenderer : IDisposable
{
    private const uint _unmatchedColor = 0xDC_FF_40_40;   // BGRA in memory: semi-opaque red
    private const uint _highlightColor = 0xB0_00_FF_FF;   // BGRA in memory: semi-opaque cyan
    private const uint _clearedColor = 0xDC_FF_40_FF;     // BGRA in memory: semi-opaque magenta
    private static readonly SKPaint _diffBackgroundPaint = new() { Color = new SKColor(0x18, 0x18, 0x1C) };

    private static readonly SKPaint _dimmedGreyscalePaint = new()
    {
        ColorFilter = SKColorFilter.CreateColorMatrix(
        [
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0,      0,      0,      0.35f, 0
        ])
    };

    private readonly CheckerboardPaint _checkerboard = new();
    private readonly SKPaint _onionCurrentPaint = new();
    private readonly SKPaint _onionResultPaint = new() { BlendMode = SKBlendMode.Plus };

    private ArrangerSkiaBitmap? _current;
    private ArrangerSkiaBitmap? _result;
    private SKBitmap? _diffLayer;
    private SKBitmap? _highlightLayer;
    private ImageImportPreview? _preview;
    private uint? _highlightedSource;

    /// <summary>
    /// Rebuilds the layers for a new staged import; with no preview only the arranger's current contents are shown
    /// </summary>
    public void SetPreview(Arranger arranger, ImageImportPreview? preview)
    {
        DisposeLayers();
        _preview = preview;
        _highlightedSource = null;

        if (preview is null)
        {
            _current = arranger.ColorType == PixelColorType.Indexed
                ? new ArrangerSkiaBitmap(new IndexedImage(arranger))
                : new ArrangerSkiaBitmap(new DirectImage(arranger));
            return;
        }

        if (preview.CurrentIndexed is { } currentIndexed)
        {
            _current = new ArrangerSkiaBitmap(currentIndexed);
            _result = new ArrangerSkiaBitmap(preview.ResultIndexed!);
        }
        else
        {
            _current = new ArrangerSkiaBitmap(preview.CurrentDirect!);
            _result = new ArrangerSkiaBitmap(preview.ResultDirect!);
        }

        _diffLayer = BuildDiffLayer(preview.Report, _result.Bitmap);
    }

    public void Render(ImportImageViewModel state, SKCanvas canvas)
    {
        if (_current is null)
            return;

        var rect = new SKRect(0, 0, _current.Width, _current.Height);

        if (state.ShowDiff)
        {
            canvas.DrawRect(rect, _diffBackgroundPaint);
            canvas.DrawBitmap(_current.Bitmap, rect, _dimmedGreyscalePaint);
            if (_diffLayer is not null)
                canvas.DrawBitmap(_diffLayer, rect);
        }
        else
        {
            canvas.DrawRect(rect, _checkerboard.Get(state.GridSettings));
            DrawBlend(canvas, rect, state.EffectiveBlend);
        }

        UpdateHighlight(state.SelectedEntry?.Source);

        if (_highlightLayer is not null)
            canvas.DrawBitmap(_highlightLayer, rect);
    }

    private void DrawBlend(SKCanvas canvas, SKRect rect, double blend)
    {
        var resultAlpha = (byte)Math.Round(Math.Clamp(blend, 0, 1) * 255);

        if (_result is null || resultAlpha == 0)
        {
            canvas.DrawBitmap(_current!.Bitmap, rect);
            return;
        }

        if (resultAlpha == 255)
        {
            canvas.DrawBitmap(_result.Bitmap, rect);
            return;
        }

        // Summing the weighted images in an isolated layer crossfades alpha too, so pixels becoming transparent fade out
        _onionCurrentPaint.Color = new SKColor(255, 255, 255, (byte)(255 - resultAlpha));
        _onionResultPaint.Color = new SKColor(255, 255, 255, resultAlpha);

        canvas.SaveLayer(rect, null);
        canvas.DrawBitmap(_current!.Bitmap, rect, _onionCurrentPaint);
        canvas.DrawBitmap(_result.Bitmap, rect, _onionResultPaint);
        canvas.Restore();
    }

    /// <summary>
    /// Changed pixels in their imported colors, pixels becoming transparent in magenta, and unmatched pixels in red, transparent elsewhere
    /// </summary>
    private static SKBitmap BuildDiffLayer(ImportReport report, SKBitmap result)
    {
        var layer = new SKBitmap(report.Width, report.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var states = report.PixelStates;

        unsafe
        {
            var src = (uint*)result.GetPixels().ToPointer();
            var dest = (uint*)layer.GetPixels().ToPointer();

            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].HasFlag(ImportPixelState.Changed))
                    dest[i] = (src[i] >> 24) == 0 ? _clearedColor : src[i] | 0xFF_00_00_00;
                else if (states[i].HasFlag(ImportPixelState.Unmatched))
                    dest[i] = _unmatchedColor;
                else
                    dest[i] = 0;
            }
        }

        return layer;
    }

    private void UpdateHighlight(ColorRgba32? source)
    {
        var key = source?.Color;
        if (key == _highlightedSource)
            return;

        _highlightedSource = key;
        _highlightLayer?.Dispose();
        _highlightLayer = null;

        if (key is not { } color || _preview is null)
            return;

        var image = _preview.Source;
        var report = _preview.Report;
        var offset = _preview.Offset;
        var bounds = _preview.Bounds;
        _highlightLayer = new SKBitmap(report.Width, report.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        _highlightLayer.Erase(SKColors.Transparent);

        unsafe
        {
            var dest = (uint*)_highlightLayer.GetPixels().ToPointer();
            for (int sy = 0; sy < image.Height; sy++)
            {
                for (int sx = 0; sx < image.Width; sx++)
                {
                    int x = sx + offset.X, y = sy + offset.Y;
                    if (bounds.Contains(x, y) && image.Pixels[sy * image.Width + sx].Color == color)
                        dest[y * report.Width + x] = _highlightColor;
                }
            }
        }
    }

    private void DisposeLayers()
    {
        _current?.Dispose();
        _result?.Dispose();
        _diffLayer?.Dispose();
        _highlightLayer?.Dispose();
        _current = null;
        _result = null;
        _diffLayer = null;
        _highlightLayer = null;
    }

    public void Dispose()
    {
        DisposeLayers();
        _checkerboard.Dispose();
        _onionCurrentPaint.Dispose();
        _onionResultPaint.Dispose();
    }
}
