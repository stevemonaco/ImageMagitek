using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class IndexedFlowGraphicsCodec : IIndexedCodec
{
    public string Name { get; set; }
    public FlowGraphicsFormat Format { get; private set; }
    public int StorageSize => Format.StorageSize;
    public ImageLayout Layout => Format.Layout;
    public PixelColorType ColorType => Format.ColorType;
    public int ColorDepth => Format.ColorDepth;

    /// <inheritdoc/>
    public Palette Palette { get; set; }
    public int Width => Format.Width;
    public int Height => Format.Height;
    public bool CanEncode => true;

    public ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
    private byte[] _foreignBuffer;

    private byte[,] _nativeBuffer;
    public byte[,] NativeBuffer => _nativeBuffer;

    public int DefaultWidth => Format.DefaultWidth;
    public int DefaultHeight => Format.DefaultHeight;
    public bool CanResize => !Format.FixedSize;
    public int WidthResizeIncrement { get; }
    public int HeightResizeIncrement => 1;

    // [property][x] -> column from RowPixelPattern
    private int[][] _columnOffsets;

    public IndexedFlowGraphicsCodec(FlowGraphicsFormat format, Palette palette)
    {
        Format = format;
        Palette = palette;
        Name = format.Name;
        AllocateBuffers();

        // Consider implementing resize increment with more accurate LCM approach
        // https://stackoverflow.com/questions/147515/least-common-multiple-for-3-or-more-numbers
        WidthResizeIncrement = format.ImageProperties.Max(x => x.RowPixelPattern.Count);
    }

    [MemberNotNull(nameof(_foreignBuffer), nameof(_nativeBuffer), nameof(_columnOffsets))]
    private void AllocateBuffers()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];

        _columnOffsets = new int[Format.ImageProperties.Count][];
        for (int p = 0; p < Format.ImageProperties.Count; p++)
        {
            var columns = new int[Width];
            for (int x = 0; x < Width; x++)
                columns[x] = Format.ImageProperties[p].RowPixelPattern[x];

            _columnOffsets[p] = columns;
        }
    }

    /// <inheritdoc/>
    public byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        var native = PackedBits.AsFlatSpan(_nativeBuffer);
        native.Clear();

        int bit = 0;
        int plane = 0;

        for (int p = 0; p < Format.ImageProperties.Count; p++)
        {
            var ip = Format.ImageProperties[p];
            var columns = _columnOffsets[p];
            var priorities = Format.MergePlanePriority.AsSpan(plane, ip.ColorDepth);

            if (ip.RowInterlace)
            {
                for (int y = 0; y < Height; y++)
                {
                    int row = y * Width;
                    for (int k = 0; k < priorities.Length; k++)
                    {
                        int shift = priorities[k];
                        for (int x = 0; x < Width; x++)
                            native[row + columns[x]] |= (byte)(PackedBits.ReadBit(encodedBuffer, bit++) << shift);
                    }
                }
            }
            else
            {
                for (int y = 0; y < Height; y++)
                {
                    int row = y * Width;
                    for (int x = 0; x < Width; x++)
                    {
                        int pos = row + columns[x];
                        for (int k = 0; k < priorities.Length; k++)
                            native[pos] |= (byte)(PackedBits.ReadBit(encodedBuffer, bit++) << priorities[k]);
                    }
                }
            }

            plane += ip.ColorDepth;
        }

        return _nativeBuffer;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var image = PackedBits.AsFlatSpan(imageBuffer);
        var output = _foreignBuffer.AsSpan();
        output.Clear();

        int bit = 0;
        int plane = 0;

        for (int p = 0; p < Format.ImageProperties.Count; p++)
        {
            var ip = Format.ImageProperties[p];
            var columns = _columnOffsets[p];
            var priorities = Format.MergePlanePriority.AsSpan(plane, ip.ColorDepth);

            if (ip.RowInterlace)
            {
                for (int y = 0; y < Height; y++)
                {
                    int row = y * Width;
                    for (int k = 0; k < priorities.Length; k++)
                    {
                        int shift = priorities[k];
                        for (int x = 0; x < Width; x++, bit++)
                        {
                            if (((image[row + columns[x]] >> shift) & 1) != 0)
                                PackedBits.SetBit(output, bit);
                        }
                    }
                }
            }
            else
            {
                for (int y = 0; y < Height; y++)
                {
                    int row = y * Width;
                    for (int x = 0; x < Width; x++)
                    {
                        int pixel = image[row + columns[x]];
                        for (int k = 0; k < priorities.Length; k++, bit++)
                        {
                            if (((pixel >> priorities[k]) & 1) != 0)
                                PackedBits.SetBit(output, bit);
                        }
                    }
                }
            }

            plane += ip.ColorDepth;
        }

        return _foreignBuffer;
    }

    /// <summary>
    /// Reads a contiguous block of foreign pixel data
    /// </summary>
    public ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
    {
        if (el.SourceAddress.Offset + StorageSize > el.Source.Length * 8)
            return null;

        el.Source.Read(el.SourceAddress, StorageSize, _foreignBuffer);

        return _foreignBuffer;
    }

    /// <summary>
    /// Writes a contiguous block of foreign pixel data
    /// </summary>
    public void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        el.Source.Write(el.SourceAddress, StorageSize, encodedBuffer);
    }

    public int GetPreferredWidth(int width)
    {
        if (!CanResize)
            return DefaultWidth;

        return Math.Clamp(width - width % WidthResizeIncrement, WidthResizeIncrement, int.MaxValue);
    }

    public int GetPreferredHeight(int height)
    {
        if (!CanResize)
            return DefaultHeight;

        return Math.Clamp(height - height % HeightResizeIncrement, HeightResizeIncrement, int.MaxValue);
    }
}
