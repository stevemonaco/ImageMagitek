using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using SkiaSharp;

namespace TileShop.UI.Features.Graphics;

public sealed class ArrangerSkiaBitmap : IDisposable
{
    private readonly IndexedImage? _indexedImage;
    private readonly DirectImage? _directImage;
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
        var pixels = Bitmap.GetPixels();

        unsafe
        {
            var backBuffer = (uint*)pixels.ToPointer();
            var stride = Bitmap.RowBytes / 4;

            if (_isIndexed)
            {
                Parallel.For(yStart, yStart + height, (scanline) =>
                {
                    var dest = backBuffer + scanline * stride + xStart;
                    var src = _indexedImage!.GetPixelRowSpan(scanline);

                    for (int x = 0; x < width; x++)
                    {
                        dest[x] = TranslateIndexedColor(x + xStart, scanline, src);
                    }
                });
            }
            else
            {
                Parallel.For(yStart, yStart + height, (scanline) =>
                {
                    var dest = backBuffer + scanline * stride + xStart;
                    var src = _directImage!.GetPixelRowSpan(scanline);

                    for (int x = 0; x < width; x++)
                    {
                        dest[x] = TranslateDirectColor(x + xStart, src);
                    }
                });
            }
        }

        Bitmap.NotifyPixelsChanged();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint TranslateIndexedColor(int x, int y, Span<byte> sourceRow)
    {
        uint outputColor = 0;

        var el = _indexedImage!.GetElementAtPixel(x, y);

        if (el?.Codec is IIndexedCodec codec)
        {
            var pal = codec.Palette;
            var index = sourceRow[x];

            var inputColor = pal[index].Color;
            outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);

            if (index == 0 && pal.ZeroIndexTransparent)
                outputColor &= 0x00FFFFFF;
        }

        return outputColor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint TranslateDirectColor(int x, in Span<ColorRgba32> sourceRow)
    {
        var inputColor = sourceRow[x].Color;
        uint outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);

        return outputColor;
    }

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}
