using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.UnitTests.TestFactories;

public static class ArrangerTestFactory
{
    public static ScatteredArranger CreateIndexedArrangerFromImage(string imageFile, ColorModel colorModel,
        bool zeroIndexTransparent, ICodecFactory factory, IGraphicsCodec codec)
    {
        using var image = Image<Rgba32>.Load<Rgba32>(imageFile);

        var imagePalette = image.Metadata.DecodedImageFormat switch
        {
            PngFormat => image.Metadata.GetPngMetadata().ColorTable,
            _ => throw new ArgumentException($"{imageFile} is not a supported palettized image type")
        };

        var palette = new Palette("testPalette", new ColorFactory(), colorModel, zeroIndexTransparent, PaletteStorageSource.GlobalJson);
        var colorSources = imagePalette.Value.Span.ToArray()
            .Select(x => x.ToPixel<Rgba32>())
            .Select(x => new ProjectNativeColorSource(new ColorRgba32(x.R, x.G, x.B, x.A)));

        palette.SetColorSources(colorSources);

        var file = new MemoryDataSource("test", image.Width * image.Height);
        var elemsX = image.Width / codec.Width;
        var elemsY = image.Height / codec.Height;

        var arranger = new ScatteredArranger("testArranger", PixelColorType.Indexed, ElementLayout.Tiled, elemsX, elemsY, codec.Width, codec.Height);

        var address = new BitAddress(0);
        for (int y = 0; y < elemsY; y++)
        {
            for (int x = 0; x < elemsX; x++)
            {
                var newCodec = factory.CloneCodec(codec);
                if (newCodec is IIndexedCodec indexedCodec)
                    indexedCodec.Palette = palette;

                var element = new ArrangerElement(x, y, file, address, newCodec);
                address += codec.StorageSize;

                arranger.SetElement(element, x, y);
            }
        }

        return arranger;
    }
}
