using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class IndexedPatternGraphicsCodec : IIndexedCodec
{
    public string Name { get; set; }
    public PatternGraphicsFormat Format { get; }
    public int StorageSize => Format.StorageSize;
    public ImageLayout Layout => Format.Layout;
    public PixelColorType ColorType => Format.ColorType;
    public int ColorDepth => Format.ColorDepth;

    /// <inheritdoc/>
    public Palette Palette { get; set; }
    public int Width => Format.Width;
    public int Height => Format.Height;

    private byte[] _foreignBuffer;
    public ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;

    private byte[,] _nativeBuffer;
    public byte[,] NativeBuffer => _nativeBuffer;

    private IBitStreamReader _bitReader;
    private List<int[,]> _planeImages;

    public int DefaultWidth => Format.DefaultWidth;
    public int DefaultHeight => Format.DefaultHeight;
    public bool CanResize => !Format.FixedSize;
    public int WidthResizeIncrement { get; }
    public int HeightResizeIncrement => 1;

    public bool CanEncode => true;

    public IndexedPatternGraphicsCodec(PatternGraphicsFormat format, Palette palette)
    {
        Format = format;
        Palette = palette;
        Name = format.Name;
        AllocateBuffers();
    }

    public byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer[.._foreignBuffer.Length].CopyTo(_foreignBuffer);
        _bitReader.SeekAbsolute(0);

        for (int i = 0; i < StorageSize; i++)
        {
            var bit = _bitReader.ReadBit();
            var coordinate = Format.Pattern.GetDecodeIndex(i);
            _planeImages[coordinate.P][coordinate.X, coordinate.Y] = bit;
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                byte color = 0;
                int xpos = Format.RowPixelPattern[x];

                for (int i = 0; i < Format.ColorDepth; i++)
                {
                    color |= (byte)(_planeImages[i][x, y] << Format.MergePlanePriority[i]);
                }
                NativeBuffer[y, xpos] = color;
            }
        }

        return NativeBuffer;
    }

    public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var bs = BitStream.OpenWrite(StorageSize, 8);

        for (short y = 0; y < Height; y++)
        {
            for (short x = 0; x < Width; x++)
            {
                int color = imageBuffer[y, x];
                for (short i = 0; i < Format.ColorDepth; i++)
                {
                    var bit = (color >> i) & 1;
                    short xpos = (short)Format.RowPixelPattern[x];
                    short plane = (short)Format.MergePlanePriority[i];
                    var index = Format.Pattern.GetEncodeIndex(new PlaneCoordinate(xpos, y, plane));
                    bs.SeekAbsolute(index);
                    bs.WriteBit(bit);
                }
            }
        }

        return bs.Data;
    }

    [MemberNotNull(nameof(_foreignBuffer), nameof(_nativeBuffer), nameof(_bitReader), nameof(_planeImages))]
    private void AllocateBuffers()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];

        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);

        _planeImages = new List<int[,]>();
        for (int i = 0; i < Format.ColorDepth; i++)
            _planeImages.Add(new int[Width, Height]);
    }

    public int GetPreferredWidth(int width) => DefaultWidth;
    public int GetPreferredHeight(int height) => DefaultHeight;

    public ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
    {
        var buffer = new byte[(StorageSize + 7) / 8];

        if (el.SourceAddress.Offset + StorageSize > el.Source.Length * 8)
            return null;

        el.Source.Read(el.SourceAddress, StorageSize, buffer);

        return buffer;
    }

    public void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        el.Source.Write(el.SourceAddress, StorageSize, encodedBuffer);
    }
}
