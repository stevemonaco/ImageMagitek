using System.IO;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public class BootstrapPathsTests
{
    [Fact]
    public void FromDirectory_RootsEveryPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "app");

        var paths = BootstrapPaths.FromDirectory(directory);

        string[] all = [paths.SettingsFileName, paths.PalettesPath, paths.CodecsPath, paths.PluginsPath, paths.LayoutsPath,
            paths.ResourceSchemaFileName, paths.CodecSchemaFileName, paths.MapFormatsPath, paths.MapFormatSchemaFileName];
        Assert.All(all, x => Assert.StartsWith(directory + Path.DirectorySeparatorChar, x));
        Assert.Equal(Path.Combine(directory, "_schemas", "CodecSchema.xsd"), paths.CodecSchemaFileName);
        Assert.Equal(Path.Combine(directory, "_mapformats"), paths.MapFormatsPath);
        Assert.Equal(Path.Combine(directory, "_schemas", "MapFormatSchema.xsd"), paths.MapFormatSchemaFileName);
    }
}
