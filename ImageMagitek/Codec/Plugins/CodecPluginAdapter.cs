using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.Codec;

/// <summary>
/// Wraps an <see cref="ICodecPlugin"/> as a host codec. The adapter owns the buffers, the data source I/O and the size rules.
/// </summary>
public abstract class CodecPluginAdapter<TPixel> : IGraphicsCodec<TPixel> where TPixel : unmanaged
{
    private readonly CodecInfo _info;
    private readonly byte[] _foreignBuffer;
    private readonly TPixel[,] _nativeBuffer;
    private bool _decodeFailureReported;

    public string Name => _info.Name;
    public int Width { get; }
    public int Height { get; }
    public ImageLayout Layout => _info.Layout == CodecLayout.Single ? ImageLayout.Single : ImageLayout.Tiled;
    public abstract PixelColorType ColorType { get; }
    public int ColorDepth => _info.ColorDepth;
    public int StorageSize { get; }
    public bool CanEncode => _info.CanEncode;

    public int DefaultWidth => _info.DefaultWidth;
    public int DefaultHeight => _info.DefaultHeight;
    public bool CanResize => WidthResizeIncrement > 0 || HeightResizeIncrement > 0;
    public int WidthResizeIncrement => _info.WidthResizeIncrement;
    public int HeightResizeIncrement => _info.HeightResizeIncrement;

    public ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;
    public TPixel[,] NativeBuffer => _nativeBuffer;

    private protected CodecPluginAdapter(ICodecPlugin plugin, int? width, int? height)
    {
        _info = plugin.Info;
        Width = GetPreferredWidth(width ?? DefaultWidth);
        Height = GetPreferredHeight(height ?? DefaultHeight);
        StorageSize = plugin.GetStorageBits(Width, Height);

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new TPixel[Height, Width];
    }

    private protected abstract void DecodePlugin(ReadOnlySpan<byte> encoded, Span<TPixel> pixels);
    private protected abstract void EncodePlugin(ReadOnlySpan<TPixel> pixels, Span<byte> encoded);

    public TPixel[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException($"{nameof(DecodeElement)}: buffer size is too small", nameof(encodedBuffer));

        Array.Clear(_nativeBuffer);

        try
        {
            DecodePlugin(encodedBuffer[.._foreignBuffer.Length], PackedBits.AsFlatSpan(_nativeBuffer));
        }
        catch (Exception ex)
        {
            Array.Clear(_nativeBuffer);

            if (!_decodeFailureReported)
            {
                _decodeFailureReported = true;
                CodecPluginEvents.RaiseDecodeFailed(Name, ex);
            }
        }

        return _nativeBuffer;
    }

    public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, TPixel[,] imageBuffer)
    {
        if (!CanEncode)
            throw new NotSupportedException($"'{Name}' is a read-only codec");

        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException($"{nameof(EncodeElement)}: image size does not match the codec size {Width}x{Height}", nameof(imageBuffer));

        Array.Clear(_foreignBuffer);

        try
        {
            EncodePlugin(PackedBits.AsFlatSpan(imageBuffer), _foreignBuffer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Plugin codec '{Name}' failed to encode: {ex.Message}", ex);
        }

        return _foreignBuffer;
    }

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

    public int GetPreferredWidth(int width) => GetPreferredSize(width, DefaultWidth, WidthResizeIncrement);

    public int GetPreferredHeight(int height) => GetPreferredSize(height, DefaultHeight, HeightResizeIncrement);

    private static int GetPreferredSize(int size, int defaultSize, int increment)
    {
        if (increment == 0)
            return defaultSize;

        return Math.Clamp(size - size % increment, increment, int.MaxValue);
    }
}
