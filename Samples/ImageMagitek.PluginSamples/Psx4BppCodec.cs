using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.PluginSample;

/// <summary>
/// C# implementation of the XML codec "PSX 4bpp Flow" (_codecs/PSX4bpp.xml).
/// </summary>
public sealed class Psx4BppCodec : IndexedCodec
{
    public override string Name => "PSX 4bpp Plugin";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 4;
    public override int StorageSize => Width * Height * 4;
    public override bool CanEncode => true;

    public override int DefaultWidth => 64;
    public override int DefaultHeight => 64;
    public override int WidthResizeIncrement => 2;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    public Psx4BppCodec(Palette palette) : base(palette)
    {
    }

    public Psx4BppCodec(Palette palette, int width, int height) : base(palette, width, height)
    {
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        // Each byte holds two pixels, with the left pixel in the low nibble
        int src = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x += 2, src++)
            {
                _nativeBuffer[y, x] = (byte)(encodedBuffer[src] & 0xF);
                _nativeBuffer[y, x + 1] = (byte)(encodedBuffer[src] >> 4);
            }
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int dest = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x += 2, dest++)
            {
                byte indexLow = imageBuffer[y, x];
                byte indexHigh = imageBuffer[y, x + 1];

                byte index = (byte)(indexLow | (indexHigh << 4));
                _foreignBuffer[dest] = index;
            }
        }

        return ForeignBuffer;
    }
}
