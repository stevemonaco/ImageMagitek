using System;
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

    // Indexed by encoded bit: flat native pixel index and color-bit shift
    private int[] _bitPixel;
    private byte[] _bitShift;

    public int DefaultWidth => Format.DefaultWidth;
    public int DefaultHeight => Format.DefaultHeight;
    public bool CanResize => !Format.FixedSize;
    public int WidthResizeIncrement => 1;
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

        var native = PackedBits.AsFlatSpan(_nativeBuffer);
        native.Clear();

        for (int b = 0; b < _bitPixel.Length; b++)
            native[_bitPixel[b]] |= (byte)(PackedBits.ReadBit(encodedBuffer, b) << _bitShift[b]);

        return _nativeBuffer;
    }

    public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var image = PackedBits.AsFlatSpan(imageBuffer);
        var output = _foreignBuffer.AsSpan();
        output.Clear();

        for (int b = 0; b < _bitPixel.Length; b++)
        {
            if (((image[_bitPixel[b]] >> _bitShift[b]) & 1) != 0)
                PackedBits.SetBit(output, b);
        }

        return _foreignBuffer;
    }

    [MemberNotNull(nameof(_foreignBuffer), nameof(_nativeBuffer), nameof(_bitPixel), nameof(_bitShift))]
    private void AllocateBuffers()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];

        _bitPixel = new int[StorageSize];
        _bitShift = new byte[StorageSize];

        for (int b = 0; b < StorageSize; b++)
        {
            var coordinate = Format.Pattern.GetDecodeIndex(b);
            _bitPixel[b] = coordinate.Y * Width + Format.RowPixelPattern[coordinate.X];
            _bitShift[b] = (byte)Format.MergePlanePriority[coordinate.P];
        }
    }

    public int GetPreferredWidth(int width) => DefaultWidth;
    public int GetPreferredHeight(int height) => DefaultHeight;

    public ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
    {
        if (el.SourceAddress.Offset + StorageSize > el.Source.Length * 8)
            return null;

        el.Source.Read(el.SourceAddress, StorageSize, _foreignBuffer);

        return _foreignBuffer;
    }

    public void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        el.Source.Write(el.SourceAddress, StorageSize, encodedBuffer);
    }
}
