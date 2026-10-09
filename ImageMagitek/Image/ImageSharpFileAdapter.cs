using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek;

public sealed class ImageSharpFileAdapter : IImageFileAdapter
{
    public void SaveImage(byte[] image, Arranger arranger, string imagePath)
    {
        var width = arranger.ArrangerPixelSize.Width;
        var height = arranger.ArrangerPixelSize.Height;

        if (CombinedPalette.TryCreate(arranger) is { } combined && TryMapToCombined(image, arranger, combined) is { } combinedIndices)
        {
            IndexedPngFile.Write(imagePath, combinedIndices, width, height, combined.Colors);
            return;
        }

        Configuration.Default.PreferContiguousImageBuffers = true;
        using var outputImage = new Image<Rgba32>(width, height);

        var srcidx = 0;

        for (int y = 0; y < height; y++)
        {
            outputImage.DangerousTryGetSinglePixelMemory(out var memory);
            var span = memory.Slice(y * width, width).Span;

            for (int x = 0; x < width; x++, srcidx++)
            {
                if (arranger.GetElementAtPixel(x, y)?.Codec is IIndexedCodec codec)
                {
                    var pal = codec.Palette;
                    var index = image[srcidx];
                    if (index == 0 && pal.ZeroIndexTransparent)
                        continue;

                    span[x] = pal[index].ToRgba32();
                }
            }
        }

        using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        outputImage.SaveAsPng(outputStream);
    }

    /// <summary>
    /// Shifts each pixel's index into its palette's range of <paramref name="combined"/>, or returns null when an index
    /// lies past the end of its palette and so has no slot of its own
    /// </summary>
    private static byte[]? TryMapToCombined(byte[] image, Arranger arranger, CombinedPalette combined)
    {
        var width = arranger.ArrangerPixelSize.Width;
        var height = arranger.ArrangerPixelSize.Height;
        var result = new byte[width * height];

        for (int y = 0, i = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++, i++)
            {
                if (arranger.GetElementAtPixel(x, y)?.Codec is not IIndexedCodec { Palette: { } palette })
                    continue;

                if (!combined.TryGetSlot(palette, out var offset, out var count) || image[i] >= count)
                    return null;

                result[i] = (byte)(offset + image[i]);
            }
        }

        return result;
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
