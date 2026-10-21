using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class AddressMapperTests
{
    [Theory]
    [InlineData(0x08000000L, 0L)]
    [InlineData(0x083526A8L, 0x3526A8L)]
    [InlineData(0x09FFFFFFL, 0x1FFFFFFL)]
    public void Gba_RoundTrips(long pointer, long offset)
    {
        var mapper = new GbaAddressMapper();

        Assert.True(mapper.TryMap(pointer, out var mapped));
        Assert.Equal(offset, mapped);
        Assert.Equal(pointer, mapper.ToPointer(mapped));
    }

    [Theory]
    [InlineData(0x02000000L)]
    [InlineData(0x07FFFFFFL)]
    [InlineData(0x0A000000L)]
    public void Gba_OutsideRom_IsUnmapped(long pointer)
    {
        Assert.False(new GbaAddressMapper().TryMap(pointer, out _));
    }

    [Fact]
    public void Gba_NullPointer()
    {
        Assert.False(new GbaAddressMapper().TryMap(0, out _));
        Assert.False(new LinearAddressMapper(0).TryMap(0, out _));
    }

    [Theory]
    [InlineData(0L, 0x10L, 0x10L)]
    [InlineData(0x8000L, 0x8010L, 0x10L)]
    public void Linear_RoundTripsWithBase(long baseAddress, long pointer, long offset)
    {
        var mapper = new LinearAddressMapper(baseAddress);

        Assert.True(mapper.TryMap(pointer, out var mapped));
        Assert.Equal(offset, mapped);
        Assert.Equal(pointer, mapper.ToPointer(mapped));
        Assert.False(mapper.TryMap(baseAddress - 1, out _));
    }

    [Theory]
    [InlineData("snes-lorom")]
    [InlineData("snes-hirom")]
    [InlineData("nes")]
    [InlineData("gb")]
    public void PendingMapper_FailsLoad(string mapper)
    {
        using var context = new TableTestContext();

        var result = context.LoadXml($"""
            <tabledefinition name="t">
            	<addressspace mapper="{mapper}" />
            	<record name="R" size="1"><field name="a" offset="0" bits="8" /></record>
            </tabledefinition>
            """);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains(mapper) && x.Contains("not implemented yet"));
    }
}
