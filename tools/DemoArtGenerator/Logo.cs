using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DemoArtGenerator;

/// <summary>
/// The TileShop app icon, a shopfront with a tiled window, kept as a 16x16 pixel map.
/// </summary>
public static class Logo
{
    private static readonly string[] _pixels =
    [
        "................",
        ".00000000000000.",
        "0117711771177110",
        "0117711771177110",
        "0117711771177110",
        "0000000000000000",
        ".04444444444440.",
        ".04000000444440.",
        ".04011330400040.",
        ".04011330409040.",
        ".04033110409040.",
        ".04033110409040.",
        ".0400000040a040.",
        ".04444444409040.",
        ".00000000000000.",
        "................",
    ];

    private static readonly Dictionary<char, string> _colors = new()
    {
        ['0'] = "15112a",
        ['1'] = "009cf0",
        ['3'] = "8fdcff",
        ['4'] = "ff9933",
        ['7'] = "ffffff",
        ['9'] = "3a3f58",
        ['a'] = "ffe14d",
    };

    private static readonly int[] _iconSizes = [16, 24, 32, 48, 64, 128, 256];

    /// <summary>
    /// Writes a Windows icon with PNG frames scaled up from the pixel map by whole factors.
    /// </summary>
    public static void WriteIcon(string path)
    {
        var frames = _iconSizes.Select(size =>
        {
            using var image = Render(size / 16);
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return (Size: size, Png: stream.ToArray());
        }).ToList();

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)frames.Count);

        var offset = 6 + 16 * frames.Count;
        foreach (var (size, png) in frames)
        {
            writer.Write((byte)(size % 256));
            writer.Write((byte)(size % 256));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in frames)
            writer.Write(png);
    }

    /// <summary>
    /// Writes the pixel map as an SVG with one rectangle per horizontal run of a color.
    /// </summary>
    public static void WriteSvg(string path)
    {
        var svg = new StringBuilder();
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\" width=\"256\" height=\"256\" shape-rendering=\"crispEdges\">");

        for (int y = 0; y < _pixels.Length; y++)
        {
            for (int x = 0; x < _pixels[y].Length;)
            {
                var c = _pixels[y][x];
                var run = 1;
                while (x + run < _pixels[y].Length && _pixels[y][x + run] == c)
                    run++;

                if (c != '.')
                    svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{run}\" height=\"1\" fill=\"#{_colors[c]}\"/>");
                x += run;
            }
        }

        svg.AppendLine("</svg>");
        File.WriteAllText(path, svg.ToString());
    }

    public static Image<Rgba32> Render(int scale)
    {
        var image = new Image<Rgba32>(16 * scale, 16 * scale);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (_pixels[y / scale][x / scale] is var c and not '.')
                    image[x, y] = Rgba32.ParseHex(_colors[c]);

        return image;
    }
}
