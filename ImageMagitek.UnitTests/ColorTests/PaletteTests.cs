using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;
using ImageMagitek.UnitTests.ColorTests;
using Xunit;

namespace ImageMagitek.UnitTests;

public sealed class PaletteTests : IDisposable
{
    private static readonly byte[] _fileColors = [0x00, 0x7C, 0xE0, 0x03, 0x1F, 0x00, 0xFF, 0x7F];
    private readonly TempDataFiles _files = new();

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
    public void ProjectPalette_NativeSource_KeepsNativeAndConvertsForeignToModel()
    {
        var palette = CreateFilePalette(OpenReadOnlySource(), [new ProjectNativeColorSource(new ColorRgba32(255, 0, 0, 255))]);

        var foreign = Assert.IsType<ColorBgr15>(palette.GetForeignColor(0));
        Assert.Equal(31, foreign.R);
        Assert.Equal(new ColorRgba32(255, 0, 0, 255), palette.GetNativeColor(0));
    }

    [Fact]
    public void SetNativeColor_FileSource_StoresRoundTrippedNative()
    {
        var palette = CreateFilePalette(OpenReadOnlySource(), CreateFileSources(4));

        palette.SetNativeColor(0, new ColorRgba32(255, 0, 0, 255));

        Assert.Equal(new ColorRgba32(248, 0, 0, 255), palette.GetNativeColor(0));
    }

    [Fact]
    public void DeserializePalette_InvalidColor_ThrowsNamingEntry()
    {
        var json = """{ "Name": "bad", "Colors": [ "#000000", "#GG0000" ] }""";

        var ex = Assert.Throws<InvalidDataException>(() => PaletteJsonSerializer.DeserializePalette(json, new ColorFactory()));

        Assert.Contains("color 1", ex.Message);
        Assert.Contains("#GG0000", ex.Message);
    }

    [Fact]
    public void SetNativeColor_NesPalette_StoresMatchingIndex()
    {
        var master = NesColorConversionTests.LoadMasterPalette();
        var factory = NesColorConversionTests.CreateNesFactory(master);
        var palette = new Palette("nes", factory, ColorModel.Nes, false, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(Enumerable.Repeat(new ProjectForeignColorSource(new ColorNes(0x0F)), 4));

        palette.SetNativeColor(1, master.GetNativeColor(0x21));

        var foreign = Assert.IsType<ColorNes>(palette.GetForeignColor(1));
        Assert.Equal(0x21u, foreign.Color);
    }

    [Fact]
    public void GlobalJsonPalette_NesModel_Loads()
    {
        var master = NesColorConversionTests.LoadMasterPalette();
        var factory = NesColorConversionTests.CreateNesFactory(master);
        var palette = new Palette("nes", factory, ColorModel.Nes, false, PaletteStorageSource.GlobalJson);
        palette.SetColorSources([new ProjectNativeColorSource(master.GetNativeColor(0x16)), new ProjectNativeColorSource(master.GetNativeColor(0x2A))]);

        Assert.Equal(0x16u, Assert.IsType<ColorNes>(palette.GetForeignColor(0)).Color);
        Assert.Equal(0x2Au, Assert.IsType<ColorNes>(palette.GetForeignColor(1)).Color);
    }

    [Fact]
    public void IsReadOnly_FileColorsOnReadOnlySource_IsTrue()
    {
        var palette = CreateFilePalette(OpenReadOnlySource(), [new FileColorSource(0, Endian.Little)]);

        Assert.True(palette.IsReadOnly);
    }

    [Fact]
    public void IsReadOnly_ProjectColorsOnly_IsFalse()
    {
        var palette = CreateFilePalette(OpenReadOnlySource(), [new ProjectNativeColorSource(new ColorRgba32(0, 0, 0, 255))]);

        Assert.False(palette.IsReadOnly);
    }

    [Fact]
    public void SavePalette_ReadOnly_WritesNothingReturnsFalse()
    {
        var source = OpenReadOnlySource();
        var palette = CreateFilePalette(source, [new FileColorSource(0, Endian.Little)]);
        palette.SetNativeColor(0, new ColorRgba32(248, 248, 248, 255));

        Assert.False(palette.SavePalette());
        Assert.Equal(_fileColors, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void SavePalette_FileColor_IsOnDiskWhenSaveReturns()
    {
        var path = _files.Create(_fileColors, isReadOnly: false);
        var palette = CreateFilePalette(_files.Open("writable", path), CreateFileSources(4));
        palette.SetNativeColor(0, new ColorRgba32(248, 0, 0, 255));

        Assert.True(palette.SavePalette());

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[_fileColors.Length];
        stream.ReadExactly(bytes);
        Assert.Equal(new byte[] { 0x1F, 0x00, 0xE0, 0x03, 0x1F, 0x00, 0xFF, 0x7F }, bytes);
    }

    [Fact]
    public void SavePalette_DoesNotRaiseDataWritten()
    {
        var source = _files.Open("writable", _fileColors, isReadOnly: false);
        var palette = CreateFilePalette(source, CreateFileSources(4));
        var raised = false;
        source.DataWritten += (_, _) => raised = true;
        palette.SetNativeColor(0, new ColorRgba32(248, 0, 0, 255));

        palette.SavePalette();

        Assert.False(raised);
    }

    [Fact]
    public void Construct_ScatteredFieldOverflowsModel_Throws()
    {
        var sources = new List<IColorSource> { new ProjectNativeColorSource(default), new ScatteredColorSource([new ColorField(0, 0, 4, 0), new ColorField(1, 0, 8, 4)]) };

        var ex = Assert.Throws<ColorFieldException>(() => new Palette("p", new ColorFactory(), ColorModel.Bgr6, sources, false,
            PaletteStorageSource.ProjectXml, OpenReadOnlySource()));

        Assert.StartsWith("Entry 1 field 1:", ex.Message);
    }

    [Fact]
    public void SetColorModel_ScatteredFieldOverflowsModel_Throws()
    {
        var sources = new List<IColorSource> { new ScatteredColorSource([new ColorField(0, 0, 8, 8)]) };
        var palette = CreateFilePalette(OpenReadOnlySource(), sources);

        var ex = Assert.Throws<ColorFieldException>(() => palette.SetColorModel(ColorModel.Bgr6, sources));

        Assert.StartsWith("Entry 0 field 0:", ex.Message);
        Assert.Equal(ColorModel.Bgr15, palette.ColorModel);
    }

    [Fact]
    public void IsReadOnly_ScatteredOnReadOnlySource()
    {
        var palette = CreateFilePalette(OpenReadOnlySource(), [new ScatteredColorSource([new ColorField(0, 0, 8, 0)])]);

        Assert.True(palette.IsReadOnly);
    }

    [Fact]
    public void LoadColors_UnknownColorSource_Throws()
    {
        var serializer = new ColorSourceSerializer(new ColorFactory());

        var ex = Assert.Throws<NotSupportedException>(() => serializer.LoadColors([new UnknownColorSource()], OpenReadOnlySource(), ColorModel.Bgr15, 1));

        Assert.Contains(nameof(UnknownColorSource), ex.Message);
    }

    [Fact]
    public void StoreColors_UnknownColorSource_Throws()
    {
        var serializer = new ColorSourceSerializer(new ColorFactory());

        var ex = Assert.Throws<NotSupportedException>(() =>
            serializer.StoreColors([new UnknownColorSource()], OpenReadOnlySource(), [default], [new ColorBgr15(0)]));

        Assert.Contains(nameof(UnknownColorSource), ex.Message);
    }

    [Theory]
    [InlineData("not scattered", 0)]
    [InlineData("single", 1)]
    [InlineData("constant stride", 3)]
    [InlineData("stride changes", 2)]
    [InlineData("shift differs", 1)]
    [InlineData("field count differs", 1)]
    [InlineData("negative stride", 1)]
    [InlineData("per-field strides", 3)]
    public void GetScatteredRun_Count(string name, int expected)
    {
        static IColorSource At(params (long Position, int Shift)[] fields) =>
            new ScatteredColorSource(fields.Select(f => new ColorField(f.Position / 8, (int)(f.Position % 8), 4, f.Shift)));

        IColorSource[] sources = name switch
        {
            "not scattered" => [new FileColorSource(0, Endian.Little), At((0, 0))],
            "single" => [At((0, 0))],
            "constant stride" => [At((0, 0)), At((4, 0)), At((8, 0))],
            "stride changes" => [At((0, 0)), At((4, 0)), At((16, 0))],
            "shift differs" => [At((0, 0)), At((4, 4))],
            "field count differs" => [At((0, 0)), At((4, 0), (64, 4))],
            "negative stride" => [At((8, 0)), At((4, 0))],
            "per-field strides" => [At((0, 0), (64, 4)), At((4, 0), (72, 4)), At((8, 0), (80, 4))],
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        Assert.Equal(expected, Palette.GetScatteredRun(sources, 0).Count);
    }

    private sealed class UnknownColorSource : IColorSource;

    private static IList<IColorSource> CreateFileSources(int count) =>
        Enumerable.Range(0, count).Select(i => (IColorSource)new FileColorSource(i * 2, Endian.Little)).ToList();

    private static Palette CreateFilePalette(DataSource source, IList<IColorSource> colorSources) =>
        new("file", new ColorFactory(), ColorModel.Bgr15, colorSources, false, PaletteStorageSource.ProjectXml, source);

    private FileDataSource OpenReadOnlySource() => _files.Open("readonly", _fileColors);

    public void Dispose() => _files.Dispose();
}
