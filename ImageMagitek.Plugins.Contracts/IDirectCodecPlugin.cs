using System;

namespace ImageMagitek.Plugins;

/// <summary>
/// A codec plugin whose pixels are colors.
/// </summary>
public interface IDirectCodecPlugin : ICodecPlugin
{
    /// <summary>
    /// Decodes <paramref name="encoded"/> into colors. <paramref name="pixels"/> is zeroed beforehand.
    /// </summary>
    void Decode(ReadOnlySpan<byte> encoded, Span<PluginColor> pixels, int width, int height);

    /// <summary>
    /// Encodes colors into the zeroed <paramref name="encoded"/> buffer. Never called when <see cref="CodecInfo.CanEncode"/> is false.
    /// </summary>
    void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height);
}
