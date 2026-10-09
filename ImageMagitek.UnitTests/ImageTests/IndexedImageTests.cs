using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
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

    [Fact]
    public void ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing()
    {
        var elementBytes = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 4bpp", 8, 8).StorageSize / 8;
        var length = elementBytes * 2 + elementBytes / 2;
        var source = new MemoryDataSource("test", length);
        var rom = TestImageGenerator.RandomBytes(length, 101);
        source.Write(BitAddress.Zero, rom);
        var arranger = CreateArranger("SNES 4bpp", 2, 2, source);

        var image = new IndexedImage(arranger);
        Assert.All(image.Image.AsSpan(16 * 8).ToArray(), x => Assert.Equal(0, x));

        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(16, 16, 4, 102));
        CodecTestHelpers.SaveIndices(arranger, indices);

        Assert.Equal(length, source.Length);
        Assert.Equal(rom.AsSpan(elementBytes * 2).ToArray(), CodecTestHelpers.ReadAll(source).AsSpan(elementBytes * 2).ToArray());
        IndexedImageAssert.AreEqual(indices.AsSpan(0, 16 * 8).ToArray(), new IndexedImage(arranger).Image.AsSpan(0, 16 * 8).ToArray(), 16);
    }

    [Fact]
    public void DirectArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing()
    {
        var codecName = "Rgb24 Tiled";
        var prototype = _fixture.CodecFactory.CreateCodec(codecName)!;
        var elementBytes = prototype.StorageSize / 8;
        var length = elementBytes + elementBytes / 2;
        var source = new MemoryDataSource("test", length);
        var rom = TestImageGenerator.RandomBytes(length, 111);
        source.Write(BitAddress.Zero, rom);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Direct, 2, 1, (_, _) => _fixture.CodecFactory.CreateCodec(codecName)!, source);

        var image = new DirectImage(arranger);
        for (int y = 0; y < prototype.Height; y++)
            for (int x = prototype.Width; x < prototype.Width * 2; x++)
                Assert.Equal(0u, image.GetPixel(x, y).Color);

        image.SaveImage();

        Assert.Equal(length, source.Length);
        Assert.Equal(rom, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void TrySetPalette_ClonedArranger_LeavesOriginalPalettes()
    {
        var original = CreateArranger("SNES 4bpp", 2, 1);
        var originalPalette = PaletteAt(original, 0, 0);
        var otherPalette = TestImageGenerator.CreateDistinctPalette(4);
        var clone = (ScatteredArranger)original.CloneArranger();

        Assert.True(new IndexedImage(clone).TrySetPalette(0, 0, otherPalette, _fixture.CodecFactory).HasSucceeded);

        Assert.Same(otherPalette, PaletteAt(clone, 0, 0));
        Assert.Same(originalPalette, PaletteAt(original, 0, 0));
        Assert.Same(originalPalette, PaletteAt(original, 1, 0));
    }

    [Fact]
    public void TrySetPalette_CodecSharedByTwoCells_ChangesOnlyTarget()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);
        var otherPalette = TestImageGenerator.CreateDistinctPalette(4);
        var shared = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 4bpp", 8, 8, palette);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => shared);

        Assert.True(new IndexedImage(arranger).TrySetPalette(8, 0, otherPalette, _fixture.CodecFactory).HasSucceeded);

        Assert.Same(otherPalette, PaletteAt(arranger, 1, 0));
        Assert.Same(palette, PaletteAt(arranger, 0, 0));
        Assert.Same(palette, shared.Palette);
    }

    [Fact]
    public void TrySetPalette_ElementsCopiedFromSequential_LeavesSequentialPalette()
    {
        var codec = _fixture.CodecFactory.CreateCodec("SNES 4bpp")!;
        var palette = TestImageGenerator.CreateDistinctPalette(codec.ColorDepth);
        var otherPalette = TestImageGenerator.CreateDistinctPalette(codec.ColorDepth);
        var source = new MemoryDataSource("test", 4 * codec.StorageSize / 8);
        var sequential = new SequentialArranger(2, 2, source, palette, _fixture.CodecFactory, codec);
        var sequentialPalette = ((IIndexedCodec)sequential.ActiveCodec).Palette;

        var scattered = new ScatteredArranger("dest", PixelColorType.Indexed, ElementLayout.Tiled, 2, 1, codec.Width, codec.Height);
        var copy = sequential.CopyElements(0, 0, 2, 1);
        Assert.True(ElementCopier.CopyElements(copy, scattered, new System.Drawing.Point(0, 0), new System.Drawing.Point(0, 0), 2, 1).HasSucceeded);

        Assert.True(new IndexedImage(scattered).TrySetPalette(0, 0, otherPalette, _fixture.CodecFactory).HasSucceeded);

        Assert.Same(otherPalette, PaletteAt(scattered, 0, 0));
        Assert.Same(sequentialPalette, PaletteAt(scattered, 1, 0));
        Assert.Same(sequentialPalette, ((IIndexedCodec)sequential.ActiveCodec).Palette);
        Assert.Same(sequentialPalette, PaletteAt(sequential, 0, 0));
    }

    [Fact]
    public void PaintIndex_DuplicateColor_WritesChosenIndex()
    {
        var palette = CreateDuplicatePalette();
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 4bpp", 8, 8, palette));
        var image = new IndexedImage(arranger);

        var result = image.TryPaintIndex(2, 3, palette, 9);

        Assert.True(result.HasSucceeded);
        Assert.Equal(9, result.AsSuccess.Result);
        Assert.Equal(9, image.GetPixel(2, 3));
    }

    [Fact]
    public void PaintIndex_OtherPalette_WritesExactColorIndex()
    {
        var sourcePalette = TestImageGenerator.CreateDistinctPalette(4);
        var elementPalette = CreateDuplicatePalette();
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 4bpp", 8, 8, elementPalette));
        var image = new IndexedImage(arranger);

        var result = image.TryPaintIndex(2, 3, sourcePalette, 5);

        Assert.True(result.HasSucceeded);
        Assert.Equal(1, result.AsSuccess.Result);
        Assert.Equal(1, image.GetPixel(2, 3));
    }

    private static Palette CreateDuplicatePalette()
    {
        var distinct = TestImageGenerator.CreateDistinctPalette(4);
        var colors = Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(200 + i), 7, 7, 255)).ToArray();
        colors[1] = distinct[5];
        colors[9] = distinct[5];
        return ArrangerTestFactory.CreatePalette(colors);
    }

    private static Palette? PaletteAt(Arranger arranger, int x, int y) =>
        (arranger.GetElement(x, y)?.Codec as IIndexedCodec)?.Palette;

    private ScatteredArranger CreateArranger(string codecName, int elementsX, int elementsY, DataSource? source = null)
    {
        var palette = TestImageGenerator.CreateDistinctPalette(4);
        return ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, elementsX, elementsY,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, 8, 8, palette), source);
    }

}
