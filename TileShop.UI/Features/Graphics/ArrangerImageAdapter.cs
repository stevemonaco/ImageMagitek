using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using TileShop.Shared.Tools;
using TileShop.UI.Imaging;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// Adapter that provides a unified interface for both IndexedImage and DirectImage
/// </summary>
public sealed class ArrangerImageAdapter
{
    private IndexedImage? _indexedImage;
    private DirectImage? _directImage;

    public Arranger Arranger { get; private set; }
    public PixelColorType ColorType => Arranger.ColorType;
    public bool IsIndexed => ColorType == PixelColorType.Indexed;
    public bool IsDirect => ColorType == PixelColorType.Direct;

    public IndexedImage? IndexedImage => _indexedImage;
    public DirectImage? DirectImage => _directImage;

    public int Width => IsIndexed ? _indexedImage!.Width : _directImage!.Width;
    public int Height => IsIndexed ? _indexedImage!.Height : _directImage!.Height;
    public int Left => IsIndexed ? _indexedImage!.Left : _directImage!.Left;
    public int Top => IsIndexed ? _indexedImage!.Top : _directImage!.Top;

    public ArrangerImageAdapter(Arranger arranger)
    {
        Arranger = arranger;
        CreateImage(0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    // public ArrangerImageAdapter(Arranger arranger, int x, int y, int width, int height)
    // {
    //     Arranger = arranger;
    //     CreateImage(x, y, width, height);
    // }

    private void CreateImage(int x, int y, int width, int height)
    {
        _indexedImage = null;
        _directImage = null;

        if (Arranger.ColorType == PixelColorType.Indexed)
        {
            _indexedImage = new IndexedImage(Arranger, x, y, width, height);
        }
        else if (Arranger.ColorType == PixelColorType.Direct)
        {
            _directImage = new DirectImage(Arranger, x, y, width, height);
        }
    }

    public void Reinitialize(Arranger arranger)
    {
        Arranger = arranger;
        CreateImage(0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    public void Reinitialize(Arranger arranger, int x, int y, int width, int height)
    {
        Arranger = arranger;
        CreateImage(x, y, width, height);
    }

    public BitmapAdapter CreateBitmapAdapter()
    {
        if (IsIndexed)
            return new IndexedBitmapAdapter(_indexedImage!);
        else
            return new DirectBitmapAdapter(_directImage!);
    }

    public ArrangerSkiaBitmap CreateSkiaBitmap()
    {
        if (IsIndexed)
            return new ArrangerSkiaBitmap(_indexedImage!);
        else
            return new ArrangerSkiaBitmap(_directImage!);
    }

    public void Render()
    {
        if (IsIndexed)
            _indexedImage!.Render();
        else
            _directImage!.Render();
    }

    public void SaveImage()
    {
        if (IsIndexed)
            _indexedImage!.SaveImage();
        else
            _directImage!.SaveImage();
    }

    public byte GetIndexedPixel(int x, int y)
    {
        if (!IsIndexed)
            throw new InvalidOperationException("Cannot get indexed pixel from direct image");
        return _indexedImage!.GetPixel(x, y);
    }

    public ColorRgba32 GetDirectPixel(int x, int y)
    {
        if (!IsDirect)
            throw new InvalidOperationException("Cannot get direct pixel from indexed image");
        return _directImage!.GetPixel(x, y);
    }

    public void SetIndexedPixel(int x, int y, byte colorIndex)
    {
        if (!IsIndexed)
            throw new InvalidOperationException("Cannot set indexed pixel on direct image");
        _indexedImage!.SetPixel(x, y, colorIndex);
    }

    public void SetDirectPixel(int x, int y, ColorRgba32 color)
    {
        if (!IsDirect)
            throw new InvalidOperationException("Cannot set direct pixel on indexed image");
        _directImage!.SetPixel(x, y, color);
    }

    public MagitekResult TrySetPixel(int x, int y, ColorRgba32 color)
    {
        if (IsIndexed)
            return _indexedImage!.TrySetPixel(x, y, color);
        else
        {
            _directImage!.SetPixel(x, y, color);
            return MagitekResult.SuccessResult;
        }
    }

    public bool FloodFill(int x, int y, byte fillIndex)
    {
        if (!IsIndexed)
            throw new InvalidOperationException("Cannot flood fill with index on direct image");
        return _indexedImage!.FloodFill(x, y, fillIndex);
    }

    public bool FloodFill(int x, int y, byte fillIndex, Rectangle? clipBounds)
    {
        if (!IsIndexed)
            throw new InvalidOperationException("Cannot flood fill with index on direct image");
        return _indexedImage!.FloodFill(x, y, fillIndex, clipBounds);
    }

    public bool FloodFill(int x, int y, ColorRgba32 fillColor)
    {
        if (!IsDirect)
            throw new InvalidOperationException("Cannot flood fill with color on indexed image");
        return _directImage!.FloodFill(x, y, fillColor);
    }

    public bool FloodFill(int x, int y, ColorRgba32 fillColor, Rectangle? clipBounds)
    {
        if (!IsDirect)
            throw new InvalidOperationException("Cannot flood fill with color on indexed image");
        return _directImage!.FloodFill(x, y, fillColor, clipBounds);
    }

    public MagitekResult TrySetPalette(int pixelX, int pixelY, Palette palette)
    {
        if (!IsIndexed)
            return new MagitekResult.Failed("Cannot set palette on direct image");
        return _indexedImage!.TrySetPalette(pixelX, pixelY, palette);
    }

    public void RemapColors(IList<byte> remap)
    {
        if (!IsIndexed)
            throw new InvalidOperationException("Cannot remap colors on direct image");
        _indexedImage!.RemapColors(remap);
    }

    public ArrangerElement? GetElementAtPixel(int x, int y)
    {
        if (IsIndexed)
            return _indexedImage!.GetElementAtPixel(x, y);
        else
            return _directImage!.GetElementAtPixel(x, y);
    }
}
