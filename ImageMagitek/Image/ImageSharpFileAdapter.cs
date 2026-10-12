using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace ImageMagitek;

public sealed class ImageSharpFileAdapter : IImageFileAdapter
{
    public void SaveImage(byte[] image, Arranger arranger, string imagePath) =>
        SaveImage(image, arranger, new Rectangle(0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height), imagePath);

    /// <exception cref="InvalidOperationException">A pixel's index lies past the last entry of its element's palette</exception>
    public void SaveImage(byte[] image, Arranger arranger, Rectangle subsection, string imagePath)
    {
        var width = subsection.Width;
        var height = subsection.Height;

        if (CombinedPalette.TryCreate(arranger) is { } combined && TryMapToCombined(image, arranger, subsection, combined) is { } combinedIndices)
        {
            IndexedPngFile.Write(imagePath, combinedIndices, width, height, combined.Colors);
            return;
        }

        Configuration.Default.PreferContiguousImageBuffers = true;
        using var outputImage = new Image<Rgba32>(width, height);
        outputImage.DangerousTryGetSinglePixelMemory(out var memory);
        var pixels = memory.Span;

        foreach (var (y, start, end, palette) in EnumeratePaletteRuns(arranger, subsection))
        {
            if (palette is null)
                continue;

            var row = y * width;
            for (int x = start; x < end; x++)
            {
                var index = image[row + x];
                if (index >= palette.Entries)
                    throw IndexPastPalette(arranger, subsection, x, y, index, palette);

                if (index == 0 && palette.ZeroIndexTransparent)
                    continue;

                pixels[row + x] = palette[index].ToRgba32();
            }
        }

        using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        outputImage.SaveAsPng(outputStream);
    }

    /// <summary>
    /// Shifts each pixel's index into its palette's range of <paramref name="combined"/>, or returns null when an index
    /// lies past the end of its palette's slot
    /// </summary>
    private static byte[]? TryMapToCombined(byte[] image, Arranger arranger, Rectangle subsection, CombinedPalette combined)
    {
        var result = new byte[subsection.Width * subsection.Height];

        foreach (var (y, start, end, palette) in EnumeratePaletteRuns(arranger, subsection))
        {
            if (palette is null)
                continue;

            if (!combined.TryGetSlot(palette, out var offset, out var count))
                return null;

            var row = y * subsection.Width;
            for (int x = start; x < end; x++)
            {
                var index = image[row + x];
                if (index >= palette.Entries)
                    throw IndexPastPalette(arranger, subsection, x, y, index, palette);

                if (index >= count)
                    return null;

                result[row + x] = (byte)(offset + index);
            }
        }

        return result;
    }

    /// <summary>
    /// Splits each row of <paramref name="subsection"/> into runs of pixels within one element, in subsection coordinates
    /// </summary>
    /// <returns>Runs with a null palette for empty cells and elements without an indexed palette</returns>
    private static IEnumerable<(int Y, int Start, int End, Palette? Palette)> EnumeratePaletteRuns(Arranger arranger, Rectangle subsection)
    {
        var elementWidth = arranger.ElementPixelSize.Width;

        for (int y = 0; y < subsection.Height; y++)
        {
            for (int x = 0; x < subsection.Width;)
            {
                var end = Math.Min(subsection.Width, x + elementWidth - (x + subsection.X) % elementWidth);
                var palette = (arranger.GetElementAtPixel(x + subsection.X, y + subsection.Y)?.Codec as IIndexedCodec)?.Palette;
                yield return (y, x, end, palette);
                x = end;
            }
        }
    }

    private static InvalidOperationException IndexPastPalette(Arranger arranger, Rectangle subsection, int x, int y, byte index, Palette palette)
    {
        var location = arranger.PointToElementLocation(new Point(x + subsection.X, y + subsection.Y));
        return new InvalidOperationException($"Cannot export '{arranger.Name}': the element at ({location.X}, {location.Y}) " +
            $"uses color index {index}, but its palette '{palette.Name}' has only {palette.Entries} colors");
    }

    public void SaveImage(ColorRgba32[] image, int width, int height, string imagePath)
    {
        Configuration.Default.PreferContiguousImageBuffers = true;
        using var outputImage = new Image<Rgba32>(width, height);
        var srcidx = 0;

        for (int y = 0; y < height; y++)
        {
            outputImage.DangerousTryGetSinglePixelMemory(out var memory);
            var span = memory.Slice(y * width, width).Span;

            for (int x = 0; x < width; x++, srcidx++)
            {
                span[x] = image[srcidx].ToRgba32();
            }
        }

        using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        outputImage.SaveAsPng(outputStream);
    }

    public MagitekResult<DecodedImage> LoadImage(string imagePath)
    {
        Configuration.Default.PreferContiguousImageBuffers = true;

        try
        {
            using var inputImage = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);
            var width = inputImage.Width;
            var height = inputImage.Height;
            var pixels = new ColorRgba32[width * height];

            inputImage.DangerousTryGetSinglePixelMemory(out var memory);
            var span = memory.Span;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new ColorRgba32(span[i].PackedValue);

            var decoded = new DecodedImage(pixels, width, height);

            if (string.Equals(Path.GetExtension(imagePath), ".png", StringComparison.OrdinalIgnoreCase) &&
                IndexedPngFile.TryRead(imagePath, out var indices, out var palette))
            {
                decoded = decoded with { Indices = indices, Palette = palette };
            }

            return new MagitekResult<DecodedImage>.Success(decoded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageFormatException)
        {
            return new MagitekResult<DecodedImage>.Failed($"Could not load image '{imagePath}': {ex.Message}");
        }
    }
}
