using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

public static class CodecTestHelpers
{
    public static IIndexedCodec CreateCodec(ICodecFactory factory, string codecName, int width, int height, Palette? palette = null)
    {
        var codec = (IIndexedCodec)factory.CreateCodec(codecName, new Size(width, height))!;
        if (palette is not null)
            codec.Palette = palette;

        return codec;
    }

    public static IDirectCodec CreateDirectCodec(ICodecFactory factory, string codecName, int width, int height) =>
        (IDirectCodec)factory.CreateCodec(codecName, new Size(width, height))!;

    public static ArrangerElement CreateElement(IGraphicsCodec codec) =>
        new(0, 0, new MemoryDataSource("test", (codec.StorageSize + 7) / 8), BitAddress.Zero, codec);

    public static byte[,] Decode(IIndexedCodec codec, in ArrangerElement el, byte[] encoded) =>
        (byte[,])codec.DecodeElement(el, encoded).Clone();

    public static byte[] Encode(IIndexedCodec codec, in ArrangerElement el, byte[,] pixels) =>
        codec.EncodeElement(el, pixels).ToArray();

    public static ColorRgba32[,] Decode(IDirectCodec codec, in ArrangerElement el, byte[] encoded) =>
        (ColorRgba32[,])codec.DecodeElement(el, encoded).Clone();

    public static byte[] Encode(IDirectCodec codec, in ArrangerElement el, ColorRgba32[,] pixels) =>
        codec.EncodeElement(el, pixels).ToArray();

    /// <summary>
    /// Clears the bits of the final byte that lie past <paramref name="storageBits"/>.
    /// </summary>
    public static byte[] MaskToStorageSize(byte[] data, int storageBits)
    {
        var result = data[..((storageBits + 7) / 8)];
        var trailingBits = storageBits % 8;

        if (trailingBits != 0)
            result[^1] &= (byte)(0xFF << (8 - trailingBits));

        return result;
    }

    /// <summary>
    /// Theory cases for each codec at its default size, plus 16x8, 8x16 and 16x16 for resizable codecs and <paramref name="largeSizes"/> where named.
    /// The codecs default to every shipped XML codec in <see cref="CodecFixture.Shared"/>, and cases are kept only where <paramref name="include"/> allows.
    /// </summary>
    public static TheoryData<string, int, int> BuildCases(bool includeSquare, IDictionary<string, Size>? largeSizes = null,
        ICodecFactory? factory = null, IEnumerable<string>? codecNames = null, Func<string, int, int, bool>? include = null)
    {
        factory ??= CodecFixture.Shared.CodecFactory;
        codecNames ??= CodecFixture.Shared.XmlCodecNames;
        var data = new TheoryData<string, int, int>();

        foreach (var name in codecNames)
        {
            var codec = factory.CreateCodec(name)!;
            var sizes = new List<Size> { new(codec.DefaultWidth, codec.DefaultHeight) };

            if (codec.CanResize)
            {
                sizes.Add(new Size(16, 8));
                sizes.Add(new Size(8, 16));
                if (includeSquare)
                    sizes.Add(new Size(16, 16));
            }

            if (largeSizes is not null && largeSizes.TryGetValue(name, out var large))
                sizes.Add(large);

            foreach (var size in sizes.Distinct().Where(x => include?.Invoke(name, x.Width, x.Height) ?? true))
                data.Add(name, size.Width, size.Height);
        }

        return data;
    }

    public static void SaveIndices(Arranger arranger, byte[] indices)
    {
        var image = new IndexedImage(arranger);
        indices.CopyTo(image.Image, 0);
        image.SaveImage();
    }

    /// <summary>
    /// Saves random indices through a one-element arranger at <paramref name="offsetBits"/> and asserts that only the element's bits changed and the indices read back.
    /// </summary>
    public static void AssertSaveIsolated(IIndexedCodec codec, bool randomSentinel, int offsetBits)
    {
        codec.Palette = TestImageGenerator.CreateDistinctPalette(8);
        var length = (offsetBits + codec.StorageSize + 7) / 8 + 8;
        var before = randomSentinel ? TestImageGenerator.RandomBytes(length, 61) : Enumerable.Repeat((byte)0xFF, length).ToArray();

        var source = new MemoryDataSource("test", length);
        source.Write(BitAddress.Zero, before);

        var arranger = new ScatteredArranger("test", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, source, new BitAddress(offsetBits), codec), 0, 0);

        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(codec.Width, codec.Height, codec.ColorDepth, 62));
        SaveIndices(arranger, indices);

        var after = source.Read(BitAddress.Zero, length * 8);
        BitAssert.EqualOutside(before, after, offsetBits, codec.StorageSize);
        IndexedImageAssert.AreEqual(indices, new IndexedImage(arranger).Image, codec.Width);
    }

    public static void SaveColors(Arranger arranger, ColorRgba32[] colors)
    {
        var image = new DirectImage(arranger);
        colors.CopyTo(image.Image, 0);
        image.SaveImage();
    }

    public static string ToColorRows(ColorRgba32[,] pixels)
    {
        var sb = new StringBuilder();

        for (int y = 0; y < pixels.GetLength(0); y++)
        {
            for (int x = 0; x < pixels.GetLength(1); x++)
            {
                var c = pixels[y, x];
                if (x > 0)
                    sb.Append(' ');
                sb.Append($"{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}");
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    public static byte[] ReadAll(DataSource source) => source.Read(BitAddress.Zero, (int)source.Length * 8);

    public static string ToHex(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("X2"));
            sb.Append((i + 1) % 16 == 0 || i == data.Length - 1 ? '\n' : ' ');
        }

        return sb.ToString();
    }

    public static string ToIndexRows(byte[,] pixels, int colorDepth)
    {
        var sb = new StringBuilder();

        for (int y = 0; y < pixels.GetLength(0); y++)
        {
            for (int x = 0; x < pixels.GetLength(1); x++)
            {
                if (colorDepth <= 4)
                {
                    sb.Append(pixels[y, x].ToString("X1"));
                }
                else
                {
                    if (x > 0)
                        sb.Append(' ');
                    sb.Append(pixels[y, x].ToString("X2"));
                }
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
