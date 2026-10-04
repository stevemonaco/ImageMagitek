using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;

namespace ImageMagitek.UnitTests;

[Collection("Codec")]
public partial class ScatteredArrangerReversibilityTests
{
    private CodecFixture _fixture;

    public ScatteredArrangerReversibilityTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(ReverseCases))]
    public void ScatteredArranger_Reversibility_CanReverse(string imageFileName, ColorModel colorModel, bool zeroIndexTransparent, string codecName, Size codecSize)
    {
        var codec = _fixture.CodecFactory.CreateCodec(codecName, new(codecSize.Width, codecSize.Height))!;
        var arranger = ArrangerTestFactory.CreateIndexedArrangerFromImage(imageFileName,
                    colorModel,
                    zeroIndexTransparent,
                    _fixture.CodecFactory,
                    codec);

        var exportedImageFileName = $"test.png";

        var preview = ImageImporter.Prepare(arranger, imageFileName, ImageImportOptions.Default, new ImageSharpFileAdapter()).AsSuccess.Result;
        preview.Commit();
        new IndexedImage(arranger).ExportImage(exportedImageFileName, new ImageSharpFileAdapter());

        using var expected = Image<Rgba32>.Load<Rgba32>(imageFileName);
        using var actual = Image<Rgba32>.Load<Rgba32>(exportedImageFileName);

        ImageRgba32Assert.AreEqual(expected, actual);
    }
}
