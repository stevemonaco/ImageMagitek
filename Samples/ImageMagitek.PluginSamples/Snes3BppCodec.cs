using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.PluginSample;

/// <summary>
/// C# implementation of the XML codec "SNES 3bpp Flow" (_codecs/SNES3bpp Flow.xml).
/// </summary>
public sealed class Snes3BppCodec : IndexedCodec
{
    public override string Name => "SNES 3bpp Plugin";
    public override int StorageSize => 3 * Width * Height;
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 3;
    public override bool CanEncode => true;

    public override int DefaultWidth => 8;
    public override int DefaultHeight => 8;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    public Snes3BppCodec(Palette palette) : base(palette)
    {
    }

    public Snes3BppCodec(Palette palette, int width, int height) : base(palette, width, height)
    {
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        // Planes 1 and 2 alternate by row, then plane 3 follows as a block. Bits are numbered MSB-first.
        var offsetPlane3 = Width * Height * 2;

        for (int y = 0; y < Height; y++)
        {
            var offsetPlane1 = y * Width * 2;
            var offsetPlane2 = offsetPlane1 + Width;

            for (int x = 0; x < Width; x++)
            {
                var bp1 = SampleBits.ReadBit(encodedBuffer, offsetPlane1 + x);
                var bp2 = SampleBits.ReadBit(encodedBuffer, offsetPlane2 + x);
                var bp3 = SampleBits.ReadBit(encodedBuffer, offsetPlane3++);

                _nativeBuffer[y, x] = (byte)(bp1 | (bp2 << 1) | (bp3 << 2));
            }
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        // Bits are set with OR, so start from a cleared buffer
        Array.Clear(_foreignBuffer);
        var offsetPlane3 = Width * Height * 2;

        for (int y = 0; y < Height; y++)
        {
            var offsetPlane1 = y * Width * 2;
            var offsetPlane2 = offsetPlane1 + Width;

            for (int x = 0; x < Width; x++)
            {
                var index = imageBuffer[y, x];

                SampleBits.WriteBit(_foreignBuffer, offsetPlane1 + x, index & 1);
                SampleBits.WriteBit(_foreignBuffer, offsetPlane2 + x, (index >> 1) & 1);
                SampleBits.WriteBit(_foreignBuffer, offsetPlane3++, (index >> 2) & 1);
            }
        }

        return _foreignBuffer;
    }
}
