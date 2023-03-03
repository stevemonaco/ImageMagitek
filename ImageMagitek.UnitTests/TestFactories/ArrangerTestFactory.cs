using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests.TestFactories;

public static class ArrangerTestFactory
{
    public static ScatteredArranger CreateIndexedArrangerFromImage(string imageFile, ColorModel colorModel,
        bool zeroIndexTransparent, ICodecFactory factory, IGraphicsCodec codec)
    {
        var image = new Bitmap(imageFile);
        var imagePalette = image.Palette;

        var palette = new Palette("testPalette", new ColorFactory(), colorModel, zeroIndexTransparent, PaletteStorageSource.ProjectXml);
        var colorSources = imagePalette.Entries
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
                var element = new ArrangerElement(x, y, file, address, factory.CloneCodec(codec), palette);
                address += codec.StorageSize;

                arranger.SetElement(element, x, y);
            }
        }

        return arranger;
    }
}
