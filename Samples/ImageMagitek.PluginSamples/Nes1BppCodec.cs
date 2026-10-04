using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.PluginSample;

/// <summary>
/// C# implementation of the XML codec "NES 1bpp" (_codecs/NES1bpp.xml).
/// </summary>
public sealed class Nes1BppCodec : IndexedCodec
{
    public override string Name => "NES 1bpp Plugin";
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 1;
    public override int StorageSize => 1 * Width * Height;
    public override bool CanEncode => true;

    public override int DefaultWidth => 8;
    public override int DefaultHeight => 8;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    public Nes1BppCodec(Palette palette) : base(palette)
    {
    }

    public Nes1BppCodec(Palette palette, int width, int height) : base(palette, width, height)
    {
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        // One bit per pixel in row-major order, MSB-first
        int bitIndex = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++, bitIndex++)
                _nativeBuffer[y, x] = (byte)SampleBits.ReadBit(encodedBuffer, bitIndex);
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        // Bits are set with OR, so start from a cleared buffer
        Array.Clear(_foreignBuffer);

        int bitIndex = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++, bitIndex++)
                SampleBits.WriteBit(_foreignBuffer, bitIndex, imageBuffer[y, x] & 1);
        }

        return _foreignBuffer;
    }
}
