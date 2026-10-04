using ImageMagitek.Colors;

namespace ImageMagitek.Image.Import;

/// <summary>
/// Controls how an image's colors are mapped onto an indexed arranger's palettes
/// </summary>
/// <param name="MapTransparentToIndexZero">Pixels with alpha at or below <paramref name="AlphaThreshold"/> become index 0 without color matching</param>
/// <param name="MaxDistance">Nearest strategies only: source colors farther than this from every entry are reported as unmatched</param>
public sealed record ImageImportOptions(
    ColorMatchStrategy MatchStrategy = ColorMatchStrategy.Exact,
    bool MapTransparentToIndexZero = false,
    byte AlphaThreshold = 0,
    double? MaxDistance = null)
{
    public static ImageImportOptions Default { get; } = new();
}
