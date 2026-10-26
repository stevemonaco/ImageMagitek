using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

public sealed class TilemapArrangerTests : IDisposable
{
    private const int TileCount = 16;
    private const int TileBytes = 32;

    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly MapFormat _gba = _formats.Find("GBA Text BG")!;
    private static readonly MapFormat _snes = _formats.Find("SNES BG")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly Palette _slot0 = CreatePalette(10);
    private readonly Palette _slot1 = CreatePalette(20);
    private readonly MemoryDataSource _tiles = new("tiles");
    private readonly MemoryDataSource _map = new("map");

    public TilemapArrangerTests()
    {
        _tiles.Write(Enumerable.Range(0, TileCount * TileBytes).Select(x => (byte)x).ToArray());
    }

    public void Dispose()
    {
        _tiles.Dispose();
        _map.Dispose();
    }

    private static Palette CreatePalette(byte seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(seed + i), 0, 0, 255)).ToArray());

    private static byte[] Entry16(int tile, int palette = 0, bool hflip = false, bool vflip = false, int paletteShift = 12, int hBit = 10, int vBit = 11)
    {
        var value = tile | palette << paletteShift | (hflip ? 1 << hBit : 0) | (vflip ? 1 << vBit : 0);
        return [(byte)value, (byte)(value >> 8)];
    }

    private TilemapArranger Create(DataSource map, MapFormat format, MapScreenLayout layout, int width, int height,
        int firstTile = 0, params Palette?[] slots) =>
        new("tilemap", map, 0, format, layout, width, height, _tiles, 0, "GBA 4bpp", firstTile, slots.Length > 0 ? slots : [_slot0], _codecFactory);

    [Fact]
    public void Snes32Map_MatchesHandBuiltScatteredArranger()
    {
        const int width = 64, height = 32;
        var map = new byte[width * height * 2];
        var expected = new ScatteredArranger("expected", PixelColorType.Indexed, ElementLayout.Tiled, width, height, 8, 8);
        var codec0 = (IIndexedCodec)_codecFactory.CreateCodec("GBA 4bpp")!;
        codec0.Palette = _slot0;
        var codec1 = (IIndexedCodec)_codecFactory.CreateCodec("GBA 4bpp")!;
        codec1.Palette = _slot1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = (x + 3 * y) % TileCount;
                var palette = (x + y) % 2;
                var (hflip, vflip) = (y % 2 == 1, x % 3 == 0);
                var index = (x / 32) * 1024 + y * 32 + x % 32;
                Entry16(tile, palette, hflip, vflip, paletteShift: 10, hBit: 14, vBit: 15).CopyTo(map, index * 2);

                var mirror = (hflip, vflip) switch
                {
                    (true, true) => MirrorOperation.Both,
                    (true, false) => MirrorOperation.Horizontal,
                    (false, true) => MirrorOperation.Vertical,
                    _ => MirrorOperation.None
                };
                expected.SetElement(new ArrangerElement(0, 0, _tiles, new BitAddress(tile * TileBytes, 0),
                    palette == 0 ? codec0 : codec1, mirror, RotationOperation.None), x, y);
            }
        }

        _map.Write(map);
        using var tilemap = Create(_map, _snes, MapScreenLayout.Snes32, width, height, 0, _slot0, _slot1);

        Assert.Null(tilemap.MapError);
        AssertSameElements(expected, tilemap);
        Assert.Equal(new IndexedImage(expected).Image, new IndexedImage(tilemap).Image);
    }

    [Fact]
    public void LinearLayout_ReadsRowsInOrder()
    {
        _map.Write([.. Entry16(3), .. Entry16(1), .. Entry16(2), .. Entry16(0)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 2);

        var addresses = tilemap.EnumerateElements().Select(x => x!.Value.SourceAddress.ByteOffset).ToArray();

        Assert.Equal([3 * TileBytes, 1 * TileBytes, 2 * TileBytes, 0], addresses);
    }

    [Fact]
    public void IndexOutsideTileSet_GivesEmptyCell()
    {
        _map.Write([.. Entry16(1), .. Entry16(2), .. Entry16(2 + TileCount), .. Entry16(5)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 4, 1, firstTile: 2);

        Assert.Null(tilemap.GetElement(0, 0));
        Assert.Equal(0, tilemap.GetElement(1, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Null(tilemap.GetElement(2, 0));
        Assert.Equal(3 * TileBytes, tilemap.GetElement(3, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(2, tilemap.OutOfRangeCount);
        Assert.Equal(2 + TileCount, tilemap.HighestTileIndex);
    }

    [Fact]
    public void FlipBits_MapToMirror()
    {
        _map.Write([.. Entry16(0), .. Entry16(0, hflip: true), .. Entry16(0, vflip: true), .. Entry16(0, hflip: true, vflip: true)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 4, 1);

        Assert.Equal([MirrorOperation.None, MirrorOperation.Horizontal, MirrorOperation.Vertical, MirrorOperation.Both],
            tilemap.EnumerateElements().Select(x => x!.Value.Mirror));
    }

    [Fact]
    public void PaletteFieldNamingEmptySlot_UsesSlotZero()
    {
        _map.Write([.. Entry16(0, palette: 0), .. Entry16(0, palette: 1), .. Entry16(0, palette: 7)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 3, 1, 0, _slot0, _slot1);

        var palettes = tilemap.EnumerateElements().Select(x => ((IIndexedCodec)x!.Value.Codec).Palette).ToArray();

        Assert.Equal([_slot0, _slot1, _slot0], palettes);
    }

    [Fact]
    public void ShortMap_HasErrorAndEmptyCells()
    {
        _map.Write([.. Entry16(1), .. Entry16(2), .. Entry16(3)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 2);

        Assert.NotNull(tilemap.MapError);
        Assert.Contains("3 of the 4", tilemap.MapError);
        Assert.NotNull(tilemap.GetElement(0, 1));
        Assert.Null(tilemap.GetElement(1, 1));
    }

    [Fact]
    public void CompressedMap_ReadsDecompressedBytes_AndRebuildsAfterParentWrite()
    {
        // GBA LZ77 at offset 0: four literal bytes, entries for tiles 2 and 3
        using var rom = new MemoryDataSource("rom");
        rom.Write([0x10, 0x04, 0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00]);
        using var compressed = new CompressedDataSource("packed", rom, 0, "GBA LZ77", new CompressorFactory());
        using var tilemap = Create(compressed, _gba, MapScreenLayout.Linear, 2, 1);

        Assert.Equal(2 * TileBytes, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(3 * TileBytes, tilemap.GetElement(1, 0)!.Value.SourceAddress.ByteOffset);

        rom.Write(new BitAddress(5, 0), [0x05]);
        rom.NotifyDataWritten();

        Assert.Equal(5 * TileBytes, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public void DataWrittenDuringRebuild_RebuildsOnNextRead()
    {
        using var map = new WriteDuringReadDataSource("map", Entry16(1));
        using var tilemap = Create(map, _gba, MapScreenLayout.Linear, 1, 1);
        map.WriteOnNextRead(Entry16(2));

        Assert.Equal(1 * TileBytes, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(2 * TileBytes, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public void UncompressedTilemap_IsWritable()
    {
        _map.Write(Entry16(0));
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 1, 1);

        Assert.Null(tilemap.GetReadOnlyReason());
        Assert.Null(tilemap.MapReadOnlyReason);
    }

    [Fact]
    public void CompressedMapWritableTiles_DrawableButMapReadOnly()
    {
        using var rom = new MemoryDataSource("rom");
        rom.Write([0x10, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00]);
        using var compressed = new CompressedDataSource("packed", rom, 0, "GBA LZ77", new CompressorFactory());
        using var tilemap = Create(compressed, _gba, MapScreenLayout.Linear, 1, 1);

        Assert.Null(tilemap.GetReadOnlyReason());
        Assert.Equal("reads compressed data 'packed', which is read-only", tilemap.MapReadOnlyReason);
        Assert.True(tilemap.WriteEntries(tilemap.CreateGrid()).HasFailed);
    }

    [Fact]
    public void CompressedTileSource_ReadOnlyReasonNamesCompressedSource()
    {
        using var rom = new MemoryDataSource("rom");
        rom.Write([0x10, 0x20, 0x00, 0x00, .. Enumerable.Repeat<byte[]>([0x00, .. new byte[8]], 4).SelectMany(x => x)]);
        using var compressedTiles = new CompressedDataSource("packed tiles", rom, 0, "GBA LZ77", new CompressorFactory());
        _map.Write(Entry16(0));
        using var tilemap = new TilemapArranger("tilemap", _map, 0, _gba, MapScreenLayout.Linear, 1, 1, compressedTiles, 0, "GBA 4bpp", 0,
            [_slot0], _codecFactory);

        Assert.Null(compressedTiles.DecompressionError);
        Assert.Equal("reads compressed data 'packed tiles', which is read-only", tilemap.GetReadOnlyReason());
    }

    [Fact]
    public void CompressedMapWithError_ReportsDecompressionError()
    {
        using var rom = new MemoryDataSource("rom");
        rom.Write([0xFF, 0x00]);
        using var compressed = new CompressedDataSource("packed", rom, 0, "GBA LZ77", new CompressorFactory());
        using var tilemap = Create(compressed, _gba, MapScreenLayout.Linear, 2, 1);

        Assert.Equal(compressed.DecompressionError, tilemap.MapError);
        Assert.All(tilemap.EnumerateElements(), Assert.Null);
    }

    [Fact]
    public void FindMissingDataSource_MissingMapFile_ReturnsIt()
    {
        using var map = new FileDataSource("missing map", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin"));
        using var tilemap = Create(map, _gba, MapScreenLayout.Linear, 2, 1);

        Assert.Same(map, tilemap.FindMissingDataSource());
        Assert.Contains("missing map", tilemap.MapError);
    }

    [Fact]
    public void Bake_ReturnsEquivalentScatteredArrangerWithOwnCodecs()
    {
        _map.Write([.. Entry16(1, palette: 1, hflip: true), .. Entry16(4)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 1, 0, _slot0, _slot1);

        var clone = tilemap.Bake();

        AssertSameElements(clone, tilemap);
        Assert.NotSame(tilemap.GetElement(0, 0)!.Value.Codec, clone.GetElement(0, 0)!.Value.Codec);
    }

    [Fact]
    public void CreateGrid_ReadsEntries_NullPastMapData()
    {
        _map.Write([.. Entry16(1, palette: 1, hflip: true), .. Entry16(2 + TileCount), .. Entry16(3)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 2, 0, _slot0, _slot1);

        var grid = tilemap.CreateGrid();

        Assert.Equal((2, 2), (grid.Width, grid.Height));
        Assert.Equal(new MapEntry(1, 1, true, false, false), grid[0, 0]);
        Assert.Equal(new MapEntry(2 + TileCount, 0, false, false, false), grid[1, 0]);
        Assert.Equal(new MapEntry(3, 0, false, false, false), grid[0, 1]);
        Assert.Null(grid[1, 1]);
    }

    [Fact]
    public void CreateGrid_IsACopy()
    {
        _map.Write([.. Entry16(1), .. Entry16(2)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 1);
        var grid = tilemap.CreateGrid();

        grid.TrySet(0, 0, null);

        Assert.NotNull(tilemap.CreateGrid()[0, 0]);
        Assert.NotNull(tilemap.GetElement(0, 0));
    }

    [Fact]
    public void WorkingView_UneditedGrid_RendersSameAsTilemap()
    {
        const int width = 64, height = 32;
        var map = new byte[width * height * 2];
        for (int i = 0; i < width * height; i++)
            Entry16((i * 7) % (TileCount + 3), i % 3, i % 2 == 1, i % 5 == 0, paletteShift: 10, hBit: 14, vBit: 15).CopyTo(map, i * 2);

        _map.Write(map);
        using var tilemap = Create(_map, _snes, MapScreenLayout.Snes32, width, height, 0, _slot0, _slot1);

        var view = tilemap.CreateWorkingView();

        AssertSameElements(tilemap, view);
        Assert.Equal(new IndexedImage(tilemap).Image, new IndexedImage(view).Image);
        Assert.NotSame(tilemap.GetElement(0, 0)!.Value.Codec, view.GetElement(0, 0)!.Value.Codec);
    }

    [Fact]
    public void WorkingView_RebuildsAfterGridEdit()
    {
        _map.Write([.. Entry16(1), .. Entry16(2)]);
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 2, 1);
        var view = tilemap.CreateWorkingView();
        Assert.NotNull(view.GetElement(0, 0));

        view.Grid.TrySet(0, 0, new MapEntry(5, 0, true, false, false));

        Assert.Equal(5 * TileBytes, view.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(MirrorOperation.Horizontal, view.GetElement(0, 0)!.Value.Mirror);
        Assert.Equal(1 * TileBytes, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public void Snes32WidthNotMultipleOf32_Throws()
    {
        Assert.Throws<ArgumentException>(() => Create(_map, _snes, MapScreenLayout.Snes32, 40, 32));
    }

    [Fact]
    public void DirectCodec_Throws()
    {
        var directCodec = CodecFixture.Shared.DirectCodecNames[0];

        Assert.Throws<ArgumentException>(() => new TilemapArranger("tilemap", _map, 0, _gba, MapScreenLayout.Linear, 1, 1, _tiles, 0,
            directCodec, 0, [_slot0], _codecFactory));
    }

    [Fact]
    public void Dependencies_AreMapTileSlotAndTilePaletteEdges()
    {
        _map.Write(Entry16(0));
        using var tilemap = Create(_map, _gba, MapScreenLayout.Linear, 1, 1, 0, _slot0, null, _slot1);
        var tilePalette = new ScatteredArranger("tile palette", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, 8, 8);
        tilemap.TilePaletteArranger = tilePalette;

        Assert.True(tilemap.Dependencies.ToHashSet().SetEquals([
            new(_map, ResourceEdgeKind.Cascade),
            new(_tiles, ResourceEdgeKind.Cascade),
            new(_slot0, ResourceEdgeKind.FallbackPalette),
            new(_slot1, ResourceEdgeKind.FallbackPalette),
            new(tilePalette, ResourceEdgeKind.ClearTilePalette)]));
    }

    internal static void AssertSameElements(IArrangerView expected, IArrangerView actual)
    {
        Assert.Equal(expected.ArrangerElementSize, actual.ArrangerElementSize);

        foreach (var (x, y) in expected.EnumerateElementsWithinElementRange())
        {
            var e = expected.GetElement(x, y);
            var a = actual.GetElement(x, y);

            Assert.Equal(e.HasValue, a.HasValue);
            if (e is not { } el || a is not { } al)
                continue;

            Assert.Same(el.Source, al.Source);
            Assert.Equal(el.SourceAddress, al.SourceAddress);
            Assert.Equal(el.Mirror, al.Mirror);
            Assert.Equal(el.Codec.Name, al.Codec.Name);
            Assert.Same(((IIndexedCodec)el.Codec).Palette, ((IIndexedCodec)al.Codec).Palette);
            Assert.Equal((x * 8, y * 8), (al.X1, al.Y1));
        }
    }
}
