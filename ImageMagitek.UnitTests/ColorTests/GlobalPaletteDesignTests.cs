using System;
using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests;

public class GlobalPaletteDesignTests
{
    private const double _minLightnessStep = 0.15;
    private const double _minDistance = 0.12;

    public static TheoryData<string> PaletteNames => ["Vivid", "Synthwave", "Dusk"];

    [Theory]
    [MemberData(nameof(PaletteNames))]
    public void Has256OpaqueEntries(string name)
    {
        var palette = Load(name);

        Assert.Equal(256, palette.Entries);
        for (int i = 0; i < 256; i++)
            Assert.Equal(255, palette[i].A);
    }

    [Theory]
    [InlineData("Vivid", 256)]
    [InlineData("Synthwave", 256)]
    [InlineData("Dusk", 256)]
    [InlineData("Classic", 256)]
    [InlineData("DefaultNes", 64)]
    public void ShippedPalette_HasEntriesAndOpaqueZeroIndex(string name, int entries)
    {
        var palette = Load(name);

        Assert.Equal(entries, palette.Entries);
        Assert.False(palette.ZeroIndexTransparent);
    }

    [Theory]
    [MemberData(nameof(PaletteNames))]
    public void FirstFour_StepInLightness(string name)
    {
        var palette = Load(name);

        for (int i = 0; i < 3; i++)
        {
            var step = OkLab.ToOkLab(palette[i + 1]).L - OkLab.ToOkLab(palette[i]).L;
            Assert.True(step >= _minLightnessStep, $"Entries {i} and {i + 1} differ by {step:F3} in lightness");
        }
    }

    [Theory]
    [MemberData(nameof(PaletteNames))]
    public void FirstSixteen_PairwiseDistinct(string name)
    {
        var palette = Load(name);

        for (int i = 0; i < 16; i++)
        {
            for (int j = i + 1; j < 16; j++)
            {
                var distance = OkLab.DeltaE(palette[i], palette[j]);
                Assert.True(distance >= _minDistance, $"Entries {i} and {j} are {distance:F3} apart");
            }
        }
    }

    [Theory]
    [MemberData(nameof(PaletteNames))]
    public void Rows_RampInLightness(string name)
    {
        var palette = Load(name);

        for (int row = 16; row < 256; row += 16)
        {
            for (int i = row; i < row + 15; i++)
                Assert.True(OkLab.ToOkLab(palette[i + 1]).L > OkLab.ToOkLab(palette[i]).L, $"Entry {i + 1} is not lighter than entry {i}");
        }
    }

    private static Palette Load(string name) =>
        new PaletteService(new ColorFactory()).ReadJsonPalette(Path.Combine(AppContext.BaseDirectory, "_palettes", name + ".json"))!;
}
