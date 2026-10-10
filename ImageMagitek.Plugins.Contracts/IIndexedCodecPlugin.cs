using System;

namespace ImageMagitek.Plugins;

/// <summary>
/// A codec plugin whose pixels are palette indices.
/// </summary>
public interface IIndexedCodecPlugin : ICodecPlugin
{
    /// <summary>
    /// Decodes <paramref name="encoded"/> into palette indices. <paramref name="pixels"/> is zeroed beforehand.
    /// </summary>
    void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height);

    /// <summary>
    /// Encodes palette indices into the zeroed <paramref name="encoded"/> buffer. Never called when <see cref="CodecInfo.CanEncode"/> is false.
    /// </summary>
    void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height);
}
