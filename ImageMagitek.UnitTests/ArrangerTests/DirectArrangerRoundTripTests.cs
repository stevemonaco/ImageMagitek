using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Round-trips and neighbor isolation for direct-color codecs through <see cref="DirectImage"/>.
/// </summary>
[Collection("Codec")]
public class DirectArrangerRoundTripTests
{
    private const int _offsetBits = 5 * 8;

    private readonly CodecFixture _fixture;

    public DirectArrangerRoundTripTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> RoundTripCases => Cases(x => true);

    public static TheoryData<string, bool> IsolationCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var name in CodecFixture.Shared.DirectCodecNames)
            {
                data.Add(name, false);
                data.Add(name, true);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void ImageToDataToImage_RoundTrips(string codecName)
    {
        var arranger = CreateArranger(codecName);
        var size = arranger.ArrangerPixelSize;
        var colors = TestImageGenerator.Flatten(DirectCodecContractTests.RepresentableColors(codecName, size.Width, size.Height, 7));

        CodecTestHelpers.SaveColors(arranger, colors);

        ColorImageAssert.AreEqual(colors, new DirectImage(arranger).Image, size.Width);
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void DataToImageToData_PreservesRom(string codecName)
    {
        var arranger = CreateArranger(codecName);
        var dataSource = arranger.GetElement(0, 0)!.Value.Source;
        var rom = TestImageGenerator.RandomBytes((int)dataSource.Length, 8);
        dataSource.Write(BitAddress.Zero, rom);

        new DirectImage(arranger).SaveImage();

        Assert.Equal(rom, CodecTestHelpers.ReadAll(dataSource));
    }

    [Theory]
    [MemberData(nameof(IsolationCases))]
    public void SaveElement_ByteAligned_ChangesOnlyElementBits(string codecName, bool randomSentinel)
    {
        var codec = _fixture.CodecFactory.CreateCodec(codecName)!;
        var length = (_offsetBits + codec.StorageSize + 7) / 8 + 8;
        var before = randomSentinel ? TestImageGenerator.RandomBytes(length, 61) : Enumerable.Repeat((byte)0xFF, length).ToArray();

        var source = new MemoryDataSource("test", length);
        source.Write(BitAddress.Zero, before);

        var arranger = new ScatteredArranger("test", PixelColorType.Direct, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, source, new BitAddress(_offsetBits), codec), 0, 0);

        var colors = TestImageGenerator.Flatten(DirectCodecContractTests.RepresentableColors(codecName, codec.Width, codec.Height, 62));
        CodecTestHelpers.SaveColors(arranger, colors);

        var after = source.Read(BitAddress.Zero, length * 8);
        BitAssert.EqualOutside(before, after, _offsetBits, codec.StorageSize);
        ColorImageAssert.AreEqual(colors, new DirectImage(arranger).Image, codec.Width);
    }

    [Fact]
    public void SaveImage_SubRectangle_WritesItsPixelsAndKeepsTheRest()
    {
        var arranger = CreateRandomArranger();
        var size = arranger.ArrangerPixelSize;
        var elementSize = arranger.ElementPixelSize;
        var left = elementSize.Width / 2;
        var top = elementSize.Height / 2;
        var expected = new DirectImage(arranger).Image;

        var image = new DirectImage(arranger, left, top, elementSize.Width, elementSize.Height);
        TestImageGenerator.Flatten(TestImageGenerator.RandomColors(image.Width, image.Height, 41)).CopyTo(image.Image, 0);
        for (int y = 0; y < image.Height; y++)
            Array.Copy(image.Image, y * image.Width, expected, (y + top) * size.Width + left, image.Width);

        image.SaveImage();

        ColorImageAssert.AreEqual(expected, new DirectImage(arranger).Image, size.Width);
    }

    [Fact]
    public void AnalyzeSaveConflicts_SubRectangle_ReportsOnlyEditedElement()
    {
        var arranger = CreateRandomArranger();
        var elementSize = arranger.ElementPixelSize;
        var image = new DirectImage(arranger, elementSize.Width / 2, elementSize.Height / 2, elementSize.Width, elementSize.Height);

        Assert.Empty(arranger.AnalyzeSaveConflicts(image).GetModifiedElements());

        var pixel = image.GetPixel(image.Width - 1, image.Height - 1);
        image.SetPixel(image.Width - 1, image.Height - 1, new ColorRgba32((byte)~pixel.R, pixel.G, pixel.B, pixel.A));
        var modified = Assert.Single(arranger.AnalyzeSaveConflicts(image).GetModifiedElements());

        Assert.Equal((1, 1), (modified.ElementX, modified.ElementY));
    }

    private ScatteredArranger CreateRandomArranger()
    {
        var arranger = CreateArranger("N64 Rgba32");
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 40));
        return arranger;
    }

    private ScatteredArranger CreateArranger(string codecName) =>
        ArrangerTestFactory.CreateArranger(PixelColorType.Direct, 2, 2, (_, _) => _fixture.CodecFactory.CreateCodec(codecName)!);

    private static TheoryData<string> Cases(Func<string, bool> include)
    {
        var data = new TheoryData<string>();
        foreach (var name in CodecFixture.Shared.DirectCodecNames.Where(include))
            data.Add(name);
        return data;
    }
}
