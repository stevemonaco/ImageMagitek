using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek;

/// <summary>
/// Pixels decoded from an image file, row-major with no padding.
/// <see cref="Indices"/> and <see cref="Palette"/> are set when the file is a paletted PNG.
/// </summary>
public sealed record DecodedImage(ColorRgba32[] Pixels, int Width, int Height, byte[]? Indices = null, IReadOnlyList<ColorRgba32>? Palette = null);
