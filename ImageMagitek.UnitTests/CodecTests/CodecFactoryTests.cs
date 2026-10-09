using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.PluginSample;
using ImageMagitek.Services;
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
    public void PluginService_DiscoversSampleCodecs()
    {
        var pluginsPath = Path.Combine(Path.GetTempPath(), $"imagemagitek-plugins-{Guid.NewGuid():N}");
        var samplesAssembly = typeof(Snes3BppCodec).Assembly;
        var pluginName = samplesAssembly.GetName().Name!;
        var pluginDirectory = Directory.CreateDirectory(Path.Combine(pluginsPath, pluginName));
        File.Copy(samplesAssembly.Location, Path.Combine(pluginDirectory.FullName, pluginName + ".dll"));

        try
        {
            var pluginService = new PluginService();
            pluginService.LoadCodecPlugins(pluginsPath);

            // Plugin types load into their own context, so compare names rather than Type identity
            var expected = samplesAssembly.GetTypes()
                .Where(x => typeof(IGraphicsCodec).IsAssignableFrom(x) && !x.IsAbstract)
                .Select(x => x.FullName)
                .Order();
            Assert.Equal(expected, pluginService.CodecPlugins.Select(x => x.FullName).Order());

            var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
            foreach (var codecType in pluginService.CodecPlugins)
                Assert.True(factory.AddCodec(codecType).HasSucceeded);

            Assert.Contains("SNES 3bpp Plugin", factory.GetRegisteredCodecNames());
            Assert.Contains("Last Armageddon Font", factory.GetRegisteredCodecNames());
        }
        finally
        {
            try
            {
                Directory.Delete(pluginsPath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The plugin context is not collectible, so the copied DLL stays locked
            }
        }
    }

    [Fact]
    public void PluginService_BadDllBesideSamples_SkipsItAndLoadsSamples()
    {
        var pluginsPath = TestPaths.CreateTempPath("");
        var samplesAssembly = typeof(Snes3BppCodec).Assembly;
        var pluginName = samplesAssembly.GetName().Name!;
        var pluginDirectory = Directory.CreateDirectory(Path.Combine(pluginsPath, pluginName));
        File.Copy(samplesAssembly.Location, Path.Combine(pluginDirectory.FullName, pluginName + ".dll"));
        var badDirectory = Directory.CreateDirectory(Path.Combine(pluginsPath, "Bad"));
        File.WriteAllText(Path.Combine(badDirectory.FullName, "Bad.dll"), "not an assembly");

        try
        {
            var pluginService = new PluginService();
            var result = pluginService.LoadCodecPlugins(pluginsPath);

            Assert.True(result.HasFailed);
            var reason = Assert.Single(result.AsError.Reasons);
            Assert.Contains("Bad.dll", reason);
            Assert.Contains(pluginService.CodecPlugins, x => x.FullName == typeof(Snes3BppCodec).FullName);
        }
        finally
        {
            try
            {
                Directory.Delete(pluginsPath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The plugin context is not collectible, so the copied DLL stays locked
            }
        }
    }

    [Fact]
    public void AddCodec_NameTakenByXmlFormat_FailsAndKeepsFormat()
    {
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
        Assert.True(factory.AddFormat(LoadFormatNamed("SNES 3bpp Plugin")).HasSucceeded);

        var result = factory.AddCodec(typeof(Snes3BppCodec));

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

    [Fact]
    public void AddCodec_ThrowingConstructor_Fails()
    {
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);

        var result = factory.AddCodec(typeof(ThrowingCodec));

        Assert.True(result.HasFailed);
        Assert.Contains(nameof(ThrowingCodec), result.AsError.Reason);
        Assert.DoesNotContain("Throwing", factory.GetRegisteredCodecNames());
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

    private sealed class ThrowingCodec : IndexedCodec
    {
        public override string Name => "Throwing";
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 1;
        public override int StorageSize => 8;
        public override bool CanEncode => false;
        public override int DefaultWidth => 8;
        public override int DefaultHeight => 8;
        public override bool CanResize => false;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;

        public ThrowingCodec(Palette palette) : base(palette)
        {
            throw new InvalidOperationException("Constructor failure");
        }

        public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer) => throw new NotSupportedException();
        public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer) => throw new NotSupportedException();
    }
}
