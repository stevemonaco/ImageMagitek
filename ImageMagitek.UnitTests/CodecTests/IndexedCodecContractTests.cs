using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagitek.Codec;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Contract every shipped XML flow and pattern codec must satisfy, independent of arrangers and data sources.
/// </summary>
[Collection("Codec")]
public partial class IndexedCodecContractTests : IndexedCodecContract
{
    private readonly CodecFixture _fixture;

    public IndexedCodecContractTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void AllShippedXmlCodecsLoad()
    {
        var files = Directory.GetFiles(TestPaths.CodecsPath, "*.xml");

        var reader = new XmlGraphicsFormatReader(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"));

        Assert.Equal(files.Length, _fixture.XmlCodecNames.Count);

        foreach (var file in files)
        {
            var root = XDocument.Load(file).Root!;
            var name = root.Attribute("name")!.Value;
            var isFlow = root.Name.LocalName == "flowcodec";
            var expectedWidth = int.Parse(root.Element(isFlow ? "defaultwidth" : "width")!.Value);
            var expectedHeight = int.Parse(root.Element(isFlow ? "defaultheight" : "height")!.Value);

            var format = reader.LoadFromFile(file).AsSuccess.Result;
            var codec = _fixture.CodecFactory.CreateCodec(name)!;

            Assert.Equal((expectedWidth, expectedHeight), (format.DefaultWidth, format.DefaultHeight));
            Assert.Equal((expectedWidth, expectedHeight), (format.Width, format.Height));
            Assert.Contains(name, _fixture.XmlCodecNames);
            Assert.Equal((expectedWidth, expectedHeight), (codec.DefaultWidth, codec.DefaultHeight));
            Assert.Equal((expectedWidth, expectedHeight), (codec.Width, codec.Height));
        }
    }

    [Fact]
    public void BuildOutputShipsEveryCodecXml()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "_codecs");
        var expected = ReadXmlFiles(TestPaths.CodecsPath);
        var actual = ReadXmlFiles(outputPath);

        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());

        foreach (var (name, contents) in expected)
            Assert.True(contents == actual[name], $"Build output copy of '{name}' differs from the source file");
    }

    private static Dictionary<string, string> ReadXmlFiles(string path) =>
        Directory.GetFiles(path, "*.xml").ToDictionary(x => Path.GetFileName(x)!, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

    protected override IIndexedCodec CreateCodec(string codecName, int width, int height) =>
        CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
}
