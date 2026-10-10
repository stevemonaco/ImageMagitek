using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ImageMagitek;
using ImageMagitek.Codec;

namespace FF5MonsterSprites.Imaging;

public static class IndexedBitmapAdapter
{
    /// <summary>
    /// Renders an <see cref="IndexedImage"/> into a new <see cref="WriteableBitmap"/> through its elements' palettes.
    /// </summary>
    public static WriteableBitmap ToBitmap(IndexedImage image)
    {
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var frameBuffer = bitmap.Lock();

        unsafe
        {
            var backBuffer = (uint*)frameBuffer.Address.ToPointer();
            var stride = frameBuffer.RowBytes / 4;

            for (int y = 0; y < image.Height; y++)
            {
                var dest = backBuffer + y * stride;
                var src = image.GetPixelRowSpan(y);

                for (int x = 0; x < image.Width; x++)
                {
                    uint outputColor = 0;

                    if (image.GetElementAtPixel(x, y) is { IsWithinSource: true, Codec: IIndexedCodec codec })
                    {
                        var pal = codec.Palette;
                        var index = src[x];

                        var inputColor = pal[index].Color;
                        outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);

                        if (index == 0 && pal.ZeroIndexTransparent)
                            outputColor &= 0x00FFFFFF;
                    }

                    dest[x] = outputColor;
                }
            }
        }

        return bitmap;
    }
}
