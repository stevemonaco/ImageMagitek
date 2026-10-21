using System;
using System.IO;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableManifestTests : IDisposable
{
    private readonly TableTestContext _context = new();
    private readonly XmlTableManifestReader _reader = new(Path.Combine(TableTestContext.SchemaDirectory, XmlTableDefinitionReader.ManifestSchemaFileName));

    public void Dispose() => _context.Dispose();

    private static TableManifestGeneration Generation(string project, string folder, params TableManifestResource[] resources) =>
        new(project, folder, "/Rom", "FireRed.table.xml", resources);

    private TableManifest Read(string fileName)
    {
        var result = _reader.LoadFromFile(fileName);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    [Fact]
    public void WriteRead_RoundTrip()
    {
        var manifest = new TableManifest([Generation("FireRedProject.xml", "/Overworld",
            new TableManifestResource("/Overworld/Maps/Pallet Town/Pallet Town", "metatilemap", "layoutMap", "Layout", 0x2DD4C0),
            new TableManifestResource("/Overworld/Tilesets/Palettes/General 3", "palette", "tilesetPalettes", "palette", 0x2D4AB4, 16, 3))]);

        var xml = XmlTableManifestWriter.Serialize(manifest);
        var path = _context.Write("FireRed.manifest.xml", xml);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);
        Assert.Contains("\n\t<generation project=\"FireRedProject.xml\" folder=\"/Overworld\" datafile=\"/Rom\" definition=\"FireRed.table.xml\">", xml.Replace("\r\n", "\n"));
        Assert.Contains("<resource key=\"/Overworld/Tilesets/Palettes/General 3\" type=\"palette\" output=\"tilesetPalettes\" record=\"palette\" offset=\"2D4AB4\" entries=\"16\" index=\"3\" />", xml);

        var read = Read(path);
        var generation = Assert.Single(read.Generations);
        Assert.Equal(manifest.Generations[0].Resources, generation.Resources);
        Assert.Equal(("FireRedProject.xml", "/Overworld", "/Rom", "FireRed.table.xml"), (generation.Project, generation.Folder, generation.DataFile, generation.Definition));
        Assert.Equal(xml, XmlTableManifestWriter.Serialize(read));
    }

    [Fact]
    public void ForeignWrittenManifest_Reads()
    {
        var manifest = Read(Path.Combine(TableTestContext.FixturesDirectory, "Foreign.manifest.xml"));

        Assert.Equal(2, manifest.Generations.Count);
        var generation = manifest.Generations[0];
        Assert.Equal(("../Projects/FireRed/FireRedProject.xml", "/Overworld", "/Roms/FireRed", "defs/FireRed.table.xml"),
            (generation.Project, generation.Folder, generation.DataFile, generation.Definition));
        Assert.Equal([
            new TableManifestResource("/Overworld/Maps/Pallet Town/Pallet Town", "metatilemap", "layoutMap", "Layout", 0x2DD4C0),
            new TableManifestResource("/Overworld/Tilesets/Palettes/General 3", "palette", "tilesetPalettes", "palette", 0x2D4AB4, 16, 3)],
            generation.Resources);
        Assert.Empty(manifest.Generations[1].Resources);
    }

    [Fact]
    public void Rewrite_KeepsOtherGenerations()
    {
        var foreign = Read(Path.Combine(TableTestContext.FixturesDirectory, "Foreign.manifest.xml"));
        var replacement = Generation("../projects/firered/FIREREDPROJECT.xml", "/overworld",
            new TableManifestResource("/overworld/A", "palette", "p", "palette", 0x10, 16));

        var rewritten = foreign.WithGeneration(replacement).WithGeneration(Generation("Third.xml", "/"));

        Assert.Equal(3, rewritten.Generations.Count);
        Assert.Same(replacement, rewritten.Generations[0]);
        Assert.Same(foreign.Generations[1], rewritten.Generations[1]);
        Assert.Equal("Third.xml", rewritten.Generations[2].Project);
    }

    [Theory]
    [InlineData("""<tablemanifest version="2.0" />""", 1)]
    [InlineData("""
        <tablemanifest version="1.0">
        	<generation project="p.xml" folder="/" datafile="/Rom" definition="d.xml">
        		<resource key="/A" type="sprite" output="o" record="R" offset="10" />
        	</generation>
        </tablemanifest>
        """, 3)]
    [InlineData("<tablemanifest version=\"1.0\">\n<generation", 2)]
    public void Invalid_FailsNamingLine(string xml, int line)
    {
        var path = _context.Write("Bad.manifest.xml", xml);

        var result = _reader.LoadFromFile(path);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("Bad.manifest.xml") && x.Contains($"line {line}"));
    }
}
