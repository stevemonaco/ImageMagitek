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
    [InlineData(2, 0, 4, new byte[] { 0b1010_0000 }, new byte[] { 0xAA, 0xBB, 0b1010_0000 })]
    [InlineData(2, 3, 2, new byte[] { 0b1100_0000 }, new byte[] { 0xAA, 0xBB, 0b0001_1000 })]
    [InlineData(1, 4, 10, new byte[] { 0b1100_1101, 0b1100_0000 }, new byte[] { 0xAA, 0xBC, 0b1101_1100 })]
    public async Task Write_PartialBytePastEnd_KeepsExistingBytesAndZeroFillsTheRest(int byteOffset, int bitOffset, int bits, byte[] payload, byte[] expected)
    {
        var sync = new MemoryDataSource("sync");
        sync.Write(BitAddress.Zero, [0xAA, 0xBB]);
        var async = new MemoryDataSource("async");
        async.Write(BitAddress.Zero, [0xAA, 0xBB]);
        var address = new BitAddress(byteOffset, bitOffset);

        sync.Write(address, bits, payload);
        await async.WriteAsync(address, bits, payload);

        Assert.Equal(expected, sync.Read(BitAddress.Zero, (int)sync.Length * 8));
        Assert.Equal(expected, async.Read(BitAddress.Zero, (int)async.Length * 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Read_ZeroBits_ReturnsEmpty(int bitOffset)
    {
        var source = CreateSentinelSource(out _);
        var address = new BitAddress(2, bitOffset);

        Assert.Empty(source.Read(address, 0));
        Assert.Empty(await source.ReadAsync(address, 0));
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
