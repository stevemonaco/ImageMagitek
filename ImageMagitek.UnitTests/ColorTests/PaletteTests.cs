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
        var palette = CreateFilePalette(OpenReadOnlySource(), [new FileColorSource(BitAddress.Zero, Endian.Little)]);

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
        var palette = CreateFilePalette(source, [new FileColorSource(BitAddress.Zero, Endian.Little)]);
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

    private static IList<IColorSource> CreateFileSources(int count) =>
        Enumerable.Range(0, count).Select(i => (IColorSource)new FileColorSource(new BitAddress(i * 2, 0), Endian.Little)).ToList();

    private static Palette CreateFilePalette(DataSource source, IList<IColorSource> colorSources) =>
        new("file", new ColorFactory(), ColorModel.Bgr15, colorSources, false, PaletteStorageSource.ProjectXml, source);

    private FileDataSource OpenReadOnlySource() => _files.Open("readonly", _fileColors);

    public void Dispose() => _files.Dispose();
}
