using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Compression;

public class CompressedDataSourceTests
{
    // At offset 4: a GBA LZ77 block decompressing to "ABABABAB"
    private static readonly byte[] _parentBytes = [0xEE, 0xEE, 0xEE, 0xEE, 0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01, 0xEE];

    private readonly CompressorFactory _factory = new();

    private static MemoryDataSource CreateParent(byte[] bytes)
    {
        var parent = new MemoryDataSource("parent");
        parent.Write(bytes);
        return parent;
    }

    [Fact]
    public void Read_ReturnsDecompressedBytes()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", _factory);

        var bytes = source.Read(new BitAddress(0, 0), 8 * 8);

        Assert.Equal("ABABABAB"u8.ToArray(), bytes);
        Assert.Equal(8, source.Length);
        Assert.Equal(9, source.CompressedLength);
        Assert.Null(source.DecompressionError);
    }

    [Fact]
    public void Write_Throws_ParentUnchanged()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", _factory);

        Assert.True(source.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => source.Write(new BitAddress(0, 0), [1, 2]));
        Assert.Equal(_parentBytes, parent.Read(new BitAddress(0, 0), _parentBytes.Length * 8));
    }

    [Fact]
    public void UnknownCompressor_HasErrorAndZeroLength()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "Missing Plugin", _factory);

        Assert.Contains("Missing Plugin", source.DecompressionError);
        Assert.Equal(0, source.Length);
    }

    [Fact]
    public void MalformedBlock_HasErrorAndZeroLength()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 0, "GBA LZ77", _factory);

        Assert.Contains("0x0", source.DecompressionError);
        Assert.Equal(0, source.Length);
    }

    [Fact]
    public void ParentWritten_Redecompresses()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", _factory);
        Assert.Equal(8, source.Length);
        var raised = false;
        source.DataWritten += (_, _) => raised = true;

        parent.Write(new BitAddress(10, 0), [0x43]);
        parent.NotifyDataWritten();

        Assert.True(raised);
        Assert.Equal("ACACACAC"u8.ToArray(), source.Read(new BitAddress(0, 0), 8 * 8));
    }

    [Fact]
    public void Invalidate_RetriesAfterParentFixed()
    {
        using var parent = CreateParent([0x10, 0x04, 0x00, 0x00, 0x00, 0x41]);
        using var source = new CompressedDataSource("compressed", parent, 0, "GBA LZ77", _factory);
        Assert.NotNull(source.DecompressionError);

        parent.Write(new BitAddress(6, 0), [0x42, 0x43, 0x44]);
        source.Invalidate();

        Assert.Null(source.DecompressionError);
        Assert.Equal("ABCD"u8.ToArray(), source.Read(new BitAddress(0, 0), 4 * 8));
    }

    [Fact]
    public void Arranger_OverCompressedSource_IsReadOnly()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", _factory);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => CodecFixture.Shared.CodecFactory.CreateCodec("NES 1bpp")!, source);

        Assert.Equal("reads compressed data 'compressed', which is read-only", arranger.GetReadOnlyReason());
    }

    [Fact]
    public void Palette_OverCompressedSource_IsReadOnly()
    {
        using var parent = CreateParent(_parentBytes);
        using var source = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", _factory);
        var palette = new Palette("pal", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false,
            PaletteStorageSource.ProjectXml, source);
        palette.SetNativeColor(0, new ColorRgba32(248, 248, 248, 255));

        Assert.True(palette.IsReadOnly);
        Assert.False(palette.SavePalette());
        Assert.Equal(_parentBytes, parent.Read(new BitAddress(0, 0), _parentBytes.Length * 8));
    }
}
