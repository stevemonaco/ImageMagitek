using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Plugins;
using ImageMagitek.PluginSamples;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Runs the indexed codec contract against every codec in the sample plugin assembly, registered the way the plugin loader registers them.
/// </summary>
public class SamplePluginContractTests : IndexedCodecContract
{
    private static readonly CodecFactory _factory = CreateFactory();

    public static TheoryData<string, int, int> ContractCases =>
        CodecTestHelpers.BuildCases(includeSquare: false, factory: _factory, codecNames: _factory.GetRegisteredCodecNames().Where(x => _factory.CreateCodec(x) is IIndexedCodec));

    protected override IIndexedCodec CreateCodec(string codecName, int width, int height) =>
        CodecTestHelpers.CreateCodec(_factory, codecName, width, height);

    [Fact]
    public void AllSamples_AreRegistered()
    {
        var sampleCount = typeof(Snes4BppCodec).Assembly.GetTypes().Count(x => typeof(ICodecPlugin).IsAssignableFrom(x) && !x.IsAbstract);

        Assert.Equal(7, sampleCount);
        Assert.Equal(sampleCount, _factory.GetRegisteredCodecNames().Count(x => _factory.CreateCodec(x) is IndexedCodecPluginAdapter));
    }

    private static CodecFactory CreateFactory()
    {
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
        var sampleTypes = typeof(Snes4BppCodec).Assembly.GetTypes()
            .Where(x => typeof(ICodecPlugin).IsAssignableFrom(x) && !x.IsAbstract);

        foreach (var sampleType in sampleTypes)
            Assert.True(factory.AddCodecPlugin(sampleType).HasSucceeded);

        return factory;
    }
}
