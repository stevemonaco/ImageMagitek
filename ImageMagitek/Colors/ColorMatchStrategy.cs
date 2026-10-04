namespace ImageMagitek.Colors;

/// <summary>
/// Determines how a color is matched to a palette entry
/// </summary>
public enum ColorMatchStrategy
{
    /// <summary>Only an identical RGBA color matches</summary>
    Exact,

    /// <summary>Perceptually closest entry by CIE94 ΔE in Lab space; alpha is ignored</summary>
    Nearest,

    /// <summary>Closest entry by red-mean weighted RGB distance; alpha is ignored</summary>
    NearestRgb
}
