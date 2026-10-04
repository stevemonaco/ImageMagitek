using System;
using System.IO;
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
                    var color = pal[index];
                    span[x] = color.ToRgba32();
                }
            }
        }

        using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        outputImage.SaveAsPng(outputStream);
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

            return new MagitekResult<DecodedImage>.Success(new DecodedImage(pixels, width, height));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageFormatException)
        {
            return new MagitekResult<DecodedImage>.Failed($"Could not load image '{imagePath}': {ex.Message}");
        }
    }
}
