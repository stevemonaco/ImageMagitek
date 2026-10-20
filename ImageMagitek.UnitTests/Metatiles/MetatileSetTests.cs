using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class MetatileSetTests : IDisposable
{
    private static readonly MapFormat _gba = MapFormatTests.LoadShippedFormats().Find("GBA Text BG")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly MemoryDataSource _tiles = new("tiles");
    private readonly MemoryDataSource _table = new("table");
    private readonly MemoryDataSource _attributes = new("attributes");

    public MetatileSetTests()
    {
        _tiles.Write(new byte[32 * 32]);
    }

    public void Dispose()
    {
        _tiles.Dispose();
        _table.Dispose();
        _attributes.Dispose();
    }

    private MetatileSet Create(int count = 3, int width = 2, int height = 2, int layers = 2, MetatileOrder order = MetatileOrder.Interleaved,
        long offset = 0, DataSource? attributes = null, int attributeSize = 4) =>
        new("Set", _table, offset, count, new Size(width, height), layers, _gba, order, _tiles, 0, "GBA 4bpp", 0, null, [], _codecFactory,
            attributes, 0, attributeSize);

    /// <summary>
    /// Writes a table at <paramref name="offset"/> whose n-th entry holds tile index n
    /// </summary>
    private void WriteNumberedTable(int entries, int offset)
    {
        var bytes = new byte[offset + entries * 2];
        for (int i = 0; i < entries; i++)
        {
            bytes[offset + i * 2] = (byte)i;
            bytes[offset + i * 2 + 1] = (byte)(i >> 8);
        }

        _table.Write(bytes);
    }

    [Theory]
    [InlineData(0, 2, 2, 1, 4, "metatile count, not 0")]
    [InlineData(1, 0, 2, 1, 4, "0x2")]
    [InlineData(1, 9, 2, 1, 4, "9x2")]
    [InlineData(1, 2, 9, 1, 4, "2x9")]
    [InlineData(1, 2, 2, 0, 4, "0 layers")]
    [InlineData(1, 2, 2, 5, 4, "5 layers")]
    [InlineData(1, 2, 2, 1, 0, "attribute size 0")]
    [InlineData(1, 2, 2, 1, 9, "attribute size 9")]
    public void Create_ValueOutOfRange_Throws(int count, int width, int height, int layers, int attributeSize, string expected)
    {
        var ex = Assert.Throws<ArgumentException>(() => Create(count, width, height, layers, attributes: _attributes, attributeSize: attributeSize));

        Assert.Contains("Metatile set 'Set'", ex.Message);
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Interleaved_EntryAddress()
    {
        const int offset = 0x10;
        WriteNumberedTable(3 * 2 * 4, offset);
        var set = Create(offset: offset);
        var table = set.ReadTable();

        foreach (var (m, l, x, y) in Positions(3, 2))
        {
            var expected = (m * 2 + l) * 4 + y * 2 + x;
            Assert.True(set.TryDecodeEntry(table, m, l, x, y, out var entry));
            Assert.Equal(expected, entry.TileIndex);
        }
    }

    [Fact]
    public void Planar_EntryAddress()
    {
        const int offset = 0x10;
        WriteNumberedTable(3 * 2 * 4, offset);
        var set = Create(order: MetatileOrder.Planar, offset: offset);
        var table = set.ReadTable();

        foreach (var (m, l, x, y) in Positions(3, 2))
        {
            var expected = (l * 4 + y * 2 + x) * 3 + m;
            Assert.True(set.TryDecodeEntry(table, m, l, x, y, out var entry));
            Assert.Equal(expected, entry.TileIndex);
        }
    }

    [Fact]
    public void EntryPastSource_IsEmpty()
    {
        WriteNumberedTable(8, 0);
        var set = Create(count: 2);
        var table = set.ReadTable();

        Assert.True(set.TryDecodeEntry(table, 0, 1, 1, 1, out _));
        Assert.False(set.TryDecodeEntry(table, 1, 0, 0, 0, out _));
    }

    [Fact]
    public void Attribute_ReadsLittleEndian()
    {
        _attributes.Write([0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88]);

        Assert.Equal(0x88776655UL, Create(attributes: _attributes, attributeSize: 4).ReadAttribute(1));
        Assert.Equal(0x665544UL, Create(attributes: _attributes, attributeSize: 3).ReadAttribute(1));
    }

    [Fact]
    public void Attribute_PastSource_IsNone()
    {
        _attributes.Write([0x11, 0x22, 0x33, 0x44, 0x55]);
        var set = Create(attributes: _attributes, attributeSize: 4);

        Assert.Equal(0x44332211UL, set.ReadAttribute(0));
        Assert.Null(set.ReadAttribute(1));
        Assert.Null(Create().ReadAttribute(0));
    }

    private static (int M, int L, int X, int Y)[] Positions(int count, int layers) =>
        (from m in Enumerable.Range(0, count)
         from l in Enumerable.Range(0, layers)
         from y in Enumerable.Range(0, 2)
         from x in Enumerable.Range(0, 2)
         select (m, l, x, y)).ToArray();
}
