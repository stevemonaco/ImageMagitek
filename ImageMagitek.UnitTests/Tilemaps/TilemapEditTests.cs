using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

/// <summary>
/// Writes to a tilemap whose map and tiles share one data source, with sentinel bytes before and after the map
/// </summary>
public sealed class TilemapEditTests : IDisposable
{
    private const int MapOffset = 16;
    private const int Width = 4;
    private const int Height = 2;
    private const int MapBytes = Width * Height * 2;
    private const int TileOffset = MapOffset + MapBytes + 16;
    private const int TileCount = 16;
    private const int TileBytes = 32;

    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly MapFormat _gba = _formats.Find("GBA Text BG")!;
    private static readonly MapFormat _snes = _formats.Find("SNES BG")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly Palette _slot0 = CreatePalette(10);
    private readonly Palette _slot1 = CreatePalette(40);
    private readonly Palette _unslotted = CreatePalette(80);
    private readonly MemoryDataSource _rom = new("rom");

    public void Dispose() => _rom.Dispose();

    private static Palette CreatePalette(byte seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(seed + i), 0, 0, 255)).ToArray());

    private static byte[] Gba(int tile, int palette = 0, bool hflip = false, bool vflip = false)
    {
        var value = tile | palette << 12 | (hflip ? 1 << 10 : 0) | (vflip ? 1 << 11 : 0);
        return [(byte)value, (byte)(value >> 8)];
    }

    private static byte[] Snes(int tile, int palette = 0, bool priority = false, bool hflip = false)
    {
        var value = tile | palette << 10 | (priority ? 1 << 13 : 0) | (hflip ? 1 << 14 : 0);
        return [(byte)value, (byte)(value >> 8)];
    }

    private void WriteRom(params byte[][] entries)
    {
        var map = entries.SelectMany(x => x).ToArray();
        Assert.Equal(MapBytes, map.Length);
        var tiles = Enumerable.Range(0, TileCount * TileBytes).Select(x => (byte)(x * 7)).ToArray();
        _rom.Write([.. Enumerable.Repeat((byte)0xAA, MapOffset), .. map, .. Enumerable.Repeat((byte)0xBB, 16), .. tiles]);
    }

    private byte[] ReadRom() => _rom.Read(BitAddress.Zero, (int)_rom.Length * 8);

    private TilemapArranger Create(MapFormat format, int emptyTile = 0, int firstTile = 0) =>
        new("tilemap", _rom, MapOffset, format, MapScreenLayout.Linear, Width, Height, _rom, TileOffset, "GBA 4bpp", firstTile,
            [_slot0, _slot1], _codecFactory, emptyTile);

    private static byte[] WithEntry(byte[] rom, int x, int y, byte[] entry)
    {
        var expected = (byte[])rom.Clone();
        entry.CopyTo(expected, MapOffset + (y * Width + x) * 2);
        return expected;
    }

    private ArrangerElement TileElement(int tile, Palette palette, DataSource? source = null, string codecName = "GBA 4bpp")
    {
        var codec = (IIndexedCodec)_codecFactory.CreateCodec(codecName)!;
        codec.Palette = palette;
        return new ArrangerElement(0, 0, source ?? _rom, new BitAddress(TileOffset + tile * TileBytes, 0), codec);
    }

    private void WriteDefaultGbaMap() =>
        WriteRom(Gba(1), Gba(2), Gba(3), Gba(4), Gba(5), Gba(6), Gba(7), Gba(8));

    [Fact]
    public void WriteEntries_MovedCell_ChangesOnlyTargetEntryBytes()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.SetElement(working.GetElement(0, 0), 3, 1);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 3, 1, Gba(1)), ReadRom());
        Assert.Equal(TileOffset + TileBytes, tilemap.GetElement(3, 1)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public void WriteEntries_PastedTile_ChangesOnlyTargetEntry()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        var cell = tilemap.ToCellElement(TileElement(9, _slot1), working.GetElement(2, 0));
        working.SetElement(cell.AsSuccess.Result, 2, 0);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 2, 0, Gba(9, palette: 1)), ReadRom());
    }

    [Fact]
    public void WriteEntries_AppliedPalette_WritesMatchingSlot_KeepsPriority()
    {
        WriteRom(Snes(1, priority: true), Snes(2), Snes(3), Snes(4), Snes(5), Snes(6), Snes(7), Snes(8));
        using var tilemap = Create(_snes);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.SetElement(TileElement(1, _slot1), 0, 0);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 0, 0, Snes(1, palette: 1, priority: true)), ReadRom());
    }

    [Fact]
    public void WriteEntries_Mirror_WritesFlipBitsOnly()
    {
        WriteRom(Snes(1, priority: true), Snes(2), Snes(3), Snes(4), Snes(5), Snes(6), Snes(7), Snes(8));
        using var tilemap = Create(_snes);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.TryMirrorElement(0, 0, MirrorOperation.Horizontal);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 0, 0, Snes(1, priority: true, hflip: true)), ReadRom());
    }

    [Fact]
    public void WriteEntries_DeletedCell_WritesConfiguredEmptyEntry()
    {
        WriteRom(Gba(1), Gba(2), Gba(3), Gba(4), Gba(5, palette: 1, hflip: true), Gba(6), Gba(7), Gba(8));
        using var tilemap = Create(_gba, emptyTile: 15);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.ResetElement(0, 1);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 0, 1, Gba(15)), ReadRom());
    }

    [Fact]
    public void WriteEntries_UnchangedOutOfRangeCell_IsNotRewritten()
    {
        WriteRom(Gba(1), Gba(500, palette: 3), Gba(3), Gba(4), Gba(5), Gba(6), Gba(7), Gba(8));
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var written = 0;
        _rom.DataWritten += (_, _) => written++;

        Assert.Null(tilemap.GetElement(1, 0));
        Assert.True(tilemap.WriteEntries(tilemap.CloneArranger()).HasSucceeded);
        Assert.Equal(before, ReadRom());
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteEntries_PaletteInNoSlot_FailsAndWritesNothing()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.SetElement(working.GetElement(0, 0), 3, 1);
        working.SetElement(TileElement(2, _unslotted), 1, 1);

        var result = tilemap.WriteEntries(working);

        Assert.True(result.HasFailed);
        Assert.Contains("(1, 1)", result.AsError.Reason);
        Assert.Equal(before, ReadRom());
    }

    [Fact]
    public void CanWriteEntries_RefusedCell_FailsAndWritesNothing()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CloneArranger();

        working.SetElement(TileElement(2, _unslotted), 1, 1);

        var result = tilemap.CanWriteEntries(working);

        Assert.True(result.HasFailed);
        Assert.Contains("(1, 1)", result.AsError.Reason);
        Assert.Equal(before, ReadRom());
    }

    [Fact]
    public void WriteEntries_NoChanges_DoesNotRaiseDataWritten()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var written = 0;
        _rom.DataWritten += (_, _) => written++;

        Assert.True(tilemap.WriteEntries(tilemap.CloneArranger()).HasSucceeded);
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteEntries_SeveralChanges_RaisesDataWrittenOnce()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        var working = tilemap.CloneArranger();
        var written = 0;
        _rom.DataWritten += (_, _) => written++;

        working.ResetElement(0, 0);
        working.TryMirrorElement(1, 0, MirrorOperation.Vertical);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(1, written);
    }

    [Fact]
    public void FindTileIndex_OtherSource_Refuses()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);
        using var other = new MemoryDataSource("other");
        other.Write(new byte[TileOffset + TileCount * TileBytes]);

        var result = tilemap.FindTileIndex(TileElement(1, _slot0, other));

        Assert.Equal("reads data file 'other', not the tile set's data file 'rom'", result.AsError.Reason);
    }

    [Fact]
    public void FindTileIndex_OtherCodec_Refuses()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);

        var result = tilemap.FindTileIndex(TileElement(1, _slot0, codecName: "SNES 4bpp"));

        Assert.Equal("uses codec 'SNES 4bpp', not the tile set's codec 'GBA 4bpp'", result.AsError.Reason);
    }

    [Fact]
    public void FindTileIndex_Rotated_Refuses()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);

        var result = tilemap.FindTileIndex(TileElement(1, _slot0).WithRotation(RotationOperation.Left));

        Assert.Equal("is rotated; tilemap entries cannot be rotated", result.AsError.Reason);
    }

    [Theory]
    [InlineData(MirrorOperation.Horizontal, "is mirrored horizontally; map format 'No Flip' has no horizontal flip field")]
    [InlineData(MirrorOperation.Vertical, "is mirrored vertically; map format 'No Flip' has no vertical flip field")]
    public void FindTileIndex_MirroredWithoutFlipField_Refuses(MirrorOperation mirror, string expected)
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(new MapFormat("No Flip", 16, Endian.Little, new MapField(0, 10)));

        var result = tilemap.FindTileIndex(TileElement(1, _slot0).WithMirror(mirror));

        Assert.Equal(expected, result.AsError.Reason);
    }

    [Fact]
    public void FindTileIndex_UnalignedAddress_Refuses()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);

        var result = tilemap.FindTileIndex(TileElement(1, _slot0).WithAddress(new BitAddress(TileOffset + TileBytes + 1, 0)));

        Assert.Contains("not on a tile boundary", result.AsError.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TileOffset + TileCount * TileBytes)]
    public void FindTileIndex_OutsideTileSet_Refuses(int byteOffset)
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);

        var result = tilemap.FindTileIndex(TileElement(1, _slot0).WithAddress(new BitAddress(byteOffset, 0)));

        Assert.Contains("outside the tile set", result.AsError.Reason);
    }

    [Fact]
    public void FindTileIndex_IndexWiderThanField_Refuses()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba, firstTile: 1020);

        var result = tilemap.FindTileIndex(TileElement(4, _slot0));

        Assert.Equal("is tile 1024, which does not fit the 10-bit tile field of map format 'GBA Text BG'", result.AsError.Reason);
    }

    [Fact]
    public void FindTileIndex_TileOfTileSet_ReturnsIndexFromFirstTile()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba, firstTile: 2);

        Assert.Equal(7, tilemap.FindTileIndex(TileElement(5, _slot0)).AsSuccess.Result);
    }

    [Fact]
    public void ToCellElement_PaletteInNoSlot_UsesDestinationSlot()
    {
        WriteRom(Gba(1), Gba(2, palette: 1), Gba(3), Gba(4), Gba(5), Gba(6), Gba(7), Gba(8));
        using var tilemap = Create(_gba);
        var working = tilemap.CloneArranger();

        var intoSlot1 = tilemap.ToCellElement(TileElement(9, _unslotted), working.GetElement(1, 0)).AsSuccess.Result;
        var intoEmpty = tilemap.ToCellElement(TileElement(9, _unslotted), null).AsSuccess.Result;
        var slotted = TileElement(9, _slot1);

        Assert.Same(_slot1, ((IIndexedCodec)intoSlot1.Codec).Palette);
        Assert.Same(_slot0, ((IIndexedCodec)intoEmpty.Codec).Palette);
        Assert.Same(slotted.Codec, tilemap.ToCellElement(slotted, null).AsSuccess.Result.Codec);
    }

    [Fact]
    public void ToCellElement_ForeignElement_RefusesWithReason()
    {
        WriteDefaultGbaMap();
        using var tilemap = Create(_gba);

        var result = tilemap.ToCellElement(TileElement(1, _slot0, codecName: "SNES 4bpp"), null);

        Assert.Contains("not the tile set's codec", result.AsError.Reason);
    }

    [Fact]
    public void FindPaletteSlot_PrefersGivenSlotThenLowestAssignedThenEffectiveSlotZero()
    {
        WriteDefaultGbaMap();
        using var tilemap = new TilemapArranger("tilemap", _rom, MapOffset, _gba, MapScreenLayout.Linear, Width, Height, _rom, TileOffset,
            "GBA 4bpp", 0, [_slot0, _slot1, null, _slot1], _codecFactory);

        Assert.Equal(3, tilemap.FindPaletteSlot(_slot1, 3));
        Assert.Equal(1, tilemap.FindPaletteSlot(_slot1, 0));
        Assert.Equal(2, tilemap.FindPaletteSlot(_slot0, 2));
        Assert.Equal(0, tilemap.FindPaletteSlot(_slot0, 1));
        Assert.Null(tilemap.FindPaletteSlot(_unslotted, 0));
    }

    [Fact]
    public void EmptyTileWiderThanField_Throws()
    {
        WriteDefaultGbaMap();

        Assert.Throws<ArgumentException>(() => Create(_gba, emptyTile: 1024));
    }

    [Fact]
    public void DrawnCell_SharedTile_ChangesEveryCellAfterSave()
    {
        WriteRom(Gba(1), Gba(1, hflip: true), Gba(1), Gba(2), Gba(3), Gba(4), Gba(5), Gba(6));
        using var tilemap = Create(_gba);
        var working = tilemap.CloneArranger();
        var current = new IndexedImage(working);
        var result = new IndexedImage(working);
        var drawn = (byte)((current.GetPixel(0, 0) + 1) % 16);

        result.SetPixel(0, 0, drawn);
        var conflicts = working.ResolveSharedTiles(current, result, changedCellsWin: true);
        result.SaveImage();

        Assert.Empty(conflicts);
        var saved = new IndexedImage(tilemap);
        Assert.Equal(drawn, saved.GetPixel(0, 0));
        Assert.Equal(drawn, saved.GetPixel(15, 0));
        Assert.Equal(drawn, saved.GetPixel(16, 0));
    }

    [Fact]
    public void DrawnCellsDisagree_IsConflict_TileUnchanged()
    {
        WriteRom(Gba(1), Gba(1, hflip: true), Gba(1), Gba(2), Gba(3), Gba(4), Gba(5), Gba(6));
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CloneArranger();
        var current = new IndexedImage(working);
        var result = new IndexedImage(working);

        result.SetPixel(0, 0, (byte)((current.GetPixel(0, 0) + 1) % 16));
        result.SetPixel(16, 0, (byte)((current.GetPixel(16, 0) + 2) % 16));
        var conflicts = working.ResolveSharedTiles(current, result, changedCellsWin: true);
        result.SaveImage();

        var conflict = Assert.Single(conflicts);
        Assert.Equal(new BitAddress(TileOffset + TileBytes, 0), conflict.Address);
        Assert.Equal(new Point[] { new(0, 0), new(1, 0), new(2, 0) }, conflict.Cells);
        Assert.Equal(before, ReadRom());
    }

    private DecodedImage RenderColors(Arranger arranger, Func<int, int, byte, byte>? edit = null)
    {
        var image = new IndexedImage(arranger);
        var pixels = new ColorRgba32[image.Width * image.Height];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var index = image.GetPixel(x, y);
                pixels[y * image.Width + x] = _slot0[edit?.Invoke(x, y, index) ?? index];
            }
        }

        return new DecodedImage(pixels, image.Width, image.Height);
    }

    [Fact]
    public void ImportIntoTilemap_ConsistentTiles_WritesThem()
    {
        WriteRom(Gba(1), Gba(1, hflip: true), Gba(2), Gba(3), Gba(4), Gba(5), Gba(6), Gba(7));
        using var tilemap = Create(_gba);
        var source = RenderColors(tilemap, (x, y, index) => (x is 0 or 15) && y == 0 ? (byte)((index + 1) % 16) : index);

        var preview = ImageImporter.Prepare(tilemap, source, ImageImportOptions.Default).AsSuccess.Result;
        preview.Commit();

        Assert.Empty(preview.Report.SharedTileConflicts);
        Assert.Equal(source.Pixels, RenderColors(tilemap).Pixels);
    }

    [Fact]
    public void ImportIntoTilemap_SharedTileDiffers_ReportsConflictAndKeepsTile()
    {
        WriteRom(Gba(1), Gba(1), Gba(2), Gba(3), Gba(4), Gba(5), Gba(6), Gba(7));
        using var tilemap = Create(_gba);
        var original = RenderColors(tilemap);
        var source = RenderColors(tilemap, (x, y, index) => (x == 0 || x == 16) && y == 0 ? (byte)((index + 1) % 16) : index);

        var preview = ImageImporter.Prepare(tilemap, source, ImageImportOptions.Default).AsSuccess.Result;
        preview.Commit();

        var conflict = Assert.Single(preview.Report.SharedTileConflicts);
        Assert.Equal(new Point[] { new(0, 0), new(1, 0) }, conflict.Cells);
        Assert.Equal(ImportPixelState.SharedTileConflict, preview.Report.PixelStates[0]);
        Assert.Equal(ImportPixelState.SharedTileConflict, preview.Report.PixelStates[8]);
        Assert.Equal(ImportPixelState.Changed, preview.Report.PixelStates[16]);
        Assert.Contains("1 shared tile differs between cells and was not imported", preview.Report.ToSummary());
        Assert.True(preview.CanCommit);

        var imported = RenderColors(tilemap);
        Assert.Equal(original.Pixels[0], imported.Pixels[0]);
        Assert.Equal(source.Pixels[16], imported.Pixels[16]);
    }
}
