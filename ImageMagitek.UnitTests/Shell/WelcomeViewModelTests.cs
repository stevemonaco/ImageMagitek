using System.IO;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Microsoft.Extensions.Logging;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class WelcomeViewModelTests
{
    private static string FirstDemoFile => WelcomeViewModel.PickDemoFile(TestPaths.DemoDirectory, 0).Path!;

    [Fact]
    public void PickDemoFile_RotatesThroughBinFilesByName()
    {
        var directory = Directory.CreateTempSubdirectory("imagemagitek-").FullName;
        foreach (var name in new[] { "b.bin", "a.bin", "c.bin", "notes.txt" })
            File.WriteAllBytes(Path.Combine(directory, name), []);

        Assert.Equal((Path.Combine(directory, "a.bin"), 1), WelcomeViewModel.PickDemoFile(directory, 0));
        Assert.Equal((Path.Combine(directory, "b.bin"), 2), WelcomeViewModel.PickDemoFile(directory, 1));
        Assert.Equal((Path.Combine(directory, "c.bin"), 0), WelcomeViewModel.PickDemoFile(directory, 2));
        Assert.Equal((Path.Combine(directory, "c.bin"), 0), WelcomeViewModel.PickDemoFile(directory, 5));
        Assert.Equal((Path.Combine(directory, "c.bin"), 0), WelcomeViewModel.PickDemoFile(directory, -1));
    }

    [Fact]
    public void PickDemoFile_NoFiles_NullAndRotationKept()
    {
        var empty = Directory.CreateTempSubdirectory("imagemagitek-").FullName;

        Assert.Equal((null, 3), WelcomeViewModel.PickDemoFile(empty, 3));
        Assert.Equal((null, 3), WelcomeViewModel.PickDemoFile(Path.Combine(empty, "missing"), 3));
    }

    [Fact]
    public void Banner_DecodesDemoFile()
    {
        var logger = new RecordingLogger();

        var image = WelcomeViewModel.DecodeBanner(FirstDemoFile, CodecFixture.Shared.CodecFactory, new ColorFactory(), logger);

        Assert.NotNull(image);
        Assert.Equal((256, 96), (image.Width, image.Height));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void MissingDemoFile_NoBannerAndLogs()
    {
        var logger = new RecordingLogger();
        var path = TestPaths.CreateTempPath(".bin");

        var image = WelcomeViewModel.DecodeBanner(path, CodecFixture.Shared.CodecFactory, new ColorFactory(), logger);

        Assert.Null(image);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(path, entry.Message);
    }

    [Fact]
    public void BrokenCodec_NoBanner()
    {
        var logger = new RecordingLogger();
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);

        var image = WelcomeViewModel.DecodeBanner(FirstDemoFile, factory, new ColorFactory(), logger);

        Assert.Null(image);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(FirstDemoFile, entry.Message);
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(255, 1)]
    [InlineData(256, 1)]
    [InlineData(511, 1)]
    [InlineData(512, 2)]
    [InlineData(768, 3)]
    [InlineData(1024, 4)]
    [InlineData(4000, 4)]
    public void ScaleFactor_LargestWholeFitUpToFour(double width, int expected)
    {
        Assert.Equal(expected, WelcomeViewModel.ScaleFor(width));
    }
}
