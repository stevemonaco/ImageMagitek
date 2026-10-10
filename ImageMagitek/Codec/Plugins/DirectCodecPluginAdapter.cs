using System;
using System.Runtime.InteropServices;
using ImageMagitek.Colors;
using ImageMagitek.Plugins;

namespace ImageMagitek.Codec;

/// <summary>
/// Wraps an <see cref="IDirectCodecPlugin"/> as a direct color codec.
/// </summary>
public sealed class DirectCodecPluginAdapter : CodecPluginAdapter<ColorRgba32>, IDirectCodec
{
    private readonly IDirectCodecPlugin _plugin;

    public override PixelColorType ColorType => PixelColorType.Direct;

    /// <param name="width">Requested width, adjusted by the plugin's size rules; the default width when null</param>
    /// <param name="height">Requested height, adjusted by the plugin's size rules; the default height when null</param>
    public DirectCodecPluginAdapter(IDirectCodecPlugin plugin, int? width = null, int? height = null)
        : base(plugin, width, height)
    {
        _plugin = plugin;
    }

    private protected override void DecodePlugin(ReadOnlySpan<byte> encoded, Span<ColorRgba32> pixels) =>
        _plugin.Decode(encoded, MemoryMarshal.Cast<ColorRgba32, PluginColor>(pixels), Width, Height);

    private protected override void EncodePlugin(ReadOnlySpan<ColorRgba32> pixels, Span<byte> encoded) =>
        _plugin.Encode(MemoryMarshal.Cast<ColorRgba32, PluginColor>(pixels), encoded, Width, Height);
}
