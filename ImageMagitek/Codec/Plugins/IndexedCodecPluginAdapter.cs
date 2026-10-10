using System;
using ImageMagitek.Colors;
using ImageMagitek.Plugins;

namespace ImageMagitek.Codec;

/// <summary>
/// Wraps an <see cref="IIndexedCodecPlugin"/> as an indexed codec that carries the host's palette.
/// </summary>
public sealed class IndexedCodecPluginAdapter : CodecPluginAdapter<byte>, IIndexedCodec
{
    private readonly IIndexedCodecPlugin _plugin;

    public override PixelColorType ColorType => PixelColorType.Indexed;

    /// <inheritdoc/>
    public Palette Palette { get; set; }

    /// <param name="width">Requested width, adjusted by the plugin's size rules; the default width when null</param>
    /// <param name="height">Requested height, adjusted by the plugin's size rules; the default height when null</param>
    public IndexedCodecPluginAdapter(IIndexedCodecPlugin plugin, Palette palette, int? width = null, int? height = null)
        : base(plugin, width, height)
    {
        _plugin = plugin;
        Palette = palette;
    }

    private protected override void DecodePlugin(ReadOnlySpan<byte> encoded, Span<byte> pixels) =>
        _plugin.Decode(encoded, pixels, Width, Height);

    private protected override void EncodePlugin(ReadOnlySpan<byte> pixels, Span<byte> encoded) =>
        _plugin.Encode(pixels, encoded, Width, Height);
}
