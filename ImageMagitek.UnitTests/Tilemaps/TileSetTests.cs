using System;
using System.IO;
using ImageMagitek.Codec;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

/// <summary>
/// A GBA 4bpp tile set of 10 tiles after a 16-byte header, numbered from 640 as FireRed's secondary tiles are
/// </summary>
public sealed class TileSetTests : IDisposable
{
    private const int Offset = 16;
    private const int TileBytes = 32;
    private const int First = 640;

    private static readonly MapFormat _gba = MapFormatTests.LoadShippedFormats().Find("GBA Text BG")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly MemoryDataSource _rom = new("rom");

    public TileSetTests() => _rom.Write(new byte[Offset + 10 * TileBytes]);

    public void Dispose() => _rom.Dispose();

    private TileSet Create(DataSource? source = null, long offset = Offset, string codecName = "GBA 4bpp", int first = First, int? declared = null) =>
        new(source ?? _rom, offset, codecName, first, declared, _codecFactory);

    [Fact]
    public void TileCount_CappedBySourceAndDeclaredCount()
    {
        using var missing = new FileDataSource("missing", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin"));

        Assert.Equal(10, Create().TileCount);
        Assert.Equal(4, Create(declared: 4).TileCount);
        Assert.Equal(10, Create(declared: 50).TileCount);
        Assert.Equal(9, Create(offset: Offset + 1).TileCount);
        Assert.Equal(0, Create(offset: 10_000).TileCount);
        Assert.Equal(0, Create(missing).TileCount);
    }

    [Fact]
    public void Contains_HoldsRangeFromFirstTile()
    {
        var tileSet = Create();

        Assert.False(tileSet.Contains(639));
        Assert.True(tileSet.Contains(640));
        Assert.True(tileSet.Contains(649));
        Assert.False(tileSet.Contains(650));
    }

    [Fact]
    public void TileAddress_CountsFromFirstTile()
    {
        var tileSet = Create();
        var codec = tileSet.CreateCodec(null);
        var element = tileSet.CreateTileElement(643, codec, MirrorOperation.Horizontal, 2, 1);

        Assert.Equal(new BitAddress(Offset, 0), tileSet.TileAddress(640));
        Assert.Equal(new BitAddress(Offset + 3 * TileBytes, 0), tileSet.TileAddress(643));
        Assert.Equal(new BitAddress(Offset + 3 * TileBytes, 0), element.SourceAddress);
        Assert.Equal((16, 8), (element.X1, element.Y1));
        Assert.Equal(MirrorOperation.Horizontal, element.Mirror);
        Assert.Same(_rom, element.Source);
    }

    [Fact]
    public void FindTileIndex_TileOfSet_ReturnsIndexFromFirstTile()
    {
        var tileSet = Create();
        using var other = new MemoryDataSource("other");
        other.Write(new byte[Offset + 10 * TileBytes]);
        var codec = tileSet.CreateCodec(null);

        var found = tileSet.FindTileIndex(tileSet.CreateTileElement(645, codec, MirrorOperation.None, 0, 0), _gba);
        var foreign = tileSet.FindTileIndex(new ArrangerElement(0, 0, other, new BitAddress(Offset, 0), codec, MirrorOperation.None, RotationOperation.None), _gba);

        Assert.Equal(645, found.AsSuccess.Result);
        Assert.Equal("reads data file 'other', not the tile set's data file 'rom'", foreign.AsError.Reason);
    }

    [Fact]
    public void Create_UnknownOrDirectCodecOrNonPositiveCount_Throws()
    {
        Assert.Equal("uses unknown codec 'No Such Codec'", Assert.Throws<ArgumentException>(() => Create(codecName: "No Such Codec")).Message);
        Assert.Equal("uses direct codec 'Rgba32 Tiled'; tile codecs must be indexed", Assert.Throws<ArgumentException>(() => Create(codecName: "Rgba32 Tiled")).Message);
        Assert.Equal("must declare a positive tile count, not 0", Assert.Throws<ArgumentException>(() => Create(declared: 0)).Message);
    }
}
