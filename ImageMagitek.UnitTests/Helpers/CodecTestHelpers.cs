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

    public static ArrangerElement CreateElement(IGraphicsCodec codec) =>
        new(0, 0, new MemoryDataSource("test", (codec.StorageSize + 7) / 8), BitAddress.Zero, codec);

    public static byte[,] Decode(IIndexedCodec codec, in ArrangerElement el, byte[] encoded) =>
        (byte[,])codec.DecodeElement(el, encoded).Clone();

    public static byte[] Encode(IIndexedCodec codec, in ArrangerElement el, byte[,] pixels) =>
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
    /// Theory cases for every shipped XML codec at its default size, plus 16x8, 8x16 and 16x16 for resizable codecs and <paramref name="largeSizes"/> where named.
    /// </summary>
    public static TheoryData<string, int, int> BuildCases(bool includeSquare, IDictionary<string, Size>? largeSizes = null)
    {
        var fixture = CodecFixture.Shared;
        var data = new TheoryData<string, int, int>();

        foreach (var name in fixture.XmlCodecNames)
        {
            var codec = fixture.CodecFactory.CreateCodec(name)!;
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

            foreach (var size in sizes.Distinct())
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
