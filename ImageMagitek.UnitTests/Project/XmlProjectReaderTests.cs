using System;
using System.Globalization;
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

    private static string ScatteredPalette(string color, params string[] runs) =>
        $"<palette datafile=\"/rom\" color=\"{color}\" zeroindextransparent=\"false\">{string.Concat(runs)}</palette>";

    private static string Run(int entries, params string[] fields) =>
        $"<scatteredcolor entries=\"{entries}\">{string.Concat(fields)}</scatteredcolor>";

    [Fact]
    public void FileSource_BitOffset_FailsValidation()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Bgr15\" zeroindextransparent=\"false\"><filesource fileoffset=\"2\" bitoffset=\"4\" entries=\"2\" /></palette>");

        var reason = ReadSingleFailure();

        Assert.Contains("bitoffset", reason);
    }

    [Fact]
    public void ScatteredColor_ReadsRun()
    {
        WriteXml("pal.xml", ScatteredPalette("Bgr15",
            Run(3, "<field fileoffset=\"10\" lowbit=\"1\" bits=\"5\" stride=\"16\" />", "<field fileoffset=\"20\" bits=\"4\" shift=\"5\" stride=\"4\" />")));

        var tree = ReadSuccess();

        Assert.True(tree.TryGetItem<Palette>("/pal", out var palette));
        var sources = palette.ColorSources.Cast<ScatteredColorSource>().ToList();
        Assert.Equal(3, sources.Count);
        Assert.Equal([new ColorField(0x10, 1, 5, 0), new ColorField(0x20, 0, 4, 5)], sources[0].Fields);
        Assert.Equal([new ColorField(0x12, 1, 5, 0), new ColorField(0x20, 4, 4, 5)], sources[1].Fields);
        Assert.Equal([new ColorField(0x14, 1, 5, 0), new ColorField(0x21, 0, 4, 5)], sources[2].Fields);
    }

    [Fact]
    public void ScatteredColor_DefaultStrideShiftAndLowBit()
    {
        WriteXml("pal.xml", ScatteredPalette("Bgr15", Run(2, "<field fileoffset=\"8\" bits=\"8\" />")));

        var tree = ReadSuccess();

        Assert.True(tree.TryGetItem<Palette>("/pal", out var palette));
        Assert.Equal([new ColorField(8, 0, 8, 0), new ColorField(9, 0, 8, 0)],
            palette.ColorSources.Cast<ScatteredColorSource>().Select(x => Assert.Single(x.Fields)));
    }

    [Fact]
    public void ScatteredColor_FieldOverflowsModel_FailsNamingEntry()
    {
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Nes\" zeroindextransparent=\"false\"><filesource fileoffset=\"0\" entries=\"2\" />" +
            Run(1, "<field fileoffset=\"8\" bits=\"4\" />", "<field fileoffset=\"9\" bits=\"4\" shift=\"6\" />") + "</palette>");

        var reason = ReadSingleFailure();

        Assert.Contains(PathOf("pal.xml"), reason);
        Assert.Contains("entry 2 field 1", reason);
    }

    [Fact]
    public void ScatteredColor_OverlappingFields_Fails()
    {
        WriteXml("pal.xml", ScatteredPalette("Bgr15", Run(1, "<field fileoffset=\"8\" bits=\"4\" />", "<field fileoffset=\"8\" lowbit=\"2\" bits=\"4\" shift=\"4\" />")));

        var reason = ReadSingleFailure();

        Assert.Contains("entry 0 field 1", reason);
        Assert.Contains("overlaps", reason);
    }

    [Fact]
    public void ScatteredColor_StrideCrossesByte_FailsNamingEntry()
    {
        WriteXml("pal.xml", ScatteredPalette("Bgr15", Run(3, "<field fileoffset=\"8\" bits=\"5\" stride=\"4\" />")));

        var reason = ReadSingleFailure();

        Assert.Contains("entry 1 field 0", reason);
        Assert.Contains("byte", reason);
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
        WriteXml("arr.xml", Arranger("", Element(0, 0), Element(0, 1, "palette=\"/Gone/Classic\"")));

        var reason = ReadSingleFailure();

        Assert.Contains("'/arr'", reason);
        Assert.Contains("'/Gone/Classic'", reason);
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
        WriteXml("arr.xml", Arranger("defaultpalette=\"classic\"", Element(0, 0)));

        var tree = ReadSuccess();

        Assert.Same(ProjectFixtures.DefaultPalette, ElementPalette(tree, "/arr", 0, 0));
    }

    [Fact]
    public void PaletteKeyNamingRenamedGlobalPalette_ResolvesToNewName()
    {
        WriteXml("arr.xml", Arranger("defaultpalette=\"DefaultRgba32\"", Element(0, 0)));

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
    public void DotDirectory_NotAFolderAndNotScanned()
    {
        Directory.CreateDirectory(PathOf(Path.Combine(".git", "objects", "ab")));
        WriteXml(".idea/workspace.xml", "<project version=\"4\"><component name=\"ChangeListManager\" /></project>");
        WriteXml(".vs/config.xml", "<arranger />");

        var tree = ReadSuccess();

        Assert.DoesNotContain(tree.EnumerateDepthFirst(), x => x.Name.StartsWith('.'));
    }

    [Fact]
    public void DotPrefixedXmlFile_Ignored()
    {
        WriteXml(".hidden.xml", "<arranger />");

        var tree = ReadSuccess();

        Assert.False(tree.TryGetItem<ScatteredArranger>("/.hidden", out _));
    }

    [Fact]
    public void ForeignRootXml_Ignored()
    {
        WriteXml("Docs/notes.xml", "<notes><note>not a resource</note></notes>");

        var tree = ReadSuccess();

        Assert.True(tree.TryGetNode("/Docs", out _));
        Assert.False(tree.TryGetNode("/Docs/notes", out _));
    }

    [Fact]
    public void MalformedXml_FailsNamingFileAndLine()
    {
        WriteXml("arr.xml", "<<<<<<< HEAD\n" + Arranger("", Element(0, 0)) + "\n=======\n" + Arranger("", Element(1, 0)) + "\n>>>>>>> branch");

        var reason = ReadSingleFailure();

        Assert.Contains(PathOf("arr.xml"), reason);
        Assert.Contains("line 2", reason);
    }

    [Fact]
    public void EmptyDirectory_BecomesFolder()
    {
        Directory.CreateDirectory(PathOf(Path.Combine("Outer", "Inner")));

        var tree = ReadSuccess();

        Assert.True(tree.TryGetNode("/Outer/Inner", out var inner));
        Assert.IsType<ResourceFolderNode>(inner);
    }

    [Fact]
    public void ReferenceIntoDotDirectory_FailsNamingKey()
    {
        WriteXml(".dot/rom.xml", "<datafile location=\".dot/rom.bin\" />");
        File.WriteAllBytes(PathOf(Path.Combine(".dot", "rom.bin")), new byte[64]);
        WriteXml("arr.xml", Arranger("", Element(0, 0, "datafile=\"/.dot/rom\"")));

        var reason = ReadSingleFailure();

        Assert.Contains("'/.dot/rom'", reason);
    }

    [Fact]
    public void NewerMinor_FailsNamingVersions()
    {
        WriteXml("project.xml", "<project version=\"1.1\" />");

        var reason = ReadSingleFailure();

        Assert.Equal("'project.xml' uses project format 1.1; this version of TileShop reads format 1.0 and older.", reason);
    }

    [Fact]
    public void NewerMajor_FailsBeforeReadingResources()
    {
        WriteXml("project.xml", "<project version=\"2.0\" />");
        WriteXml("pal.xml", "<palette datafile=\"/rom\" color=\"Bogus\" zeroindextransparent=\"false\" />");

        var reason = ReadSingleFailure();

        Assert.Contains("project format 2.0", reason);
    }

    [Fact]
    public void NewerWithUnknownAttributes_ReportsVersionNotSchema()
    {
        WriteXml("project.xml", "<project version=\"1.3\" keyindex=\"keys.xml\"><future /></project>");

        var reason = ReadSingleFailure();

        Assert.Contains("project format 1.3", reason);
    }

    [Theory]
    [InlineData("<project />", "no project format version")]
    [InlineData("<project version=\"abc\" />", "'abc'")]
    [InlineData("<project version=\"1,0\" />", "'1,0'")]
    [InlineData("<project version=\"1.0.0\" />", "'1.0.0'")]
    public void MissingOrUnparseableVersion_FailsNamingFile(string projectXml, string expected)
    {
        WriteXml("project.xml", projectXml);

        var reason = ReadSingleFailure();

        Assert.Contains("'project.xml'", reason);
        Assert.Contains(expected, reason);
    }

    [Fact]
    public void OlderThan09_FailsNamingVersion()
    {
        WriteXml("project.xml", "<project version=\"0.8\" />");

        var reason = ReadSingleFailure();

        Assert.Contains("'project.xml'", reason);
        Assert.Contains("0.8", reason);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void Parse_IsCultureInvariant(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.True(ProjectFormat.TryParse("0.9", out var version));
            Assert.Equal(new Version(0, 9), version);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Ordering_1_10_IsNewerThan_1_9()
    {
        Assert.True(ProjectFormat.TryParse("1.10", out var newer));
        Assert.True(ProjectFormat.TryParse("1.9", out var older));

        Assert.True(newer > older);
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
