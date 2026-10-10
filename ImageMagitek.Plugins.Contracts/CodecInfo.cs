namespace ImageMagitek.Plugins;

/// <summary>
/// Describes a plugin codec to the host. The host validates it when the plugin is registered.
/// </summary>
public sealed class CodecInfo
{
    /// <summary>
    /// Unique name that projects use to reference the codec.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the host shows elements as tiles in a grid or as a single image.
    /// </summary>
    public required CodecLayout Layout { get; init; }

    /// <summary>
    /// Bits per pixel: 1–8 for indexed codecs, 1–32 for direct codecs.
    /// </summary>
    public required int ColorDepth { get; init; }

    /// <summary>
    /// Element width in pixels when no other size is requested.
    /// </summary>
    public required int DefaultWidth { get; init; }

    /// <summary>
    /// Element height in pixels when no other size is requested.
    /// </summary>
    public required int DefaultHeight { get; init; }

    /// <summary>
    /// Step a requested width is rounded down to, or 0 when the width is always <see cref="DefaultWidth"/>.
    /// </summary>
    public int WidthResizeIncrement { get; init; }

    /// <summary>
    /// Step a requested height is rounded down to, or 0 when the height is always <see cref="DefaultHeight"/>.
    /// </summary>
    public int HeightResizeIncrement { get; init; }

    /// <summary>
    /// Whether the codec can encode. When false, the host never calls Encode and treats the codec as read-only.
    /// </summary>
    public bool CanEncode { get; init; }
}
