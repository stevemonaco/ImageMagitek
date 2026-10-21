using System.Collections.Generic;
using System.IO;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class CharmapTests
{
    private static Charmap LoadTestCharmap()
    {
        var reader = new XmlCharmapReader(Path.Combine(TableTestContext.SchemaDirectory, XmlTableDefinitionReader.CharmapSchemaFileName));
        var result = reader.LoadFromFile(Path.Combine(TableTestContext.FixturesDirectory, "test.charmap.xml"));
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    [Fact]
    public void Runs_Decode()
    {
        var charmap = LoadTestCharmap();

        Assert.Equal("AZ az 09", charmap.Decode([0xBB, 0xD4, 0x00, 0xD5, 0xEE, 0x00, 0xA1, 0xAA]));
        Assert.Equal("Pokémon PK", charmap.Decode([0xCA, 0xE3, 0xDF, 0x1B, 0xE1, 0xE3, 0xE2, 0x00, 0xF0]));
    }

    [Fact]
    public void Terminator_EndsString()
    {
        Assert.Equal("AB", LoadTestCharmap().Decode([0xBB, 0xBC, 0xFF, 0xBD]));
    }

    [Fact]
    public void UnmappedByte_RendersHexAndReports()
    {
        var unmapped = new List<byte>();

        Assert.Equal("A[FC]B", LoadTestCharmap().Decode([0xBB, 0xFC, 0xBC], unmapped));
        Assert.Equal([(byte)0xFC], unmapped);
    }
}
