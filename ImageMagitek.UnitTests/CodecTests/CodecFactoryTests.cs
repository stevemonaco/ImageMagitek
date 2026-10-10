using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

[Collection("Codec")]
public class CodecFactoryTests
{
    private readonly CodecFixture _fixture;

    public CodecFactoryTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("SNES 3bpp", "SNES 3bpp Flow")]
    [InlineData("PSX 4bpp", "PSX 4bpp Flow")]
    [InlineData("PSX 8bpp", "PSX 8bpp Flow")]
    public void LegacyName_ResolvesToXmlCodec(string legacyName, string currentName)
    {
        var codec = _fixture.CodecFactory.CreateCodec(legacyName, new Size(16, 8));
        var unsized = _fixture.CodecFactory.CreateCodec(legacyName);

        var flow = Assert.IsType<IndexedFlowGraphicsCodec>(codec);
        Assert.Equal(currentName, flow.Name);
        Assert.Equal((16, 8), (flow.Width, flow.Height));
        Assert.Equal(currentName, unsized!.Name);
        Assert.DoesNotContain(legacyName, _fixture.CodecFactory.GetRegisteredCodecNames());
    }

    [Fact]
    public void AddCodecPlugin_NameTakenByXmlFormat_FailsAndKeepsFormat()
    {
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
        Assert.True(factory.AddFormat(LoadFormatNamed("SNES 3bpp Plugin")).HasSucceeded);

        var result = factory.AddCodecPlugin(typeof(Snes3BppCodec));

        Assert.True(result.HasFailed);
        Assert.Contains(nameof(Snes3BppCodec), result.AsError.Reason);
        Assert.IsType<IndexedFlowGraphicsCodec>(factory.CreateCodec("SNES 3bpp Plugin"));
    }

    [Fact]
    public void AddFormat_NameTakenByBuiltInCodec_Fails()
    {
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);

        var result = factory.AddFormat(LoadFormatNamed("PSX 16bpp"));

        Assert.True(result.HasFailed);
        Assert.IsType<Psx16BppCodec>(factory.CreateCodec("PSX 16bpp"));
    }

    [Fact]
    public void RegisteredNames_AreUnique()
    {
        var names = _fixture.CodecFactory.GetRegisteredCodecNames().ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    private static IGraphicsFormat LoadFormatNamed(string name)
    {
        var fileName = TestPaths.CreateTempPath(".xml");
        File.WriteAllText(fileName, File.ReadAllText(Path.Combine(TestPaths.CodecsPath, "GBA4bpp.xml")).Replace("name=\"GBA 4bpp\"", $"name=\"{name}\""));

        try
        {
            var reader = new XmlGraphicsFormatReader(Path.Combine(AppContext.BaseDirectory, "_schemas", "CodecSchema.xsd"));
            return reader.LoadFromFile(fileName).AsSuccess.Result;
        }
        finally
        {
            File.Delete(fileName);
        }
    }
}
