using System;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ColorTests;

public class NesColorConversionTests
{
    public static Palette LoadMasterPalette() =>
        new PaletteService(new ColorFactory()).ReadJsonPalette(Path.Combine(AppContext.BaseDirectory, "_palettes", "DefaultNes.json"))!;

    public static ColorFactory CreateNesFactory(Palette master)
    {
        var factory = new ColorFactory();
        factory.SetNesPalette(master);
        return factory;
    }

    [Fact]
    public void ToForeign_Native_ReturnsNearestMasterIndex()
    {
        var master = LoadMasterPalette();
        var factory = CreateNesFactory(master);

        for (int i = 0; i < 64; i++)
        {
            var native = master.GetNativeColor(i);
            var firstWithColor = Enumerable.Range(0, 64).First(x => master.GetNativeColor(x).Color == native.Color);

            var foreign = Assert.IsType<ColorNes>(factory.ToForeign(native, ColorModel.Nes));

            Assert.Equal((uint)firstWithColor, foreign.Color);
        }

        var offWhite = Assert.IsType<ColorNes>(factory.ToForeign(new ColorRgba32(250, 250, 250, 255), ColorModel.Nes));
        Assert.Equal(0x20u, offWhite.Color);
    }

    [Fact]
    public void ToForeign_MasterLongerThan64_StaysBelow64()
    {
        var target = new ColorRgba32(255, 0, 255, 255);
        var colors = Enumerable.Repeat(new ColorRgba32(0, 0, 0, 255), 64).Append(target).ToArray();
        var factory = CreateNesFactory(ArrangerTestFactory.CreatePalette(colors));

        var foreign = Assert.IsType<ColorNes>(factory.ToForeign(target, ColorModel.Nes));

        Assert.True(foreign.Color < 64);
    }
}
