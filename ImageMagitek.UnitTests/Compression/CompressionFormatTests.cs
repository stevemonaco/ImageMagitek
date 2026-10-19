using System.IO;
using ImageMagitek.Compression;
using Xunit;

namespace ImageMagitek.UnitTests.Compression;

public class CompressionFormatTests
{
    private readonly GbaLz77Compression _lz77 = new();
    private readonly GbaRleCompression _rle = new();

    [Fact]
    public void Lz77_Literals_Decompress()
    {
        byte[] input = [0x10, 0x04, 0x00, 0x00, 0x00, 0x41, 0x42, 0x43, 0x44];

        var result = _lz77.Decompress(input);

        Assert.Equal("ABCD"u8.ToArray(), result.Data);
    }

    [Fact]
    public void Lz77_BackReference_Decompresses()
    {
        // 'A', 'B', then a reference of length 6 at distance 2
        byte[] input = [0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01];

        var result = _lz77.Decompress(input);

        Assert.Equal("ABABABAB"u8.ToArray(), result.Data);
    }

    [Fact]
    public void Lz77_ReportsConsumedLength()
    {
        byte[] input = [0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01, 0xEE, 0xEE];

        var result = _lz77.Decompress(input);

        Assert.Equal(9, result.ConsumedLength);
    }

    [Fact]
    public void Lz77_WrongHeader_Throws()
    {
        Assert.Throws<InvalidDataException>(() => _lz77.Decompress([0x11, 0x04, 0x00, 0x00, 0x00, 1, 2, 3, 4]));
    }

    [Fact]
    public void Lz77_DistanceBeforeStart_Throws()
    {
        byte[] input = [0x10, 0x08, 0x00, 0x00, 0x40, 0x41, 0x30, 0x05];

        Assert.Throws<InvalidDataException>(() => _lz77.Decompress(input));
    }

    [Fact]
    public void Lz77_TruncatedInput_Throws()
    {
        Assert.Throws<InvalidDataException>(() => _lz77.Decompress([0x10, 0x08, 0x00, 0x00, 0x00, 0x41, 0x42]));
    }

    [Fact]
    public void Rle_RunsAndLiterals_Decompress()
    {
        // A run of five 'A', then one literal 'B'
        byte[] input = [0x30, 0x06, 0x00, 0x00, 0x82, 0x41, 0x00, 0x42];

        var result = _rle.Decompress(input);

        Assert.Equal("AAAAAB"u8.ToArray(), result.Data);
        Assert.Equal(8, result.ConsumedLength);
    }

    [Fact]
    public void Rle_TruncatedInput_Throws()
    {
        Assert.Throws<InvalidDataException>(() => _rle.Decompress([0x30, 0x06, 0x00, 0x00, 0x82]));
    }
}
