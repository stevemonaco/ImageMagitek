using System;
using ImageMagitek.Codec;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// In-game, this font is a 16px tall VWF
/// In-ROM, this 1bpp font is stored rotated right at 90 degrees. Each character has one byte of metadata before it,
/// corresponding to the character's width. Each line of the character is stored in little endian and needs swapped.
/// The font location is 0x2AE31
/// This codec decodes the entirety of the font into one element so an exported image can be rotated for proper viewing.
/// This codec is view-only, adds two pixels of spacing, and does not support editing.
/// </summary>
public class MarmaladeBoyCodec : IndexedCodec
{
    public override string Name => "Marmalade Boy Font";
    public override int Width => 16;
    public override int Height => 10000;
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 1;
    public override int StorageSize => 126820;
    public override bool CanEncode => false;

    public override int DefaultWidth => 16;
    public override int DefaultHeight => 10000;
    public override int WidthResizeIncrement => 0;
    public override int HeightResizeIncrement => 0;
    public override bool CanResize => false;

    private BitStream _bitStream;

    public MarmaladeBoyCodec()
    {
        Initialize();
    }

    private void Initialize()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];
        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException($"{nameof(DecodeElement)}: buffer size is too small", nameof(encodedBuffer));

        encodedBuffer[.._foreignBuffer.Length].CopyTo(_foreignBuffer);

        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);

        try
        {
            int yPos = 0;
            while (true)
            {
                int characterWidth = _bitStream.ReadByte();

                if (characterWidth == 0)
                    break;

                for (int y = 0; y < characterWidth; y++, yPos++)
                {
                    _nativeBuffer[yPos, 8] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 9] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 10] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 11] = (byte)_bitStream.ReadBit();

                    _nativeBuffer[yPos, 12] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 13] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 14] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 15] = (byte)_bitStream.ReadBit();

                    _nativeBuffer[yPos, 0] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 1] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 2] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 3] = (byte)_bitStream.ReadBit();

                    _nativeBuffer[yPos, 4] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 5] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 6] = (byte)_bitStream.ReadBit();
                    _nativeBuffer[yPos, 7] = (byte)_bitStream.ReadBit();
                }

                yPos += 2;
            }
        }
        catch (Exception ex)
        {

        }


        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        throw new NotImplementedException($"'{Name}' is a read-only codec");
    }
}
