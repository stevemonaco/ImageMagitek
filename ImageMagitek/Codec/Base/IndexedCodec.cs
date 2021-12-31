using System;

namespace ImageMagitek.Codec;

public interface IIndexedCodec : IGraphicsCodec<byte> { }

public abstract class IndexedCodec : IIndexedCodec
{
    public abstract string Name { get; }
    public abstract int Width { get; }
    public abstract int Height { get; }
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
