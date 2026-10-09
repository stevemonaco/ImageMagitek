using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.UnitTests.Fixtures;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class XmlProjectReaderTests : IDisposable
{
    private const string Header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";

    private readonly string _directory;
    private readonly ColorFactory _colorFactory = new();
    private ProjectTree? _tree;

    public XmlProjectReaderTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(PathOf("rom.bin"), new byte[64]);
        WriteXml("project.xml", "<project version=\"0.9\" />");
        WriteXml("rom.xml", "<datafile location=\"rom.bin\" />");
    }

    public void Dispose()
    {
        if (_tree is not null)
        {
            foreach (var source in _tree.EnumerateDepthFirst().Select(x => x.Item).OfType<DataSource>())
                source.Dispose();
        }

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    private string PathOf(string relativePath) => Path.Combine(_directory, relativePath);

    private void WriteXml(string relativePath, string xml)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(relativePath))!);
        File.WriteAllText(PathOf(relativePath), Header + xml);
    }

    private MagitekResults<ProjectTree> Read()
    {
        var result = ProjectFixtures.CreateSerializerFactory(_colorFactory).CreateReader().ReadProject(PathOf("project.xml"));
        if (result.HasSucceeded)
            _tree = result.AsSuccess.Result;

        return result;
    }

    private ProjectTree ReadSuccess()
    {
        var result = Read();
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("; ", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    private string ReadSingleFailure()
    {
        var result = Read();
        Assert.True(result.HasFailed);
        return Assert.Single(result.AsError.Reasons);
    }

    private static string Arranger(string attributes, params string[] elements) => ArrangerWithCodec("SNES 4bpp", attributes, elements);

    private static string ArrangerWithCodec(string codec, string attributes, params string[] elements) =>
        $"<arranger elementsx=\"2\" elementsy=\"2\" width=\"8\" height=\"8\" layout=\"tiled\" color=\"indexed\" defaultcodec=\"{codec}\" defaultdatafile=\"/rom\" {attributes}>" +
        string.Concat(elements) + "</arranger>";

    private static string Element(int x, int y, string attributes = "") =>
        $"<element fileoffset=\"0\" posx=\"{x}\" posy=\"{y}\" {attributes} />";

    private static Palette ElementPalette(ProjectTree tree, string arrangerKey, int x, int y)
    {
        Assert.True(tree.TryGetItem<ScatteredArranger>(arrangerKey, out var arranger));
        var codec = Assert.IsAssignableFrom<IIndexedCodec>(arranger.GetElement(x, y)!.Value.Codec);
        return codec.Palette;
    }

    [Fact]
    public void FileSource_BitOffset_LoadsAtBitAddress()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Bgr15\" zeroindextransparent=\"false\"><filesource fileoffset=\"2\" bitoffset=\"4\" entries=\"2\" /></palette>");

        var tree = ReadSuccess();

        Assert.True(tree.TryGetItem<Palette>("/pal", out var palette));
        Assert.Equal([new BitAddress(2, 4), new BitAddress(4, 4)], palette.ColorSources.Cast<FileColorSource>().Select(x => x.Offset));
    }

    [Fact]
    public void NativeColor_Unparseable_FailsNamingEntry()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Rgba32\" zeroindextransparent=\"false\"><filesource fileoffset=\"0\" entries=\"3\" /><nativecolor value=\"#GG0000FF\" /></palette>");
        // The schema rejects every value the parser rejects, so only an unvalidated read reaches the reader's own check
        var reader = new XmlProjectReader(null!, CodecFixture.Shared.CodecFactory, _colorFactory, [ProjectFixtures.DefaultPalette]);

        var result = reader.ReadProject(PathOf("project.xml"));

        Assert.True(result.HasFailed);
        var reason = Assert.Single(result.AsError.Reasons);
        Assert.Contains(PathOf("pal.xml"), reason);
        Assert.Contains("entry 3", reason);
        Assert.Contains("'#GG0000FF'", reason);
    }

    [Fact]
    public void ForeignColor_WrongWidthForModel_FailsNamingEntry()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Bgr15\" zeroindextransparent=\"false\"><nativecolor value=\"#000000FF\" /><foreigncolor value=\"#7C\" /></palette>");

        var reason = ReadSingleFailure();

        Assert.Contains(PathOf("pal.xml"), reason);
        Assert.Contains("entry 1", reason);
        Assert.Contains("'#7C'", reason);
        Assert.Contains("Bgr15", reason);
    }

    [Fact]
    public void Schema_ColorPatterns_HaveNoAnchors()
    {
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        var patterns = XDocument.Load(ProjectFixtures.ResourceSchemaPath).Descendants(xs + "pattern")
            .Select(x => x.Attribute("value")!.Value)
            .ToList();

        Assert.NotEmpty(patterns);
        Assert.All(patterns, x => Assert.DoesNotContain('^', x));
        Assert.All(patterns, x => Assert.DoesNotContain('$', x));
    }

    [Fact]
    public void SchemaError_FailsWithFileAndLine()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Bogus\" zeroindextransparent=\"false\" />");

        var result = Read();

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains(PathOf("pal.xml")) && x.Contains("line 2"));
    }

    [Fact]
    public void UnknownRootElement_Fails()
    {
        WriteXml("project.xml", "<datafile location=\"rom.bin\" />");

        var reason = ReadSingleFailure();

        Assert.Contains("expected to be a project file", reason);
    }

    [Fact]
    public void UnknownCodec_FailsNamingCodecAndArranger()
    {
        WriteXml("Folder/arr.xml", ArrangerWithCodec("Missing Codec", "", Element(0, 0)));

        var reason = ReadSingleFailure();

        Assert.Contains("/Folder/arr", reason);
        Assert.Contains("'Missing Codec'", reason);
    }

    [Fact]
    public void IndexedArrangerWithDirectCodec_FailsNamingCodecAndArranger()
    {
        WriteXml("arr.xml", ArrangerWithCodec("Rgba32 Tiled", "", Element(0, 0)));

        var reason = ReadSingleFailure();

        Assert.Contains("/arr", reason);
        Assert.Contains("direct codec 'Rgba32 Tiled'", reason);
    }

    [Fact]
    public void UnresolvedDataFileKey_Palette_FailsNamingKeyAndPalette()
    {
        WriteXml("Palettes/pal.xml", "<palette datafile=\"/missing\" color=\"Bgr15\" zeroindextransparent=\"false\"><filesource fileoffset=\"0\" entries=\"2\" /></palette>");

        var reason = ReadSingleFailure();

        Assert.Contains("'/Palettes/pal'", reason);
        Assert.Contains("'/missing'", reason);
    }

    [Fact]
    public void UnresolvedDataFileKey_Element_FailsNamingKeyArrangerAndPosition()
    {
        WriteXml("arr.xml", Arranger("", Element(0, 0), Element(1, 0, "datafile=\"/missing\"")));

        var reason = ReadSingleFailure();

        Assert.Contains("'/arr'", reason);
        Assert.Contains("'/missing'", reason);
        Assert.Contains("(1, 0)", reason);
    }

    [Fact]
    public void UnresolvedPaletteKey_FailsNamingKeyArrangerAndPosition()
    {
        WriteXml("arr.xml", Arranger("", Element(0, 0), Element(0, 1, "palette=\"/Gone/DefaultRgba32\"")));

        var reason = ReadSingleFailure();

        Assert.Contains("'/arr'", reason);
        Assert.Contains("'/Gone/DefaultRgba32'", reason);
        Assert.Contains("(0, 1)", reason);
    }

    [Fact]
    public void UnresolvedKeyUsedByManyElements_ReportedOnce()
    {
        WriteXml("arr.xml", Arranger("defaultpalette=\"/Gone\"", Element(0, 0), Element(1, 0), Element(0, 1), Element(1, 1, "datafile=\"/missing\"")));

        var result = Read();

        Assert.True(result.HasFailed);
        Assert.Equal(2, result.AsError.Reasons.Count);
        var paletteReason = Assert.Single(result.AsError.Reasons, x => x.Contains("'/Gone'"));
        Assert.Contains("(0, 0)", paletteReason);
        Assert.Single(result.AsError.Reasons, x => x.Contains("'/missing'"));
    }

    [Fact]
    public void PaletteKeyNamingGlobalPalette_ResolvesIgnoringCase()
    {
        WriteXml("arr.xml", Arranger("defaultpalette=\"defaultrgba32\"", Element(0, 0)));

        var tree = ReadSuccess();

        Assert.Same(ProjectFixtures.DefaultPalette, ElementPalette(tree, "/arr", 0, 0));
    }

    [Fact]
    public void MissingDefaultPalette_UsesGlobalDefault()
    {
        WriteXml("arr.xml", Arranger("", Element(0, 0)));

        var tree = ReadSuccess();

        Assert.Same(ProjectFixtures.DefaultPalette, ElementPalette(tree, "/arr", 0, 0));
    }

    [Fact]
    public void JournalInProjectFileDirectory_RefusesLoad()
    {
        WriteXml("project.xml", "<project version=\"0.9\" root=\"Resources\" />");
        Directory.CreateDirectory(PathOf("Resources"));
        File.Move(PathOf("rom.xml"), PathOf(Path.Combine("Resources", "rom.xml")));
        File.WriteAllText(PathOf("_transaction.json"), "{}");

        var reason = ReadSingleFailure();

        Assert.Contains(PathOf("_transaction.json"), reason);
    }
}
