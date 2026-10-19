using System.IO;
using ImageMagitek.Compression;
using ImageMagitek.PluginSamples;
using Xunit;

namespace ImageMagitek.UnitTests.Compression;

public class SampleCompressionPluginTests
{
    private readonly SquareLzssCompression _lzss = new();

    [Fact]
    public void SquareLzss_LiteralsAndBufferReference_Decompress()
    {
        // 'A' and 'B' land at 0x7DE and 0x7DF; a length-4 reference to 0x7DE repeats them while it writes
        byte[] input = [0x07, 0x00, 0x03, 0x41, 0x42, 0xDE, 0xE1, 0xEE];

        var result = _lzss.Decompress(input);

        Assert.Equal("ABABAB"u8.ToArray(), result.Data);
        Assert.Equal(7, result.ConsumedLength);
    }

    [Fact]
    public void SquareLzss_ReferenceToUnwrittenBuffer_ReadsZeros()
    {
        byte[] input = [0x05, 0x00, 0x00, 0x00, 0x00];

        var result = _lzss.Decompress(input);

        Assert.Equal(new byte[3], result.Data);
    }

    [Fact]
    public void SquareLzss_LengthPastInput_Throws()
    {
        Assert.Throws<InvalidDataException>(() => _lzss.Decompress([0x40, 0x00, 0xFF, 0x41]));
    }

    [Fact]
    public void SquareLzss_RegistersWithCompressorFactory()
    {
        var factory = new CompressorFactory();

        var result = factory.AddCompressionPlugin(typeof(SquareLzssCompression));

        Assert.Equal("Square LZSS", result.AsSuccess.Result);
    }
}
