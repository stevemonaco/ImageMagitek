using ImageMagitek.Colors;

namespace ImageMagitek;

/// <summary>
/// Pixels decoded from an image file, row-major with no padding
/// </summary>
public sealed record DecodedImage(ColorRgba32[] Pixels, int Width, int Height);
