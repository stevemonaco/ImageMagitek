using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class N64Rgba32Codec : DirectCodec
{
    public override string Name => "N64 Rgba32";
    public override int Width { get; } = 32;
    public override int Height { get; } = 32;
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 32;
    public override int StorageSize => Width * Height * 32;
    public override int RowStride { get; } = 0;
    public override int ElementStride { get; } = 0;
    public override bool CanEncode => true;

    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 32;
    public override int DefaultHeight => 32;

    private BitStream _bitStream;

    public N64Rgba32Codec()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];

        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public N64Rgba32Codec(int width, int height)
    {
        Width = width;
        Height = height;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];

        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
        _bitStream.SeekAbsolute(0);

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++)
            {
                var g = _bitStream.ReadByte();
                var r = _bitStream.ReadByte();
                var a = _bitStream.ReadByte();
                var b = _bitStream.ReadByte();

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, a);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var bs = BitStream.OpenWrite(StorageSize, 8);

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++)
            {
                var imageColor = imageBuffer[y, x];
                bs.WriteByte(imageColor.G);
                bs.WriteByte(imageColor.R);
                bs.WriteByte(imageColor.A);
                bs.WriteByte(imageColor.B);
            }
        }

        return bs.Data;
    }
}
