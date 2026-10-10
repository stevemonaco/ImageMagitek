using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public interface IIndexedCodec : IGraphicsCodec<byte>
{
    /// <summary>
    /// Palette to apply to the element's pixel data
    /// </summary>
    public Palette Palette { get; set; }
}
