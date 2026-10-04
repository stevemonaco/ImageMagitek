using System;
using System.IO;
using ImageMagitek.Codec;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

[Collection("Codec")]
public class IndexedImageTests
{
    private readonly CodecFixture _fixture;

    public IndexedImageTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, MirrorOperation, RotationOperation> MirrorRotationCases
    {
        get
        {
            var data = new TheoryData<string, MirrorOperation, RotationOperation>();

            foreach (var codecName in new[] { "SNES 4bpp", "GBA4bpp Pattern" })
                foreach (var mirror in Enum.GetValues<MirrorOperation>())
                    foreach (var rotation in Enum.GetValues<RotationOperation>())
                        data.Add(codecName, mirror, rotation);

            return data;
        }
    }

    [Theory]
    [InlineData("SNES 4bpp")]
    [InlineData("GBA4bpp Pattern")]
    public void PartialUnalignedEdit_LeavesOtherPixelsAndBytesUnchanged(string codecName)
    {
        const int left = 3, top = 5, width = 10, height = 7;
        var arranger = CreateArranger(codecName, 3, 3);
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 71));

        var bytesBefore = CodecTestHelpers.ReadAll(source);
        var pixelsBefore = new IndexedImage(arranger).Image;
        var edit = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(width, height, 4, 72));

        var partial = new IndexedImage(arranger, left, top, width, height);
        edit.CopyTo(partial.Image, 0);
        partial.SaveImage();

        var full = new IndexedImage(arranger);
        var expected = (byte[])pixelsBefore.Clone();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                expected[(y + top) * full.Width + x + left] = edit[y * width + x];

        IndexedImageAssert.AreEqual(expected, full.Image, full.Width);

        var bytesAfter = CodecTestHelpers.ReadAll(source);
        var editRect = new System.Drawing.Rectangle(left, top, width, height);
        foreach (var el in arranger.EnumerateElements())
        {
            var element = el!.Value;
            if (editRect.IntersectsWith(new System.Drawing.Rectangle(element.X1, element.Y1, element.Width, element.Height)))
                continue;

            var start = (int)element.SourceAddress.ByteOffset;
            var length = element.Codec.StorageSize / 8;
            Assert.Equal(bytesBefore.AsSpan(start, length).ToArray(), bytesAfter.AsSpan(start, length).ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(MirrorRotationCases))]
    public void MirrorAndRotation_RoundTrip(string codecName, MirrorOperation mirror, RotationOperation rotation)
    {
        var arranger = CreateArranger(codecName, 2, 2);
        foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
            arranger.SetElement(arranger.GetElement(x, y)!.Value.WithMirror(mirror).WithRotation(rotation), x, y);

        var source = arranger.GetElement(0, 0)!.Value.Source;
        var rom = TestImageGenerator.RandomBytes((int)source.Length, 81);
        source.Write(BitAddress.Zero, rom);

        new IndexedImage(arranger).SaveImage();
        Assert.Equal(rom, CodecTestHelpers.ReadAll(source));

        var size = arranger.ArrangerPixelSize;
        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(size.Width, size.Height, 4, 82));
        CodecTestHelpers.SaveIndices(arranger, indices);

        IndexedImageAssert.AreEqual(indices, new IndexedImage(arranger).Image, size.Width);
    }

    [Fact]
    public void FileDataSource_RoundTrip()
    {
        var path = TestPaths.CreateTempPath(".bin");
        var codecName = "SNES 4bpp";
        var elementBytes = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, 8, 8).StorageSize / 8;
        var rom = TestImageGenerator.RandomBytes(elementBytes * 4 + 16, 91);
        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(16, 16, 4, 92));

        try
        {
            File.WriteAllBytes(path, rom);

            using (var source = new FileDataSource("test", path))
            {
                var arranger = CreateArranger(codecName, 2, 2, source);
                new IndexedImage(arranger).SaveImage();
            }

            Assert.Equal(rom, File.ReadAllBytes(path));

            using (var source = new FileDataSource("test", path))
            {
                var arranger = CreateArranger(codecName, 2, 2, source);
                CodecTestHelpers.SaveIndices(arranger, indices);
            }

            using (var source = new FileDataSource("test", path))
            {
                var arranger = CreateArranger(codecName, 2, 2, source);
                IndexedImageAssert.AreEqual(indices, new IndexedImage(arranger).Image, 16);
            }

            Assert.Equal(rom.AsSpan(elementBytes * 4).ToArray(), File.ReadAllBytes(path).AsSpan(elementBytes * 4).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private ScatteredArranger CreateArranger(string codecName, int elementsX, int elementsY, DataSource? source = null)
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);
        return ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, elementsX, elementsY,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, 8, 8, palette), source);
    }

}
