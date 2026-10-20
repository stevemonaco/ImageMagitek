using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Scenes;
using SkiaSharp;

namespace TileShop.UI.Features.Graphics;

public sealed class ArrangerSkiaBitmap : IDisposable
{
    private const int _parallelRowThreshold = 32;

    private readonly IndexedImage? _indexedImage;
    private readonly DirectImage? _directImage;
    private readonly LayerImage? _layerImage;
    private readonly bool _isIndexed;

    public SKBitmap Bitmap { get; private set; }
    public int Width { get; }
    public int Height { get; }

    public ArrangerSkiaBitmap(IndexedImage image)
    {
        _indexedImage = image;
        _isIndexed = true;
        Width = image.Width;
        Height = image.Height;

        Bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        Invalidate();
    }

    public ArrangerSkiaBitmap(DirectImage image)
    {
        _directImage = image;
        _isIndexed = false;
        Width = image.Width;
        Height = image.Height;

        Bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        Invalidate();
    }

    /// <summary>
    /// A bitmap of a composited image, which never changes; make a new bitmap to show a new composite
    /// </summary>
    public ArrangerSkiaBitmap(LayerImage image)
    {
        _layerImage = image;
        Width = image.Width;
        Height = image.Height;

        Bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        Invalidate();
    }

    public void Invalidate()
    {
        Render(0, 0, Width, Height);
    }

    public void Invalidate(int x, int y, int width, int height)
    {
        Render(x, y, width, height);
    }

    private void Render(int xStart, int yStart, int width, int height)
    {
        var backBuffer = Bitmap.GetPixels();
        var stride = Bitmap.RowBytes / 4;

        if (height < _parallelRowThreshold)
        {
            for (int scanline = yStart; scanline < yStart + height; scanline++)
                RenderRow(backBuffer, stride, scanline, xStart, width);
        }
        else
        {
            Parallel.For(yStart, yStart + height, scanline => RenderRow(backBuffer, stride, scanline, xStart, width));
        }

        Bitmap.NotifyPixelsChanged();
    }

    private unsafe void RenderRow(IntPtr backBuffer, int stride, int scanline, int xStart, int width)
    {
        var dest = (uint*)backBuffer.ToPointer() + scanline * stride;

        if (_layerImage is { } layerImage)
        {
            var src = layerImage.Pixels.AsSpan(scanline * Width, Width);
            for (int x = xStart; x < xStart + width; x++)
                dest[x] = ToBgra(src[x].Color);
        }
        else if (_isIndexed)
        {
            RenderIndexedRow(dest, scanline, xStart, width);
        }
        else
        {
            var src = _directImage!.GetPixelRowSpan(scanline);
            for (int x = xStart; x < xStart + width; x++)
                dest[x] = ToBgra(src[x].Color);
        }
    }

    private unsafe void RenderIndexedRow(uint* dest, int scanline, int xStart, int width)
    {
        var image = _indexedImage!;
        var src = image.GetPixelRowSpan(scanline);
        var elementWidth = image.Arranger.ElementPixelSize.Width;
        var end = xStart + width;
        var x = xStart;

        while (x < end)
        {
            var runEnd = Math.Min(end, x + elementWidth - (x + image.Left) % elementWidth);

            if (image.GetElementAtPixel(x, scanline) is { IsWithinSource: true, Codec: IIndexedCodec codec })
            {
                var pal = codec.Palette;
                var entries = pal.Entries;
                var zeroTransparent = pal.ZeroIndexTransparent;

                for (; x < runEnd; x++)
                {
                    var index = src[x];
                    if (index >= entries)
                    {
                        dest[x] = 0;
                        continue;
                    }

                    var color = ToBgra(pal[index].Color);
                    dest[x] = index == 0 && zeroTransparent ? color & 0x00FFFFFF : color;
                }
            }
            else
            {
                for (; x < runEnd; x++)
                    dest[x] = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToBgra(uint rgba) => (rgba & 0xFF00FF00) | BitOperations.RotateLeft(rgba & 0xFF00FF, 16);

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}
