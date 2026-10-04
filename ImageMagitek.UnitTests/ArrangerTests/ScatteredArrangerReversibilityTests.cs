using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ImageMagitek.UnitTests;

[Collection("Codec")]
public partial class ScatteredArrangerReversibilityTests
{
    private readonly CodecFixture _fixture;
    private readonly ImageSharpFileAdapter _adapter = new();

    public ScatteredArrangerReversibilityTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(ReverseCases))]
    public void ImageToDataToImage_RoundTrips(string codecName, int width, int height) =>
        AssertImageRoundTrip(codecName, width, height);

    [Theory(Skip = CodecTestHelpers.RowInterlaceEncodeBug)]
    [MemberData(nameof(KnownBugReverseCases))]
    public void ImageToDataToImage_RowInterlacedNonSquare_RoundTrips(string codecName, int width, int height) =>
        AssertImageRoundTrip(codecName, width, height);

    [Theory]
    [MemberData(nameof(ReverseCases))]
    public void DataToImageToData_PreservesRom(string codecName, int width, int height) =>
        AssertRomPreserved(codecName, width, height);

    [Theory(Skip = CodecTestHelpers.RowInterlaceEncodeBug)]
    [MemberData(nameof(KnownBugReverseCases))]
    public void DataToImageToData_RowInterlacedNonSquare_PreservesRom(string codecName, int width, int height) =>
        AssertRomPreserved(codecName, width, height);

    private void AssertImageRoundTrip(string codecName, int width, int height)
    {
        var (arranger, palette) = CreateArranger(codecName, width, height);
        var size = arranger.ArrangerPixelSize;
        var depth = ((IIndexedCodec)arranger.GetElement(0, 0)!.Value.Codec).ColorDepth;
        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(size.Width, size.Height, depth, 7));
        var source = new DecodedImage(indices.Select(x => palette[x]).ToArray(), size.Width, size.Height);
        var inputPath = TestPaths.CreateTempPath(".png");
        var outputPath = TestPaths.CreateTempPath(".png");

        try
        {
            _adapter.SaveImage(source.Pixels, size.Width, size.Height, inputPath);

            var preview = ImageImporter.Prepare(arranger, inputPath, ImageImportOptions.Default, _adapter).AsSuccess.Result;
            Assert.True(preview.CanCommit);
            preview.Commit();

            var actual = new IndexedImage(arranger);
            actual.ExportImage(outputPath, _adapter);

            IndexedImageAssert.AreEqual(indices, actual.Image, size.Width);

            using var expectedImage = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath);
            using var actualImage = SixLabors.ImageSharp.Image.Load<Rgba32>(outputPath);
            ImageRgba32Assert.AreEqual(expectedImage, actualImage);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    private void AssertRomPreserved(string codecName, int width, int height)
    {
        var (arranger, _) = CreateArranger(codecName, width, height);
        var dataSource = arranger.GetElement(0, 0)!.Value.Source;
        var rom = TestImageGenerator.RandomBytes((int)dataSource.Length, 8);
        dataSource.Write(BitAddress.Zero, rom);
        var path = TestPaths.CreateTempPath(".png");

        try
        {
            new IndexedImage(arranger).ExportImage(path, _adapter);

            var preview = ImageImporter.Prepare(arranger, path, ImageImportOptions.Default, _adapter).AsSuccess.Result;
            Assert.True(preview.CanCommit);
            Assert.Equal(0, preview.Report.ChangedPixelCount);
            preview.Commit();

            Assert.Equal(rom, dataSource.Read(BitAddress.Zero, rom.Length * 8));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private (ScatteredArranger Arranger, Palette Palette) CreateArranger(string codecName, int width, int height)
    {
        var prototype = _fixture.CodecFactory.CreateCodec(codecName, new System.Drawing.Size(width, height))!;
        var palette = TestImageGenerator.CreateDistinctPalette(prototype.ColorDepth);
        var elements = prototype.Layout == ImageLayout.Single ? 1 : 2;

        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, elements, elements,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height, palette));

        return (arranger, palette);
    }
}
