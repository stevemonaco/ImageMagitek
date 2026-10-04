using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
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
                factory.AddOrUpdateCodec(codecType);

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
}
