using System;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public sealed class IndexedPngFileTests : IDisposable
{
    private readonly string _path = TestPaths.CreateTempPath(".png");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static readonly ColorRgba32[] _duplicatePalette =
    [
        new(0, 0, 0, 0),
        new(255, 0, 0, 255),
        new(0, 0, 0, 255),
        new(255, 0, 0, 255),
    ];

    [Fact]
    public void WriteThenRead_DuplicatePaletteColors_PreservesIndices()
    {
        var indices = Enumerable.Range(0, 7 * 5).Select(i => (byte)(i % 4)).ToArray();

        IndexedPngFile.Write(_path, indices, 7, 5, _duplicatePalette);

        Assert.True(IndexedPngFile.TryRead(_path, out var readIndices, out var palette));
        Assert.Equal(indices, readIndices);
        Assert.Equal(_duplicatePalette.Select(x => x.Color), palette.Select(x => x.Color));
    }

    [Fact]
    public void Write_ImageSharpLoadsExpectedColors()
    {
        var indices = Enumerable.Range(0, 7 * 5).Select(i => (byte)(i % 4)).ToArray();

        IndexedPngFile.Write(_path, indices, 7, 5, _duplicatePalette);

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(_path);
        Assert.Equal(7, image.Width);
        Assert.Equal(5, image.Height);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                var expected = _duplicatePalette[indices[y * 7 + x]];
                var actual = image[x, y];
                Assert.Equal((expected.R, expected.G, expected.B, expected.A), (actual.R, actual.G, actual.B, actual.A));
            }
        }
    }

    [Fact]
    public void TryRead_FourBitImageSharpPng_ReadsIndices()
    {
        var colors = Enumerable.Range(0, 16).Select(i => new Rgba32((byte)(i * 16), (byte)(255 - i * 16), (byte)(i * 7), 255)).ToArray();
        using (var image = new Image<Rgba32>(13, 9))
        {
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 13; x++)
                    image[x, y] = colors[(x * 3 + y) % 16];

            image.SaveAsPng(_path, new PngEncoder { ColorType = PngColorType.Palette, BitDepth = PngBitDepth.Bit4 });
        }

        Assert.True(IndexedPngFile.TryRead(_path, out var indices, out var palette));
        Assert.Equal(13 * 9, indices.Length);

        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 13; x++)
            {
                var expected = colors[(x * 3 + y) % 16];
                var actual = palette[indices[y * 13 + x]];
                Assert.Equal((expected.R, expected.G, expected.B), (actual.R, actual.G, actual.B));
            }
        }
    }

    [Fact]
    public void TryRead_RgbaPng_ReturnsFalse()
    {
        using (var image = new Image<Rgba32>(4, 4))
            image.SaveAsPng(_path, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });

        Assert.False(IndexedPngFile.TryRead(_path, out _, out _));
    }
}
