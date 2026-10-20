using System;
using System.IO;
using System.Linq;
using ImageMagitek.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

public sealed class MapFormatTests : IDisposable
{
    private static readonly string _schemaFileName = Path.Combine(AppContext.BaseDirectory, "_schemas", "MapFormatSchema.xsd");
    private static readonly string _formatsPath = Path.Combine(AppContext.BaseDirectory, "_mapformats");

    private readonly string _directory = TestPaths.CreateTempPath("");

    public MapFormatTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    public static MapFormatFactory LoadShippedFormats()
    {
        var factory = new MapFormatFactory();
        var failures = factory.LoadFormats(_formatsPath, _schemaFileName);
        Assert.Empty(failures);
        return factory;
    }

    [Fact]
    public void ShippedFormats_AllLoad()
    {
        var factory = LoadShippedFormats();

        Assert.Equal(["FRLG Block", "Game Boy BG", "GBA Affine", "GBA Text BG", "Genesis Plane", "Plain 8-bit Index", "SNES BG", "SNES Mode 7"],
            factory.Formats.Select(x => x.Name));
    }

    [Theory]
    [InlineData("SNES BG", new byte[] { 0x55, 0xF7 }, 0x355, 5, true, true, true)]
    [InlineData("SNES BG", new byte[] { 0xFF, 0x03 }, 0x3FF, 0, false, false, false)]
    [InlineData("GBA Text BG", new byte[] { 0x23, 0xA5 }, 0x123, 0xA, true, false, false)]
    [InlineData("GBA Text BG", new byte[] { 0x00, 0x08 }, 0, 0, false, true, false)]
    [InlineData("Genesis Plane", new byte[] { 0xC9, 0x23 }, 0x123, 2, true, false, true)]
    [InlineData("Genesis Plane", new byte[] { 0x10, 0x05 }, 0x5, 0, false, true, false)]
    [InlineData("SNES Mode 7", new byte[] { 0xAB }, 0xAB, 0, false, false, false)]
    [InlineData("GBA Affine", new byte[] { 0xFE }, 0xFE, 0, false, false, false)]
    [InlineData("Game Boy BG", new byte[] { 0x80 }, 0x80, 0, false, false, false)]
    [InlineData("Plain 8-bit Index", new byte[] { 0x07 }, 0x07, 0, false, false, false)]
    public void Decode_ShippedFormat_ReadsEveryField(string formatName, byte[] entry, int tile, int palette, bool hflip, bool vflip, bool priority)
    {
        var format = LoadShippedFormats().Find(formatName)!;

        var decoded = format.Decode(entry);

        Assert.Equal(new MapEntry(tile, palette, hflip, vflip, priority), decoded);
    }

    public static TheoryData<string> ShippedFormatNames() =>
        new(LoadShippedFormats().Formats.Select(x => x.Name));

    [Theory]
    [MemberData(nameof(ShippedFormatNames))]
    public void Encode_ShippedFormat_RoundTripsEveryField(string formatName)
    {
        var format = LoadShippedFormats().Find(formatName)!;
        MapEntry[] entries =
        [
            new(format.MaxTileIndex, format.PaletteSlotCount - 1, format.HorizontalFlip is not null, format.VerticalFlip is not null, format.Priority is not null),
            new(0, 0, false, false, false),
            new(1, format.PaletteSlotCount / 2, false, format.VerticalFlip is not null, false)
        ];

        foreach (var entry in entries)
        {
            var bytes = new byte[format.EntryBytes];
            format.Encode(entry, bytes);

            Assert.Equal(entry, format.Decode(bytes));
        }
    }

    [Fact]
    public void Encode_KeepsPriorityAndBitsOutsideFields()
    {
        var narrow = new MapFormat("Narrow", 16, Endian.Little, new MapField(0, 10));
        byte[] bytes = [0xFF, 0xFF];

        narrow.Encode(new MapEntry(0x123, 0, false, false, false), bytes);

        Assert.Equal(new byte[] { 0x23, 0xFD }, bytes);

        var snes = LoadShippedFormats().Find("SNES BG")!;
        byte[] snesBytes = [0x00, 0x20];

        snes.Encode(snes.Decode(snesBytes) with { TileIndex = 5, HorizontalFlip = true }, snesBytes);

        Assert.Equal(new byte[] { 0x05, 0x60 }, snesBytes);
    }

    [Fact]
    public void Encode_ValueWiderThanField_Throws()
    {
        var format = LoadShippedFormats().Find("SNES BG")!;
        var bytes = new byte[2];

        Assert.Throws<ArgumentOutOfRangeException>(() => format.Encode(new MapEntry(0x400, 0, false, false, false), bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => format.Encode(new MapEntry(0, 8, false, false, false), bytes));
        Assert.Equal(new byte[2], bytes);
    }

    [Fact]
    public void PaletteSlotCount_FollowsPaletteBits()
    {
        var factory = LoadShippedFormats();

        Assert.Equal(16, factory.Find("GBA Text BG")!.PaletteSlotCount);
        Assert.Equal(8, factory.Find("SNES BG")!.PaletteSlotCount);
        Assert.Equal(4, factory.Find("Genesis Plane")!.PaletteSlotCount);
        Assert.Equal(1, factory.Find("SNES Mode 7")!.PaletteSlotCount);
    }

    [Fact]
    public void Load_FieldWiderThanEntry_FailsNamingFile()
    {
        var fileName = WriteFormat("wide.xml", """
            <mapformat name="Wide" entrybits="8">
              <tileindex lowbit="0" bits="10" />
            </mapformat>
            """);

        var result = new XmlMapFormatReader(_schemaFileName).LoadFromFile(fileName);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains(fileName) && x.Contains("tileindex"));
    }

    [Fact]
    public void Load_OverlappingFields_FailsNamingFile()
    {
        var fileName = WriteFormat("overlap.xml", """
            <mapformat name="Overlap" entrybits="16">
              <tileindex lowbit="0" bits="10" />
              <hflip bit="9" />
            </mapformat>
            """);

        var result = new XmlMapFormatReader(_schemaFileName).LoadFromFile(fileName);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains(fileName) && x.Contains("overlap"));
    }

    [Fact]
    public void LoadFormats_DuplicateName_KeepsFirstAndReportsFile()
    {
        WriteFormat("a.xml", """<mapformat name="Dup" entrybits="8"><tileindex bits="8" /></mapformat>""");
        var second = WriteFormat("b.xml", """<mapformat name="dup" entrybits="16"><tileindex bits="8" /></mapformat>""");
        var factory = new MapFormatFactory();

        var failures = factory.LoadFormats(_directory, _schemaFileName);

        Assert.Equal(second, Assert.Single(failures).FileName);
        Assert.Equal(8, factory.Find("DUP")!.EntryBits);
    }

    private string WriteFormat(string name, string xml)
    {
        var fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, xml);
        return fileName;
    }
}
