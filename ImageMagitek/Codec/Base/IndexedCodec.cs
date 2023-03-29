using System;
using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public interface IIndexedCodec : IGraphicsCodec<byte>
{
    /// <summary>
    /// Palette to apply to the element's pixel data
    /// </summary>
    public Palette Palette { get; set; }
}

public abstract class IndexedCodec : IIndexedCodec
{
    public abstract string Name { get; }
    public int Width { get; }
    public int Height { get; }

    /// <inheritdoc/>
    public Palette Palette { get; set; }
    public abstract ImageLayout Layout { get; }
    public PixelColorType ColorType => PixelColorType.Indexed;
    public abstract int ColorDepth { get; }
    public abstract int StorageSize { get; }
    public abstract bool CanEncode { get; }

    public abstract int DefaultWidth { get; }
    public abstract int DefaultHeight { get; }
    public abstract bool CanResize { get; }
    public abstract int WidthResizeIncrement { get; }
    public abstract int HeightResizeIncrement { get; }

    public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
    protected byte[] _foreignBuffer;

    public virtual byte[,] NativeBuffer => _nativeBuffer;
    protected byte[,] _nativeBuffer;

    public abstract byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);
    public abstract ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer);

    public IndexedCodec(Palette palette)
    {
        Palette = palette;
        Width = DefaultWidth;
        Height = DefaultHeight;

        AllocateBuffers(Width, Height);        
    }

    public IndexedCodec(Palette palette, int width, int height)
    {
        Palette = palette;
        Width = width;
        Height = height;

        AllocateBuffers(Width, Height);
    }

    [MemberNotNull(nameof(_nativeBuffer), nameof(_foreignBuffer))]
    protected virtual void AllocateBuffers(int width, int height)
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[height, width];
    }

    /// <summary>
    /// Reads a contiguous block of encoded pixel data
    /// </summary>
    public virtual ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
    {
        var buffer = new byte[(StorageSize + 7) / 8];
        var bitStream = BitStream.OpenRead(buffer, StorageSize);

        if (el.SourceAddress.Offset + StorageSize > el.Source.Length * 8)
            return null;

        bitStream.SeekAbsolute(0);
        el.Source.Read(el.SourceAddress, StorageSize, buffer);

        return buffer;
    }

    /// <summary>
    /// Writes a contiguous block of encoded pixel data
    /// </summary>
    public virtual void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        el.Source.Write(el.SourceAddress, StorageSize, encodedBuffer);
    }

    public virtual int GetPreferredWidth(int width)
    {
        if (!CanResize)
            return DefaultWidth;

        return Math.Clamp(width - width % WidthResizeIncrement, WidthResizeIncrement, int.MaxValue);
    }

    public virtual int GetPreferredHeight(int height)
    {
        if (!CanResize)
            return DefaultHeight;

        return Math.Clamp(height - height % HeightResizeIncrement, HeightResizeIncrement, int.MaxValue);
    }
}
