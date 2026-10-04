using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests.TestFactories;

public static class ArrangerTestFactory
{
    /// <summary>
    /// Creates a tiled arranger over <paramref name="source"/> (default: a zeroed memory source), with a codec created per element from its element coordinates
    /// </summary>
    public static ScatteredArranger CreateArranger(PixelColorType colorType, int elemsX, int elemsY, Func<int, int, IGraphicsCodec> createCodec, DataSource? source = null)
    {
        var prototype = createCodec(0, 0);
        var bytesPerElement = (prototype.StorageSize + 7) / 8;
        source ??= new MemoryDataSource("test", elemsX * elemsY * bytesPerElement);
        var arranger = new ScatteredArranger("testArranger", colorType, ElementLayout.Tiled, elemsX, elemsY, prototype.Width, prototype.Height);

        var address = new BitAddress(0);
        for (int y = 0; y < elemsY; y++)
        {
            for (int x = 0; x < elemsX; x++)
            {
                var codec = createCodec(x, y);
                arranger.SetElement(new ArrangerElement(x, y, source, address, codec), x, y);
                address += codec.StorageSize;
            }
        }

        return arranger;
    }

    public static Palette CreatePalette(params ColorRgba32[] colors)
    {
        var palette = new Palette("testPalette", new ColorFactory(), ColorModel.Rgba32, false, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(colors.Select(x => new ProjectNativeColorSource(x)));
        return palette;
    }
}
