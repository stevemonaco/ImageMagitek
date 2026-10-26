using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.Tilemaps;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Synthetic bytes shaped like FireRed's map tables for <c>Fixtures/Tables/Synthetic.table.xml</c>: 2 map groups of 2 headers, 2 layouts sharing
/// a compressed primary tileset, and 2 uncompressed secondary tilesets sharing palettes, the second with an attribute table before its metatiles
/// </summary>
public static class FireRedShapedData
{
    public const long Headers = 0x40;
    public const int HeaderSize = 0x1C;
    public const long LayoutA = 0x100;
    public const long LayoutB = 0x120;
    public const long Primary = 0x200;
    public const long SecondaryA = 0x220;
    public const long SecondaryB = 0x240;
    public const long BlocksA = 0x300;
    public const long BlocksB = 0x320;
    public const long BorderA = 0x400;
    public const long NameTable = 0x500;
    public const long PrimaryTiles = 0x1000;
    public const long SecondaryTiles = 0x1100;
    public const long PrimaryPalettes = 0x2000;
    public const long SecondaryPalettes = 0x2200;
    public const long PrimaryMetatiles = 0x3000;
    public const long SecondaryMetatilesA = 0x6800;
    public const long SecondaryMetatilesB = 0x6900;
    public const int Size = 0x8000;

    /// <summary>
    /// Unused bytes that a repointed block map can be copied to
    /// </summary>
    public const long FreeSpace = 0x340;

    private static uint Pointer(long offset) => (uint)(GbaAddressMapper.RomBase + offset);

    public static byte[] Create()
    {
        var rom = new byte[Size];
        void Put16(long offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan((int)offset), (ushort)value);

        Put32(rom, 0x00, Pointer(0x10));
        Put32(rom, 0x04, Pointer(0x18));
        for (var i = 0; i < 4; i++)
            Put32(rom, 0x10 + 4 * i, Pointer(Headers + HeaderSize * i));

        (long Layout, byte Section, byte Type)[] headers = [(LayoutA, 0x58, 1), (LayoutB, 0x58, 8), (LayoutB, 0x59, 8), (LayoutB, 0x59, 8)];
        for (var i = 0; i < headers.Length; i++)
        {
            var offset = Headers + HeaderSize * i;
            Put32(rom, offset, Pointer(headers[i].Layout));
            rom[offset + 0x14] = headers[i].Section;
            rom[offset + 0x17] = headers[i].Type;
        }

        WriteLayout(rom, LayoutA, 3, 2, BorderA, BlocksA, SecondaryA, 2, 2);
        WriteLayout(rom, LayoutB, 2, 2, BorderA + 0x10, BlocksB, SecondaryB, 0, 0);

        WriteTileset(rom, Primary, true, false, PrimaryTiles, PrimaryPalettes, PrimaryMetatiles, 0x7000);
        WriteTileset(rom, SecondaryA, false, true, SecondaryTiles, SecondaryPalettes, SecondaryMetatilesA, SecondaryMetatilesA + 0x40);
        WriteTileset(rom, SecondaryB, false, true, SecondaryTiles, SecondaryPalettes, SecondaryMetatilesB, SecondaryMetatilesB - 0x80);

        int[] blocksA = [0, 1 | 0x0400, 640, 641 | 0x3000, 642, 643];
        for (var i = 0; i < blocksA.Length; i++)
            Put16(BlocksA + 2 * i, blocksA[i]);

        for (var i = 0; i < 4; i++)
            Put16(BlocksB + 2 * i, 640 + i);

        int[] border = [1, 1, 640, 640];
        for (var i = 0; i < border.Length; i++)
            Put16(BorderA + 2 * i, border[i]);

        Put32(rom, NameTable, Pointer(NameTable + 0x10));
        Put32(rom, NameTable + 4, Pointer(NameTable + 0x20));
        Encode("PALLET TOWN").CopyTo(rom, NameTable + 0x10);
        Encode("VIRIDIAN CITY").CopyTo(rom, NameTable + 0x20);

        CompressLiterals(Enumerable.Repeat((byte)0x11, 4 * 32).ToArray()).CopyTo(rom, PrimaryTiles);

        for (var i = 0; i < 13 * 16; i++)
            Put16(PrimaryPalettes + 2 * i, i);

        return rom;
    }

    /// <summary>
    /// Copies a layout's block map to <paramref name="blocks"/> and points the layout at the copy
    /// </summary>
    public static void RepointLayoutBlocks(byte[] rom, long layout, long blocks)
    {
        var width = BinaryPrimitives.ReadInt32LittleEndian(rom.AsSpan((int)layout));
        var height = BinaryPrimitives.ReadInt32LittleEndian(rom.AsSpan((int)layout + 4));
        var current = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan((int)layout + 0xC)) - GbaAddressMapper.RomBase;
        rom.AsSpan((int)current, width * height * 2).CopyTo(rom.AsSpan((int)blocks));
        Put32(rom, layout + 0xC, Pointer(blocks));
    }

    /// <summary>
    /// Unused bytes past the primary tileset's attribute table that its compressed tiles can be copied to
    /// </summary>
    public const long TilesFreeSpace = 0x7C00;

    /// <summary>
    /// Copies the primary tileset's compressed tiles to <see cref="TilesFreeSpace"/> and points the tileset at the copy
    /// </summary>
    public static void RepointPrimaryTiles(byte[] rom)
    {
        rom.AsSpan((int)PrimaryTiles, 4 + 16 * 9).CopyTo(rom.AsSpan((int)TilesFreeSpace));
        Put32(rom, Primary + 4, Pointer(TilesFreeSpace));
    }

    /// <summary>
    /// The bytes inside <see cref="RepointPrimaryTilesNested"/>'s innermost compressed block
    /// </summary>
    public static readonly byte[] NestedPayload = [1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>
    /// Writes primary tiles at <see cref="TilesFreeSpace"/> that hold, at offset 0, a compressed block holding at offset 0 a compressed
    /// <see cref="NestedPayload"/>, and points the tileset at them
    /// </summary>
    public static void RepointPrimaryTilesNested(byte[] rom)
    {
        var middle = CompressLiterals(CompressLiterals(NestedPayload));
        var tiles = new byte[4 * 32];
        middle.CopyTo(tiles, 0);
        CompressLiterals(tiles).CopyTo(rom, TilesFreeSpace);
        Put32(rom, Primary + 4, Pointer(TilesFreeSpace));
    }

    private static byte[] CompressLiterals(byte[] data)
    {
        var block = new List<byte> { 0x10, (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16) };
        foreach (var group in data.Chunk(8))
        {
            block.Add(0);
            block.AddRange(group);
        }

        return [.. block];
    }

    private static void Put32(byte[] rom, long offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan((int)offset), value);

    private static void WriteLayout(byte[] rom, long offset, uint width, uint height, long border, long blocks, long secondary, byte borderWidth, byte borderHeight)
    {
        Put32(rom, offset, width);
        Put32(rom, offset + 4, height);
        Put32(rom, offset + 8, Pointer(border));
        Put32(rom, offset + 0xC, Pointer(blocks));
        Put32(rom, offset + 0x10, Pointer(Primary));
        Put32(rom, offset + 0x14, Pointer(secondary));
        rom[offset + 0x18] = borderWidth;
        rom[offset + 0x19] = borderHeight;
    }

    private static void WriteTileset(byte[] rom, long offset, bool isCompressed, bool isSecondary, long tiles, long palettes, long metatiles, long attributes)
    {
        rom[offset] = isCompressed ? (byte)1 : (byte)0;
        rom[offset + 1] = isSecondary ? (byte)1 : (byte)0;
        Put32(rom, offset + 4, Pointer(tiles));
        Put32(rom, offset + 8, Pointer(palettes));
        Put32(rom, offset + 0xC, Pointer(metatiles));
        Put32(rom, offset + 0x14, Pointer(attributes));
    }

    /// <summary>
    /// Encodes upper-case letters and spaces in the gen3 encoding, terminated with FF
    /// </summary>
    public static byte[] Encode(string text)
    {
        var bytes = new byte[text.Length + 1];
        for (var i = 0; i < text.Length; i++)
            bytes[i] = text[i] == ' ' ? (byte)0 : (byte)(0xBB + text[i] - 'A');

        bytes[^1] = 0xFF;
        return bytes;
    }
}

/// <summary>
/// A temp directory for table definitions, charmaps and data files, loaded with the built schemas and deleted on dispose
/// </summary>
public sealed class TableTestContext : IDisposable
{
    public static string SchemaDirectory => Path.Combine(AppContext.BaseDirectory, "_schemas");
    public static string FixturesDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Tables");

    public string Directory { get; } = TestPaths.CreateTempPath("");
    public TempDataFiles Files { get; } = new();

    public TableTestContext()
    {
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Write(string fileName, string contents)
    {
        var path = Path.Combine(Directory, fileName);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Copies the table fixtures (definitions, charmap, manifest) into the directory
    /// </summary>
    public void CopyFixtures()
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(FixturesDirectory))
            File.Copy(file, Path.Combine(Directory, Path.GetFileName(file)));
    }

    public MagitekResults<TableDefinition> Load(string fileName) =>
        new XmlTableDefinitionReader(SchemaDirectory).LoadFromFile(Path.Combine(Directory, fileName));

    /// <summary>
    /// Writes a definition with the test charmap beside it and loads it
    /// </summary>
    public MagitekResults<TableDefinition> LoadXml(string xml)
    {
        var charmap = Path.Combine(Directory, "test.charmap.xml");
        if (!File.Exists(charmap))
            File.Copy(Path.Combine(FixturesDirectory, "test.charmap.xml"), charmap);

        Write("test.table.xml", xml);
        return Load("test.table.xml");
    }

    public TableDefinition LoadValid(string xml)
    {
        var result = LoadXml(xml);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    public void Dispose()
    {
        Files.Dispose();

        try
        {
            System.IO.Directory.Delete(Directory, true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>
/// A new project in a temp directory holding a FireRed-shaped '/Rom' data file, with the table fixtures (minus the foreign manifest) beside it
/// </summary>
public sealed class GenerationProjectContext : IDisposable
{
    public string Directory { get; } = TestPaths.CreateTempPath("");
    public TableTestContext Definitions { get; } = new();
    public ProjectService Service { get; }
    public ResourceGenerationService Generation { get; }
    public ProjectTree Tree { get; private set; } = null!;

    public string ProjectFile => Path.Combine(Directory, "project.xml");
    public string DefinitionFile => Path.Combine(Definitions.Directory, "Synthetic.table.xml");
    public string ManifestFile => Path.Combine(Definitions.Directory, "Synthetic.manifest.xml");

    public GenerationProjectContext(Func<ProjectWorkspace, ResourceGenerationService>? createGeneration = null)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllBytes(Path.Combine(Directory, "rom.bin"), FireRedShapedData.Create());
        Definitions.CopyFixtures();
        File.Delete(Path.Combine(Definitions.Directory, "Foreign.manifest.xml"));

        var workspace = new ProjectWorkspace(new XmlProjectSerializerFactory(ProjectFixtures.ResourceSchemaPath, CodecFixture.Shared.CodecFactory,
            new ColorFactory(), [ProjectFixtures.DefaultPalette], null, MapFormatTests.LoadShippedFormats()));
        Service = new ProjectService(workspace);
        Generation = createGeneration?.Invoke(workspace) ?? new ResourceGenerationService(workspace);
    }

    public string RomFile => Path.Combine(Directory, "rom.bin");

    /// <summary>
    /// Closes the project, applies <paramref name="editRom"/> to the data file and opens the project again
    /// </summary>
    public async Task ReopenAsync(Action<byte[]>? editRom = null)
    {
        Service.CloseProjects();

        if (editRom is not null)
        {
            var rom = File.ReadAllBytes(RomFile);
            editRom(rom);
            File.WriteAllBytes(RomFile, rom);
        }

        var result = await Service.OpenProjectFileAsync(ProjectFile);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        Tree = result.AsSuccess.Result;
    }

    /// <summary>
    /// Adds folders '/A' then '/Z', compressed data '/A/C2' over <paramref name="parentKey"/> and '/Z/C3' over C2; a reopened tree lists '/Z' first
    /// </summary>
    public async Task AddCompressedChainAsync(string parentKey)
    {
        Assert.True(Tree.TryGetItem<DataSource>(parentKey, out var parent));
        var a = (await Service.CreateNewFolderAsync(Tree.Root, "A")).AsSuccess.Result;
        var z = (await Service.CreateNewFolderAsync(Tree.Root, "Z")).AsSuccess.Result;
        var compressors = new CompressorFactory();

        var c2 = new CompressedDataSource("C2", parent, 0, "GBA LZ77", compressors);
        Assert.True((await Service.AddResourceAsync(a, c2)).HasSucceeded);
        var c3 = new CompressedDataSource("C3", c2, 0, "GBA LZ77", compressors);
        Assert.True((await Service.AddResourceAsync(z, c3)).HasSucceeded);
    }

    public void EditDefinition(Func<string, string> edit) =>
        File.WriteAllText(DefinitionFile, edit(File.ReadAllText(DefinitionFile)), new UTF8Encoding(false));

    public async Task InitializeAsync()
    {
        Tree = (await Service.CreateNewProjectAsync(ProjectFile)).AsSuccess.Result;
        Assert.True((await Service.AddResourceAsync(Tree.Root, new FileDataSource("Rom", Path.Combine(Directory, "rom.bin")))).HasSucceeded);
    }

    /// <summary>
    /// The bytes of every project XML file and every definition-directory file
    /// </summary>
    public Dictionary<string, byte[]> Snapshot() =>
        System.IO.Directory.EnumerateFiles(Directory, "*.xml", SearchOption.AllDirectories)
            .Concat(System.IO.Directory.EnumerateFiles(Definitions.Directory, "*", SearchOption.AllDirectories))
            .ToDictionary(x => x, File.ReadAllBytes);

    public void AssertUnchanged(Dictionary<string, byte[]> before)
    {
        var after = Snapshot();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (file, bytes) in before)
            Assert.Equal(bytes, after[file]);
    }

    public void Dispose()
    {
        Service.CloseProjects();
        Definitions.Dispose();

        try
        {
            System.IO.Directory.Delete(Directory, true);
        }
        catch (IOException)
        {
        }
    }
}
