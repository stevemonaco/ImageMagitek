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
    [InlineData(0x008000L, 0L)]
    [InlineData(0x0FCA00L, 0x7CA00L)]
    [InlineData(0x1CEE00L, 0xE6E00L)]
    [InlineData(0x0E9800L, 0x71800L)]
    public void SnesLoRom_RoundTrips(long pointer, long offset)
    {
        var mapper = new SnesLoRomAddressMapper(0);

        Assert.True(mapper.TryMap(pointer, out var mapped));
        Assert.Equal(offset, mapped);
        Assert.Equal(pointer, mapper.ToPointer(mapped));
        Assert.True(mapper.TryMap(pointer | 0x800000, out var mirrored));
        Assert.Equal(offset, mirrored);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x0F7FFFL)]
    [InlineData(0x0E0000L)]
    [InlineData(0x7E8000L)]
    [InlineData(0x7FFFFFL)]
    [InlineData(0x1000000L)]
    public void SnesLoRom_LowHalfAndWram_Unmapped(long pointer)
    {
        Assert.False(new SnesLoRomAddressMapper(0).TryMap(pointer, out _));
    }

    [Theory]
    [InlineData(0x100200L, 0x200L)]
    [InlineData(0x100000L, 0L)]
    [InlineData(0x80200L, 0x200L)]
    public void SnesLoRom_HeaderDetectedFromLength(long length, long header)
    {
        var mapper = Assert.IsType<SnesLoRomAddressMapper>(new SnesLoRomAddressMapper(null).ForDataLength(length));

        Assert.Equal(header, mapper.Header);
        Assert.True(mapper.TryMap(0x0FCA00, out var offset));
        Assert.Equal(0x7CA00 + header, offset);
        Assert.Equal(0x0FCA00, mapper.ToPointer(offset));
    }

    [Fact]
    public void SnesLoRom_ExplicitHeader()
    {
        using var context = new TableTestContext();

        var definition = context.LoadValid("""
            <tabledefinition name="t">
            	<addressspace mapper="snes-lorom" header="0" />
            	<record name="R" size="1"><field name="a" offset="0" bits="8" /></record>
            </tabledefinition>
            """);

        var mapper = Assert.IsType<SnesLoRomAddressMapper>(definition.Mapper.ForDataLength(0x100200));
        Assert.Equal(0, mapper.Header);
        Assert.Same(mapper, mapper.ForDataLength(0x100000));
        Assert.Equal(0x200, Assert.IsType<SnesLoRomAddressMapper>(AddressMappers.Create("snes-lorom", 0, 512).AsSuccess.Result).Header);
    }

    [Theory]
    [InlineData("gba", "0", "does not take a copier header")]
    [InlineData("linear", "512", "does not take a copier header")]
    [InlineData("snes-lorom", "0x8000", "outside 0 to 0x7FFF")]
    public void Header_OnNonSnesMapper_FailsLoad(string mapper, string header, string message)
    {
        using var context = new TableTestContext();

        var result = context.LoadXml($"""
            <tabledefinition name="t">
            	<addressspace mapper="{mapper}" header="{header}" />
            	<record name="R" size="1"><field name="a" offset="0" bits="8" /></record>
            </tabledefinition>
            """);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains(message) && x.Contains("mapper"));
    }

    [Theory]
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
