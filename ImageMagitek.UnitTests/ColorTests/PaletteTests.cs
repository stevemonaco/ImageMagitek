using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using Xunit;

namespace ImageMagitek.UnitTests;

public class PaletteTests
{
    [Fact]
    public void SetNativeColor_Components_StoresForeignColorInPaletteModel()
    {
        var palette = new Palette("bgr15", new ColorFactory(), ColorModel.Bgr15, false, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(Enumerable.Repeat(new ProjectNativeColorSource(new ColorRgba32(0, 0, 0, 255)), 4));

        palette.SetNativeColor(1, 248, 0, 0, 255);

        var foreign = Assert.IsType<ColorBgr15>(palette.GetForeignColor(1));
        Assert.Equal(31, foreign.R);
        Assert.Equal(new ColorRgba32(248, 0, 0, 255), palette.GetNativeColor(1));
    }

    [Fact]
    public void DeserializePalette_InvalidColor_ThrowsNamingEntry()
    {
        var json = """{ "Name": "bad", "Colors": [ "#000000", "#GG0000" ] }""";

        var ex = Assert.Throws<InvalidDataException>(() => PaletteJsonSerializer.DeserializePalette(json, new ColorFactory()));

        Assert.Contains("color 1", ex.Message);
        Assert.Contains("#GG0000", ex.Message);
    }
}
