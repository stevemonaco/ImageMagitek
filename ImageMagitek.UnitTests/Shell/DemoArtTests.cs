using System.IO;
using System.Linq;
using DemoArtGenerator;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

[Collection("Codec")]
public class DemoArtTests(CodecFixture fixture)
{
    public static TheoryData<string> SceneNames => new(DemoArt.Scenes.Select(x => x.Name));

    private static Scene SceneNamed(string name) => DemoArt.Scenes.Single(x => x.Name == name);
    private static string ShippedPath(string name) => Path.Combine(TestPaths.DemoDirectory, DemoArt.FileName(SceneNamed(name)));

    [Fact]
    public void ShippedFiles_AreExactlyTheScenes()
    {
        var shipped = Directory.GetFiles(TestPaths.DemoDirectory).Select(Path.GetFileName).Order();

        Assert.Equal(DemoArt.Scenes.Select(DemoArt.FileName).Order(), shipped);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void ShippedFile_MatchesGenerator(string name)
    {
        var shipped = File.ReadAllBytes(ShippedPath(name));

        Assert.Equal(DemoArt.Generate(SceneNamed(name), fixture.CodecFactory), shipped);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void ShippedFile_Is12320Bytes(string name)
    {
        Assert.Equal(12320, new FileInfo(ShippedPath(name)).Length);
        Assert.Equal(DemoArt.FileLength, WelcomeViewModel.DemoFileLength);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Palette_IsLast32Bytes_Bgr15_ZeroTransparent(string name)
    {
        var path = ShippedPath(name);
        var bytes = File.ReadAllBytes(path);
        var colors = Enumerable.Range(0, 16)
            .Select(i => (ushort)(bytes[bytes.Length - 32 + i * 2] | bytes[bytes.Length - 31 + i * 2] << 8))
            .ToArray();

        Assert.All(colors, x => Assert.Equal(0, x & 0x8000));
        Assert.Equal(SceneNamed(name).Colors.Select(c => (ushort)new ColorBgr15(c.R, c.G, c.B).Color), colors);

        var image = WelcomeViewModel.DecodeBanner(path, fixture.CodecFactory, new ColorFactory(), NullLogger.Instance)!;
        var palette = ((SequentialArranger)image.Arranger).ActivePalette;
        Assert.True(palette.ZeroIndexTransparent);
        Assert.Equal(ColorModel.Bgr15, palette.ColorModel);
        Assert.Equal(16, palette.Entries);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Decodes_WithSnes4bpp_NoIndexAbove15(string name)
    {
        var image = WelcomeViewModel.DecodeBanner(ShippedPath(name), fixture.CodecFactory, new ColorFactory(), NullLogger.Instance);

        Assert.NotNull(image);
        Assert.Equal((256, 96), (image.Width, image.Height));
        Assert.Equal("SNES 4bpp", ((SequentialArranger)image.Arranger).ActiveCodec.Name);
        Assert.All(image.Image, x => Assert.True(x <= 15));
        Assert.Equal(DemoArt.Compose(SceneNamed(name)), image.Image);
    }
}
