using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using ImageMagitek;
using ImageMagitek.Codec;

namespace TileShop.UI.Imaging;

public class IndexedBitmapAdapter
{
    public IndexedImage Image { get; }
    public WriteableBitmap Bitmap { get; }

    public IndexedBitmapAdapter(IndexedImage image)
    {
        Image = image;
        Bitmap = new WriteableBitmap(new PixelSize(Image.Width, Image.Height), new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        Render();
    }

    private void Render()
    {
        using var frameBuffer = Bitmap.Lock();

        unsafe
        {
            var backBuffer = (uint*)frameBuffer.Address.ToPointer();
            var stride = frameBuffer.RowBytes / 4;

            Parallel.For(0, Image.Height, (scanline) =>
            {
                var dest = backBuffer + scanline * stride;
                var src = Image.GetPixelRowSpan(scanline);

                for (int x = 0; x < Image.Width; x++)
                {
                    dest[x] = TranslateColor(x, scanline, src);
                }
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint TranslateColor(int x, int y, Span<byte> sourceRow)
    {
        uint outputColor = 0;

        var el = Image.GetElementAtPixel(x, y);

        if (el is { IsWithinSource: true, Codec: IIndexedCodec codec } && sourceRow[x] < codec.Palette.Entries)
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
}
