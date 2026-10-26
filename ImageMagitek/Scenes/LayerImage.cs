using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Scenes;

/// <summary>
/// A 32-bit color image, row-major, that the compositor stacks; <see cref="Pixels"/> can be passed straight to
/// <see cref="IImageFileAdapter.SaveImage(ColorRgba32[], int, int, string)"/>.
/// </summary>
public sealed class LayerImage
{
    public int Width { get; }
    public int Height { get; }
    public ColorRgba32[] Pixels { get; }

    /// <exception cref="ArgumentException">A size is negative or <paramref name="pixels"/> does not hold width × height pixels</exception>
    public LayerImage(int width, int height, ColorRgba32[] pixels)
    {
        if (width < 0 || height < 0)
            throw new ArgumentException($"A layer image must not have a negative size, not {width}x{height}");

        if (pixels.Length != (long)width * height)
            throw new ArgumentException($"A {width}x{height} layer image needs {(long)width * height} pixels, not {pixels.Length}");

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>
    /// Writes the image to a direct-color file through <paramref name="adapter"/>
    /// </summary>
    public void ExportImage(string imagePath, IImageFileAdapter adapter) => adapter.SaveImage(Pixels, Width, Height, imagePath);

    /// <summary>
    /// Renders the arranger's current data to 32-bit color: indexed pixels take their element's palette color and alpha,
    /// direct pixels keep their decoded color, and empty cells, elements outside their source and indexes past the palette are transparent.
    /// </summary>
    /// <param name="forceZeroTransparent">Makes index 0 fully transparent whatever the palette's zero-index-transparent flag says</param>
    public static LayerImage FromArranger(IArrangerView arranger, bool forceZeroTransparent = false)
    {
        var width = arranger.ArrangerPixelSize.Width;
        var height = arranger.ArrangerPixelSize.Height;

        if (width <= 0 || height <= 0)
            return new LayerImage(0, 0, []);

        if (arranger.ColorType == PixelColorType.Direct)
            return new LayerImage(width, height, new DirectImage(arranger).Image);

        var image = new IndexedImage(arranger);
        var pixels = new ColorRgba32[width * height];
        var elementWidth = arranger.ElementPixelSize.Width;

        for (int y = 0; y < height; y++)
        {
            var src = image.GetPixelRowSpan(y);
            var dest = pixels.AsSpan(y * width, width);
            var x = 0;

            while (x < width)
            {
                var runEnd = Math.Min(width, x + elementWidth - x % elementWidth);

                if (arranger.GetElementAtPixel(x, y) is { IsWithinSource: true, Codec: IIndexedCodec codec })
                {
                    var palette = codec.Palette;
                    var entries = palette.Entries;
                    var zeroTransparent = palette.ZeroIndexTransparent || forceZeroTransparent;

                    for (; x < runEnd; x++)
                    {
                        var index = src[x];
                        if (index >= entries)
                            continue;

                        dest[x] = index == 0 && zeroTransparent ? palette[index] with { A = 0 } : palette[index];
                    }
                }

                x = runEnd;
            }
        }

        return new LayerImage(width, height, pixels);
    }
}
