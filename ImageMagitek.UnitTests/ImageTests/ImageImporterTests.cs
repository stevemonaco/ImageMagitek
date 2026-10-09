using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.PluginSample;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public class ImageImporterTests
{
    private static readonly ColorRgba32 _black = new(0, 0, 0, 255);
    private static readonly ColorRgba32 _white = new(255, 255, 255, 255);
    private static readonly ColorRgba32 _red = new(255, 0, 0, 255);
    private static readonly ColorRgba32 _blue = new(0, 0, 255, 255);
    private static readonly ColorRgba32 _green = new(0, 255, 0, 255);
    private static readonly ColorRgba32[] _colors = [_black, _white, _red, _blue];

    private static readonly ImageImportOptions _nearest = new(ColorMatchStrategy.Nearest);

    /// <summary>Two 8x8 8bpp elements side by side (16x8 pixels) sharing one palette, all pixels initially index 0</summary>
    private static ScatteredArranger CreateIndexedArranger(Palette palette) =>
        ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => new Psx8BppCodec(palette, 8, 8));

    private static DecodedImage CreateImage(int width, int height, Func<int, int, ColorRgba32> pixel)
    {
        var pixels = new ColorRgba32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);

        return new DecodedImage(pixels, width, height);
    }

    private static ImageImportPreview Prepare(Arranger arranger, DecodedImage image, ImageImportOptions? options = null) =>
        ImageImporter.Prepare(arranger, image, options ?? ImageImportOptions.Default).AsSuccess.Result;

    private static byte[] ResultRow(ImageImportPreview preview, int y) => preview.ResultIndexed!.GetPixelRowSpan(y).ToArray();

    [Fact]
    public void Prepare_SmallerImage_LeavesUncoveredPixelsUnchanged()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(8, 8, (_, _) => _red);

        var preview = Prepare(arranger, image);

        Assert.Equal(16, preview.Report.Width);
        Assert.Equal(8, preview.Report.Height);
        Assert.Equal(64, preview.Report.ChangedPixelCount);
        Assert.All(ResultRow(preview, 0)[..8], x => Assert.Equal(2, x));
        Assert.All(ResultRow(preview, 0)[8..], x => Assert.Equal(0, x));
        Assert.Equal(ImportPixelState.Unchanged, preview.Report.PixelStates[8]);
    }

    [Fact]
    public void Prepare_LargerImage_IsCropped()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(20, 12, (x, y) => x >= 16 || y >= 8 ? _green : _red);

        var preview = Prepare(arranger, image);

        Assert.True(preview.CanCommit);
        Assert.Equal(128, preview.Report.ChangedPixelCount);
        Assert.All(preview.ResultIndexed!.Image, x => Assert.Equal(2, x));
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(-3, -1)]
    public void Prepare_Offset_PlacesImageInArrangerCoordinates(int offsetX, int offsetY)
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(8, 4, (x, y) => x == 0 && y == 0 ? _blue : _red);

        var preview = ImageImporter.Prepare(arranger, image, ImageImportOptions.Default, new Point(offsetX, offsetY)).AsSuccess.Result;
        var result = preview.ResultIndexed!;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int sx = x - offsetX, sy = y - offsetY;
                byte expected = sx is >= 0 and < 8 && sy is >= 0 and < 4 ? (sx == 0 && sy == 0 ? (byte)3 : (byte)2) : (byte)0;
                Assert.Equal(expected, result.GetPixel(x, y));
            }
        }

        Assert.Equal(new Point(offsetX, offsetY), preview.Offset);
    }

    [Fact]
    public void Prepare_Bounds_ClipsChanges()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(16, 8, (_, _) => _green);
        var bounds = new Rectangle(8, 0, 8, 8);

        var preview = ImageImporter.Prepare(arranger, CreateImage(8, 8, (_, _) => _red), ImageImportOptions.Default, new Point(8, 0), bounds).AsSuccess.Result;
        var unmatched = ImageImporter.Prepare(arranger, image, ImageImportOptions.Default, Point.Empty, bounds).AsSuccess.Result;

        Assert.Equal(64, preview.Report.ChangedPixelCount);
        Assert.All(ResultRow(preview, 0)[..8], x => Assert.Equal(0, x));
        Assert.All(ResultRow(preview, 0)[8..], x => Assert.Equal(2, x));
        Assert.Equal(bounds, preview.Bounds);
        Assert.Equal(64, unmatched.Report.UnmatchedPixelCount);
        Assert.Equal(new Point(8, 0), unmatched.Report.Unmatched.Single().FirstLocation);
    }

    [Fact]
    public void Prepare_SourceIndices_UsedWhenPaletteMatches_EvenWithDuplicateColors()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_black, _red, _black));
        var indices = Enumerable.Range(0, 128).Select(i => (byte)(i % 2 == 0 ? 2 : 0)).ToArray();
        var pngPalette = new[] { _black, _red, new ColorRgba32(0, 0, 0, 0) };
        var image = new DecodedImage(indices.Select(i => pngPalette[i]).ToArray(), 16, 8, indices, pngPalette);

        var preview = Prepare(arranger, image);

        Assert.True(preview.Report.UsedSourceIndices);
        Assert.Equal(indices, preview.ResultIndexed!.Image);
        Assert.Equal(64, preview.Report.ChangedPixelCount);
        Assert.EndsWith("palette indices read from PNG", preview.Report.ToSummary());
    }

    [Fact]
    public void ExportThenImport_SinglePalette_RoundTripsIndicesThroughPng()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_black, _red, _black));
        var current = new IndexedImage(arranger);
        for (int i = 0; i < current.Image.Length; i++)
            current.Image[i] = (byte)(i % 3);
        current.SaveImage();

        var adapter = new ImageSharpFileAdapter();
        var path = TestPaths.CreateTempPath(".png");
        try
        {
            adapter.SaveImage(new IndexedImage(arranger).Image, arranger, path);
            var preview = ImageImporter.Prepare(arranger, path, ImageImportOptions.Default, adapter).AsSuccess.Result;

            Assert.True(preview.Report.UsedSourceIndices);
            Assert.Equal(0, preview.Report.ChangedPixelCount);
            Assert.Equal(current.Image, preview.ResultIndexed!.Image);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportThenImport_ZeroIndexTransparent_WritesTransparentIndexZero()
    {
        var palette = ArrangerTestFactory.CreatePalette(_black, _red, _black);
        palette.ZeroIndexTransparent = true;
        var arranger = CreateIndexedArranger(palette);
        var current = new IndexedImage(arranger);
        for (int i = 0; i < current.Image.Length; i++)
            current.Image[i] = (byte)(i % 3);
        current.SaveImage();

        var adapter = new ImageSharpFileAdapter();
        var path = TestPaths.CreateTempPath(".png");
        try
        {
            adapter.SaveImage(new IndexedImage(arranger).Image, arranger, path);

            Assert.True(IndexedPngFile.TryRead(path, out _, out var pngPalette));
            Assert.Equal(0, pngPalette[0].A);
            Assert.Equal(255, pngPalette[1].A);

            var preview = ImageImporter.Prepare(arranger, path, ImageImportOptions.Default, adapter).AsSuccess.Result;
            Assert.True(preview.Report.UsedSourceIndices);
            Assert.Equal(current.Image, preview.ResultIndexed!.Image);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportThenImport_MultiplePalettes_KeepsEveryPaletteEntryAndRoundTrips()
    {
        var first = ArrangerTestFactory.CreatePalette(_black, _red, _black);
        var second = ArrangerTestFactory.CreatePalette(_black, _red, _blue);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (x, _) => new Psx8BppCodec(x == 0 ? first : second, 8, 8));
        var current = new IndexedImage(arranger);
        for (int i = 0; i < current.Image.Length; i++)
            current.Image[i] = (byte)(i % 3);
        current.SaveImage();

        var adapter = new ImageSharpFileAdapter();
        var path = TestPaths.CreateTempPath(".png");
        try
        {
            adapter.SaveImage(new IndexedImage(arranger).Image, arranger, path);

            Assert.True(IndexedPngFile.TryRead(path, out var pngIndices, out var pngPalette));
            Assert.Equal([_black, _red, _black, _black, _red, _blue], pngPalette);
            Assert.Equal(current.Image[8] + 3, pngIndices[8]);

            var preview = ImageImporter.Prepare(arranger, path, ImageImportOptions.Default, adapter).AsSuccess.Result;
            Assert.True(preview.Report.UsedSourceIndices);
            Assert.Equal(0, preview.Report.ChangedPixelCount);
            Assert.Equal(current.Image, preview.ResultIndexed!.Image);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Prepare_MultiplePalettes_IndexFromAnotherPalettesRange_IsColorMatched()
    {
        var first = ArrangerTestFactory.CreatePalette(_black, _red);
        var second = ArrangerTestFactory.CreatePalette(_red, _blue);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (x, _) => new Psx8BppCodec(x == 0 ? first : second, 8, 8));
        ColorRgba32[] pngPalette = [_black, _red, _red, _blue];
        var indices = Enumerable.Repeat((byte)1, 128).ToArray();
        var image = new DecodedImage(indices.Select(i => pngPalette[i]).ToArray(), 16, 8, indices, pngPalette);

        var preview = Prepare(arranger, image);

        Assert.True(preview.Report.UsedSourceIndices);
        Assert.All(ResultRow(preview, 0)[..8], x => Assert.Equal(1, x));
        Assert.All(ResultRow(preview, 0)[8..], x => Assert.Equal(0, x));
    }

    [Fact]
    public void Prepare_SourceIndices_IgnoredWhenPaletteDiffers()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var pngPalette = new[] { _red, _black };
        var indices = Enumerable.Repeat((byte)0, 128).ToArray();
        var image = new DecodedImage(indices.Select(i => pngPalette[i]).ToArray(), 16, 8, indices, pngPalette);

        var preview = Prepare(arranger, image);

        Assert.False(preview.Report.UsedSourceIndices);
        Assert.All(preview.ResultIndexed!.Image, x => Assert.Equal(2, x));
    }

    [Fact]
    public void Prepare_Exact_ColorsInPalette_ProducesIndicesAndChangedCount()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(16, 8, (x, y) => _colors[(x + y) % 4]);

        var preview = Prepare(arranger, image);
        var report = preview.Report;

        var expected = Enumerable.Range(0, 128).Select(i => (byte)((i % 16 + i / 16) % 4)).ToArray();
        Assert.Equal(expected, preview.ResultIndexed!.Image);
        Assert.Equal(expected.Count(x => x != 0), report.ChangedPixelCount);
        Assert.Equal(ImportPixelState.Unchanged, report.PixelStates[0]);
        Assert.Equal(ImportPixelState.Changed, report.PixelStates[1]);
        Assert.Empty(report.Substitutions);
        Assert.Empty(report.Unmatched);
        Assert.True(preview.CanCommit);
    }

    [Fact]
    public void Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        Prepare(arranger, CreateImage(16, 8, (_, _) => _red)).Commit();

        var image = CreateImage(16, 8, (_, y) => y == 0 ? _green : _red);
        var preview = Prepare(arranger, image);
        var report = preview.Report;

        Assert.False(preview.CanCommit);
        Assert.Equal(0, report.ChangedPixelCount);
        Assert.Equal(16, report.UnmatchedPixelCount);
        Assert.All(preview.ResultIndexed!.Image, x => Assert.Equal(2, x));
        Assert.Equal(ImportPixelState.Unmatched, report.PixelStates[0]);
        Assert.Equal(ImportPixelState.Unchanged, report.PixelStates[16]);

        var entry = Assert.Single(report.Unmatched);
        Assert.Equal(_green.Color, entry.Source.Color);
        Assert.Equal(16, entry.Count);
        Assert.Equal(new Point(0, 0), entry.FirstLocation);
        Assert.True(entry.NearestDistance > 0);
    }

    [Fact]
    public void Prepare_DuplicatePaletteColors_KeepsCurrentIndexOnExactTie()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_black, _red, _black));
        var current = new IndexedImage(arranger);
        Array.Fill(current.Image, (byte)2);
        current.SaveImage();

        var preview = Prepare(arranger, CreateImage(16, 8, (_, _) => _black));

        Assert.Equal(0, preview.Report.ChangedPixelCount);
        Assert.All(preview.ResultIndexed!.Image, x => Assert.Equal(2, x));
    }

    [Fact]
    public void Prepare_Nearest_SubstitutesNearestEntryAndReports()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var nearRed = new ColorRgba32(250, 5, 5, 255);
        var nearBlue = new ColorRgba32(5, 5, 250, 255);
        var image = CreateImage(16, 8, (_, y) => y switch { 0 => nearRed, 1 => nearBlue, _ => _black });

        var preview = Prepare(arranger, image, _nearest);
        var report = preview.Report;

        Assert.True(preview.CanCommit);
        Assert.Equal(32, report.ChangedPixelCount);
        Assert.Equal(32, report.SubstitutedPixelCount);
        Assert.All(preview.ResultIndexed!.GetPixelRowSpan(0).ToArray(), x => Assert.Equal(2, x));
        Assert.All(preview.ResultIndexed!.GetPixelRowSpan(1).ToArray(), x => Assert.Equal(3, x));
        Assert.Equal(ImportPixelState.Changed | ImportPixelState.Substituted, report.PixelStates[0]);

        Assert.Equal(2, report.Substitutions.Count);
        Assert.True(report.Substitutions[0].Distance >= report.Substitutions[1].Distance);
        var redEntry = report.Substitutions.Single(x => x.Source.Color == nearRed.Color);
        Assert.Equal(2, redEntry.Index);
        Assert.Equal(_red.Color, redEntry.Matched.Color);
        Assert.Equal(16, redEntry.Count);
        Assert.Equal(new Point(0, 0), redEntry.FirstLocation);
        Assert.Equal("32 of 128 pixels change · 2 colors substituted (32 pixels)", report.ToSummary());
    }

    [Fact]
    public void Prepare_Nearest_BeyondMaxDistance_IsUnmatched()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(16, 8, (_, _) => _green);

        var preview = Prepare(arranger, image, _nearest with { MaxDistance = 1 });

        Assert.False(preview.CanCommit);
        Assert.Equal(128, preview.Report.UnmatchedPixelCount);
    }

    [Theory]
    [InlineData(true, 0, 0, true)]
    [InlineData(true, 10, 10, true)]
    [InlineData(true, 10, 5, false)]
    [InlineData(false, 0, 0, false)]
    public void Prepare_MapTransparentToIndexZero_SkipsColorMatching(bool mapTransparent, byte alpha, byte threshold, bool expectMapped)
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(16, 8, (_, _) => new ColorRgba32(77, 88, 99, alpha));
        var options = new ImageImportOptions(MapTransparentToIndexZero: mapTransparent, AlphaThreshold: threshold);

        var preview = Prepare(arranger, image, options);

        Assert.Equal(expectMapped, preview.CanCommit);
        Assert.Equal(expectMapped ? 0 : 128, preview.Report.UnmatchedPixelCount);
        Assert.Empty(preview.Report.Substitutions);
    }

    [Fact]
    public void Prepare_LimitsEntriesToCodecColorDepth()
    {
        var palette = ArrangerTestFactory.CreatePalette(_colors);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1, (_, _) => new Nes1BppCodec(palette));
        var image = CreateImage(8, 8, (_, _) => _blue);

        var exact = Prepare(arranger, image);
        var nearest = Prepare(arranger, image, _nearest);

        Assert.False(exact.CanCommit);
        Assert.True(exact.Report.Unmatched.Single().NearestIndex < 2);
        Assert.True(nearest.CanCommit);
        Assert.All(nearest.ResultIndexed!.Image, x => Assert.True(x < 2));
    }

    [Fact]
    public void Prepare_MultiplePalettes_MatchesPerElementPalette()
    {
        var paletteA = ArrangerTestFactory.CreatePalette(_black, _red);
        var paletteB = ArrangerTestFactory.CreatePalette(_red, _black);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (x, _) => new Psx8BppCodec(x == 0 ? paletteA : paletteB, 8, 8));
        var image = CreateImage(16, 8, (_, _) => _red);

        var preview = Prepare(arranger, image);

        Assert.True(preview.CanCommit);
        Assert.All(preview.ResultIndexed!.GetPixelRowSpan(0)[..8].ToArray(), x => Assert.Equal(1, x));
        Assert.All(preview.ResultIndexed!.GetPixelRowSpan(0)[8..].ToArray(), x => Assert.Equal(0, x));
    }

    [Fact]
    public void Prepare_UndefinedElement_IsSkipped()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        arranger.ResetElement(1, 0);
        var image = CreateImage(16, 8, (_, _) => _red);

        var preview = Prepare(arranger, image);

        Assert.True(preview.CanCommit);
        Assert.Equal(64, preview.Report.ChangedPixelCount);
        Assert.All(preview.ResultIndexed!.GetPixelRowSpan(0)[8..].ToArray(), x => Assert.Equal(0, x));
        Assert.Equal(ImportPixelState.Unchanged, preview.Report.PixelStates[8]);
    }

    [Fact]
    public void Commit_WritesResultIntoArranger()
    {
        var arranger = CreateIndexedArranger(ArrangerTestFactory.CreatePalette(_colors));
        var image = CreateImage(16, 8, (x, y) => _colors[(x * y) % 4]);
        var preview = Prepare(arranger, image);

        preview.Commit();

        Assert.Equal(preview.ResultIndexed!.Image, new IndexedImage(arranger).Image);
    }

    [Fact]
    public void Prepare_Direct_CopiesPixelsAndCountsChanged()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Direct, 2, 1, (_, _) => new Rgba32TiledCodec());
        var image = CreateImage(16, 8, (x, y) => new ColorRgba32((byte)(x * 16), (byte)(y * 32), 0, 255));

        var preview = Prepare(arranger, image);
        preview.Commit();

        Assert.True(preview.CanCommit);
        Assert.Equal(128, preview.Report.ChangedPixelCount);
        Assert.Equal(image.Pixels.Select(x => x.Color), preview.ResultDirect!.Image.Select(x => x.Color));
        Assert.Equal(image.Pixels.Select(x => x.Color), new DirectImage(arranger).Image.Select(x => x.Color));
    }
}
