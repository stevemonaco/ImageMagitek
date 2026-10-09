using System.Threading.Tasks;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

public class DataSourceBitAddressTests
{
    private static readonly byte[] _payload = [0b10110011, 0b01011100, 0b11100000];
    private const int _payloadBits = 19;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void Write_ThenRead_AtBitOffset_RoundTripsAndPreservesNeighbors(int bitOffset)
    {
        var source = CreateSentinelSource(out var before);
        var address = new BitAddress(2, bitOffset);

        source.Write(address, _payloadBits, _payload);
        var actual = source.Read(address, _payloadBits);

        Assert.Equal(_payload, actual);
        BitAssert.EqualOutside(before, source.Read(BitAddress.Zero, before.Length * 8), (int)address.Offset, _payloadBits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task WriteAsync_ThenReadAsync_AtBitOffset_RoundTripsAndPreservesNeighbors(int bitOffset)
    {
        var source = CreateSentinelSource(out var before);
        var address = new BitAddress(2, bitOffset);

        await source.WriteAsync(address, _payloadBits, _payload);
        var actual = await source.ReadAsync(address, _payloadBits);

        Assert.Equal(_payload, actual);
        BitAssert.EqualOutside(before, source.Read(BitAddress.Zero, before.Length * 8), (int)address.Offset, _payloadBits);
    }

    [Fact]
    public void Read_IntoOversizedBuffer_LeavesExtraBytesUntouched()
    {
        var source = CreateSentinelSource(out _);
        var buffer = new byte[] { 0, 0, 0, 0xAA, 0xAA };

        source.Read(new BitAddress(1, 5), _payloadBits, buffer);

        Assert.Equal(0xAA, buffer[3]);
        Assert.Equal(0xAA, buffer[4]);
        Assert.Equal(0, buffer[2] & 0b00011111);
    }

    [Fact]
    public void Equals_NonBitAddress_ReturnsFalse()
    {
        var address = new BitAddress(0, 0);

        Assert.False(address.Equals("0"));
        Assert.False(address.Equals((object)0));
        Assert.False(address.Equals(null));
    }

    private static MemoryDataSource CreateSentinelSource(out byte[] before)
    {
        before = TestImageGenerator.RandomBytes(8, 91);
        var source = new MemoryDataSource("test", before.Length);
        source.Write(BitAddress.Zero, before);
        return source;
    }
}
