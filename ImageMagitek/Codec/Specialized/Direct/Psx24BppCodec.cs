using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class Psx24BppCodec : DirectCodec
{
    public override string Name => "PSX 24bpp";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 24;
    public override int StorageSize => Width * Height * 24;
    public override bool CanEncode => true;

    public override int RowStride => 0;
    public override int ElementStride => 0;
    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 64;
    public override int DefaultHeight => 64;

    public Psx24BppCodec()
    {
    }

    public Psx24BppCodec(int width, int height) : base(width, height)
    {
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        int src = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, src += 3)
            {
                var r = encodedBuffer[src];
                var g = encodedBuffer[src + 1];
                var b = encodedBuffer[src + 2];

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, 0xFF);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int dest = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, dest += 3)
            {
                var imageColor = imageBuffer[y, x];
                _foreignBuffer[dest] = imageColor.R;
                _foreignBuffer[dest + 1] = imageColor.G;
                _foreignBuffer[dest + 2] = imageColor.B;
            }
        }

        return _foreignBuffer;
    }
}
