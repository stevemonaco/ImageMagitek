using System;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.Codec;

public interface IDirectCodec : IGraphicsCodec<ColorRgba32> { }

public abstract class DirectCodec : IDirectCodec
{
    public abstract string Name { get; }
    public abstract int Width { get; }
    public abstract int Height { get; }
    public abstract ImageLayout Layout { get; }
    public PixelColorType ColorType => PixelColorType.Direct;
    public abstract int ColorDepth { get; }
    public abstract int StorageSize { get; }
    public abstract int RowStride { get; }
    public abstract int ElementStride { get; }
    public abstract bool CanEncode { get; }

    public abstract bool CanResize { get; }
    public abstract int WidthResizeIncrement { get; }
    public abstract int HeightResizeIncrement { get; }
    public abstract int DefaultWidth { get; }
    public abstract int DefaultHeight { get; }

    public virtual ColorRgba32[,] NativeBuffer => _nativeBuffer;
    protected ColorRgba32[,] _nativeBuffer;

    public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
    protected byte[] _foreignBuffer;

    public abstract ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer);
    public abstract ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer);

    /// <summary>
    /// Reads a contiguous block of encoded pixel data
    /// </summary>
    public virtual ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
    {
        var buffer = new byte[(StorageSize + 7) / 8];
        var bitStream = BitStream.OpenRead(buffer, StorageSize);

        //var fs = el.DataFile.Stream;

        // TODO: Add bit granularity to seek and read
        if (el.SourceAddress.Offset + StorageSize > el.Source.Length * 8)
            return null;

        bitStream.SeekAbsolute(0);
        el.Source.Read(el.SourceAddress, StorageSize, buffer);

        //fs.ReadShifted(el.FileAddress, StorageSize, buffer);

        return buffer;
    }

    /// <summary>
    /// Writes a contiguous block of encoded pixel data
    /// </summary>
    public virtual void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        // TODO: Add bit granularity to seek and read
        //var fs = el.DataFile.Stream;
        //fs.WriteShifted(el.FileAddress, StorageSize, encodedBuffer);

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
