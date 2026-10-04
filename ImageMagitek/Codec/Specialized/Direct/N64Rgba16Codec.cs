using System;
using System.Buffers.Binary;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class N64Rgba16Codec : DirectCodec
{
    public override string Name => "N64 Rgba16";
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 16;
    public override int StorageSize => Width * Height * 16;
    public override int RowStride { get; } = 0;
    public override int ElementStride { get; } = 0;

    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 32;
    public override int DefaultHeight => 32;
    public override bool CanEncode => true;

    public N64Rgba16Codec()
    {
    }

    public N64Rgba16Codec(int width, int height) : base(width, height)
    {
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        int src = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, src += 2)
            {
                ushort pair = BinaryPrimitives.ReadUInt16BigEndian(encodedBuffer[src..]);
                byte r = (byte)((pair >> 11) << 3);
                byte g = (byte)(((pair >> 6) & 0x1F) << 3);
                byte b = (byte)(((pair >> 1) & 0x1F) << 3);
                byte a = (pair & 0x1) == 1 ? (byte)0xFF : (byte)0;

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, a);
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
            for (int x = 0; x < el.Width; x++, dest += 2)
            {
                var imageColor = imageBuffer[y, x];

                ushort r = (ushort)((imageColor.R >> 3) << 11);
                ushort g = (ushort)((imageColor.G >> 3) << 6);
                ushort b = (ushort)((imageColor.B >> 3) << 1);
                ushort a = imageColor.A == 255 ? (byte)1 : (byte)0;

                ushort pair = (ushort)(r | g | b | a);
                BinaryPrimitives.WriteUInt16BigEndian(_foreignBuffer.AsSpan(dest), pair);
            }
        }

        return _foreignBuffer;
    }
}
