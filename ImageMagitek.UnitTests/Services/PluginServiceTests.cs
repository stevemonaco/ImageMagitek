using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Plugins;
using ImageMagitek.PluginSamples;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests;

public sealed class PluginServiceTests : IDisposable
{
    private readonly string _pluginsPath = TestPaths.CreateTempPath("");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pluginsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The plugin context is not collectible, so copied DLLs stay locked
        }
    }

    [Fact]
    public void LoadCodecPlugins_Samples_DiscoversEveryCodec()
    {
        CopyPlugin(typeof(Snes3BppCodec).Assembly.Location, "ImageMagitek.PluginSamples");
        var pluginService = new PluginService();

        var result = pluginService.LoadCodecPlugins(_pluginsPath);

        // Plugin types load into their own context, so compare names rather than Type identity
        var expected = typeof(Snes3BppCodec).Assembly.GetTypes()
            .Where(x => typeof(ICodecPlugin).IsAssignableFrom(x) && !x.IsAbstract)
            .Select(x => x.FullName)
            .Order();
        Assert.True(result.HasSucceeded);
        Assert.Equal(expected, pluginService.CodecPlugins.Select(x => x.FullName).Order());

        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
        foreach (var codecType in pluginService.CodecPlugins)
            Assert.True(factory.AddCodecPlugin(codecType).HasSucceeded);

        Assert.Contains("SNES 3bpp Plugin", factory.GetRegisteredCodecNames());
        Assert.IsType<IndexedCodecPluginAdapter>(factory.CreateCodec("Last Armageddon Font"));
    }

    [Fact]
    public void LoadCodecPlugins_BadDllBesideSamples_SkipsItAndLoadsSamples()
    {
        CopyPlugin(typeof(Snes3BppCodec).Assembly.Location, "ImageMagitek.PluginSamples");
        var badDirectory = Directory.CreateDirectory(Path.Combine(_pluginsPath, "Bad"));
        File.WriteAllText(Path.Combine(badDirectory.FullName, "Bad.dll"), "not an assembly");
        var pluginService = new PluginService();

        var result = pluginService.LoadCodecPlugins(_pluginsPath);

        Assert.True(result.HasFailed);
        var reason = Assert.Single(result.AsError.Reasons);
        Assert.Contains("Bad.dll", reason);
        Assert.Contains(pluginService.CodecPlugins, x => x.FullName == typeof(Snes3BppCodec).FullName);
    }

    [Fact]
    public void LoadCodecPlugins_CoreReferenceWithoutPluginTypes_ReportsRebuild()
    {
        CopyPlugin(typeof(PluginService).Assembly.Location, "Legacy");
        var pluginService = new PluginService();

        var result = pluginService.LoadCodecPlugins(_pluginsPath);

        Assert.True(result.HasFailed);
        var reason = Assert.Single(result.AsError.Reasons);
        Assert.Contains("Legacy.dll", reason);
        Assert.Contains("pre-1.0", reason);
        Assert.Contains("ImageMagitek.Plugins.Contracts", reason);
        Assert.Empty(pluginService.CodecPlugins);
    }

    [Theory]
    [InlineData("1.2", "1.2", null)]
    [InlineData("1.1", "1.2", null)]
    [InlineData("1.3", "1.2", "newer TileShop")]
    [InlineData("2.0", "1.2", "incompatible plugin contract")]
    [InlineData("0.9", "1.2", "incompatible plugin contract")]
    public void CheckContractVersion_FollowsMajorMinorRule(string referenced, string host, string? expected)
    {
        var reason = PluginService.CheckContractVersion(Version.Parse(referenced), Version.Parse(host));

        if (expected is null)
        {
            Assert.Null(reason);
            return;
        }

        Assert.NotNull(reason);
        Assert.Contains(expected, reason);
        Assert.Contains(referenced, reason);
        Assert.Contains(host, reason);
    }

    private void CopyPlugin(string assemblyLocation, string pluginName)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_pluginsPath, pluginName));
        File.Copy(assemblyLocation, Path.Combine(directory.FullName, pluginName + ".dll"));
    }
}
