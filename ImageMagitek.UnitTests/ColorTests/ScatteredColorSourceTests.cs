using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests.ColorTests;

public sealed class ScatteredColorSourceTests
{
    private readonly ColorFactory _colorFactory = new();

    private static MemoryDataSource CreateSource(params byte[] bytes)
    {
        var source = new MemoryDataSource("data", bytes.Length);
        source.Write(new BitAddress(0), bytes);
        return source;
    }

    private Palette CreatePalette(DataSource source, ColorModel model, int entries, params ColorFieldRun[] runs) =>
        CreatePalette(source, model, ScatteredColorSource.ExpandRun(entries, runs), _colorFactory);

    private static Palette CreatePalette(DataSource source, ColorModel model, IEnumerable<ScatteredColorSource> sources, IColorFactory colorFactory) =>
        new("scattered", colorFactory, model, sources.ToList<IColorSource>(), false, PaletteStorageSource.ProjectXml, source);

    private static ColorFieldRun Run(long offset, int bits, int shift = 0, int lowBit = 0, int stride = 8) =>
        new(new ColorField(offset, lowBit, bits, shift), stride);

    private static uint[] Colors(Palette palette) =>
        Enumerable.Range(0, palette.Entries).Select(i => palette.GetForeignColor(i).Color).ToArray();

    private static byte[] ReadAll(DataSource source) => source.Read(new BitAddress(0), (int)source.Length * 8);

    [Fact]
    public void Load_PerChannelByteTables_Bgr15()
    {
        var source = CreateSource(0x00, 0x1F, 0x03, 0x00, 0x10, 0x07, 0x00, 0x05, 0x1F);
        var palette = CreatePalette(source, ColorModel.Bgr15, 3, Run(0, 5), Run(3, 5, shift: 5), Run(6, 5, shift: 10));

        var channels = Enumerable.Range(0, 3)
            .Select(i => Assert.IsType<ColorBgr15>(palette.GetForeignColor(i)))
            .Select(c => ((int)c.R, (int)c.G, (int)c.B));
        Assert.Equal([(0, 0, 0), (0x1F, 0x10, 0x05), (0x03, 0x07, 0x1F)], channels);
    }

    [Fact]
    public void Load_LowBitInsideByte()
    {
        var palette = CreatePalette(CreateSource(0b1011_0100), ColorModel.Bgr15, 1, Run(0, 3, lowBit: 2));

        Assert.Equal([0b101u], Colors(palette));
    }

    [Fact]
    public void Load_NibblePackedStride4_LowNibbleFirst()
    {
        var palette = CreatePalette(CreateSource(0x21, 0x43), ColorModel.Bgr15, 4, Run(0, 4, stride: 4));

        Assert.Equal([1u, 2u, 3u, 4u], Colors(palette));
    }

    [Fact]
    public void Load_LowAndHighByteTables()
    {
        var palette = CreatePalette(CreateSource(0x34, 0xCD, 0x12, 0xFF), ColorModel.Bgr15, 2, Run(0, 8), Run(2, 7, shift: 8));

        Assert.Equal([0x1234u, 0x7FCDu], Colors(palette));
    }

    [Fact]
    public void Load_UncoveredBitsAreZero()
    {
        var palette = CreatePalette(CreateSource(0xFF), ColorModel.Bgr15, 1, Run(0, 4, shift: 1));

        Assert.Equal([0x1Eu], Colors(palette));
    }

    [Fact]
    public void Load_NesTableModel()
    {
        var master = NesColorConversionTests.LoadMasterPalette();
        var factory = NesColorConversionTests.CreateNesFactory(master);
        var sources = ScatteredColorSource.ExpandRun(1, [Run(0, 6, lowBit: 1)]);
        var palette = CreatePalette(CreateSource((byte)(0x2A << 1 | 0x81)), ColorModel.Nes, sources, factory);

        Assert.Equal(0x2Au, Assert.IsType<ColorNes>(palette.GetForeignColor(0)).Color);
        Assert.Equal(master.GetNativeColor(0x2A), palette.GetNativeColor(0));
    }

    [Fact]
    public void Store_WritesOnlyFieldBits()
    {
        var source = CreateSource(0xAA, 0xAA, 0xAA);
        var fields = new[] { new ColorField(0, 2, 3, 0), new ColorField(0, 5, 2, 3), new ColorField(2, 1, 4, 5) };
        var palette = CreatePalette(source, ColorModel.Bgr15, [new ScatteredColorSource(fields)], _colorFactory);

        palette.SetForeignColor(0, new ColorBgr15(0));
        Assert.True(palette.SavePalette());
        Assert.Equal([0x82, 0xAA, 0xA0], ReadAll(source));

        palette.SetForeignColor(0, new ColorBgr15(0x1FF));
        Assert.True(palette.SavePalette());
        Assert.Equal([0xFE, 0xAA, 0xBE], ReadAll(source));
    }

    [Fact]
    public void Store_RoundTripsEveryEntry()
    {
        var source = CreateSource(new byte[8]);
        ColorFieldRun[] runs = [Run(0, 4, stride: 4), Run(2, 8, shift: 4), Run(6, 3, shift: 12, lowBit: 5, stride: 3)];
        var palette = CreatePalette(source, ColorModel.Bgr15, 2, runs);
        uint[] expected = [0x7ABC, 0x1234];

        for (int i = 0; i < expected.Length; i++)
            palette.SetForeignColor(i, new ColorBgr15(expected[i]));
        Assert.True(palette.SavePalette());

        Assert.Equal(expected, Colors(CreatePalette(source, ColorModel.Bgr15, 2, runs)));
    }

    [Fact]
    public void SetColor_ClearsUncoveredBits()
    {
        var palette = CreatePalette(CreateSource(0x00), ColorModel.Bgr15, 1, Run(0, 4, shift: 1));

        palette.SetForeignColor(0, new ColorBgr15(0x7FFF));
        Assert.Equal(0x1Eu, palette.GetForeignColor(0).Color);

        palette.SetNativeColor(0, new ColorRgba32(255, 255, 255, 255));
        Assert.Equal(0x1Eu, palette.GetForeignColor(0).Color);
        Assert.Equal(_colorFactory.ToNative(new ColorBgr15(0x1E)), palette.GetNativeColor(0));
    }

    [Theory]
    [InlineData("no fields", 0)]
    [InlineData("bits 0", 0)]
    [InlineData("bits 9", 0)]
    [InlineData("low bit 8", 0)]
    [InlineData("low bit plus bits over 8", 0)]
    [InlineData("negative shift", 0)]
    [InlineData("value overlap", 1)]
    [InlineData("file overlap", 1)]
    public void Construct_InvalidFields_Throws(string name, int field)
    {
        ColorField[] fields = name switch
        {
            "no fields" => [],
            "bits 0" => [new(0, 0, 0, 0)],
            "bits 9" => [new(0, 0, 9, 0)],
            "low bit 8" => [new(0, 8, 1, 0)],
            "low bit plus bits over 8" => [new(0, 5, 4, 0)],
            "negative shift" => [new(0, 0, 4, -1)],
            "value overlap" => [new(0, 0, 4, 0), new(1, 0, 4, 3)],
            "file overlap" => [new(0, 0, 4, 0), new(0, 3, 4, 8)],
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        var ex = Assert.Throws<ColorFieldException>(() => new ScatteredColorSource(fields));

        Assert.Equal(field, ex.Field);
        Assert.StartsWith($"Field {field}:", ex.Message);
    }
}
