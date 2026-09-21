using System;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using SkiaSharp;
using TileShop.Shared.Models;
using TileShop.UI.Features.Graphics;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Renderer;

/// <summary>
/// Draws the import dialog's canvas: the current arranger, the staged result, or a blend or diff of the two
/// </summary>
public sealed class ImportPreviewRenderer : IDisposable
{
    private const uint _unmatchedColor = 0xDC_FF_40_40;   // BGRA in memory: semi-opaque red
    private const uint _highlightColor = 0xB0_00_FF_FF;   // BGRA in memory: semi-opaque cyan

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
    private readonly SKPaint _onionSkinPaint = new();

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
        canvas.DrawRect(rect, _checkerboard.Get(state.GridSettings));

        switch (state.PreviewMode)
        {
            case ImportPreviewMode.Current:
                canvas.DrawBitmap(_current.Bitmap, rect);
                break;

            case ImportPreviewMode.Imported:
                canvas.DrawBitmap((_result ?? _current).Bitmap, rect);
                break;

            case ImportPreviewMode.OnionSkin:
                canvas.DrawBitmap(_current.Bitmap, rect);
                if (_result is not null)
                {
                    _onionSkinPaint.Color = new SKColor(255, 255, 255, (byte)Math.Round(Math.Clamp(state.OnionSkinOpacity, 0, 1) * 255));
                    canvas.DrawBitmap(_result.Bitmap, rect, _onionSkinPaint);
                }
                break;

            case ImportPreviewMode.Diff:
                canvas.DrawBitmap(_current.Bitmap, rect, _dimmedGreyscalePaint);
                if (_diffLayer is not null)
                    canvas.DrawBitmap(_diffLayer, rect);
                break;
        }

        UpdateHighlight(state.SelectedEntry?.Source);

        if (_highlightLayer is not null)
            canvas.DrawBitmap(_highlightLayer, rect);
    }

    /// <summary>
    /// Changed pixels in their imported colors and unmatched pixels in red, transparent elsewhere
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
                    dest[i] = src[i];
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

        var pixels = _preview.Source.Pixels;
        _highlightLayer = new SKBitmap(_preview.Source.Width, _preview.Source.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        unsafe
        {
            var dest = (uint*)_highlightLayer.GetPixels().ToPointer();
            for (int i = 0; i < pixels.Length; i++)
                dest[i] = pixels[i].Color == color ? _highlightColor : 0;
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
        _onionSkinPaint.Dispose();
    }
}
