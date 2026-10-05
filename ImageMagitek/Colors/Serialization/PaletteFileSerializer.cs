using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ImageMagitek.Colors.Serialization;

/// <summary>
/// Reads and writes palette interchange files: JASC (.pal) and GIMP (.gpl)
/// </summary>
public static class PaletteFileSerializer
{
    private const string _jascHeader = "JASC-PAL";
    private const string _gplHeader = "GIMP Palette";

    public static string WriteJasc(IReadOnlyList<ColorRgba32> colors)
    {
        var builder = new StringBuilder();
        builder.Append(_jascHeader).Append("\r\n");
        builder.Append("0100\r\n");
        builder.Append(colors.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");

        foreach (var color in colors)
            builder.Append(CultureInfo.InvariantCulture, $"{color.R} {color.G} {color.B}\r\n");

        return builder.ToString();
    }

    public static string WriteGpl(string name, IReadOnlyList<ColorRgba32> colors)
    {
        var builder = new StringBuilder();
        builder.Append(_gplHeader).Append('\n');
        builder.Append("Name: ").Append(name).Append('\n');
        builder.Append("Columns: 16\n");
        builder.Append("#\n");

        for (int i = 0; i < colors.Count; i++)
        {
            var color = colors[i];
            builder.Append(CultureInfo.InvariantCulture, $"{color.R,3} {color.G,3} {color.B,3}\tIndex {i}\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads a JASC or GIMP palette, detected by its header
    /// </summary>
    public static MagitekResult<IReadOnlyList<ColorRgba32>> Read(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        int headerLine = 0;
        while (headerLine < lines.Length && string.IsNullOrWhiteSpace(lines[headerLine]))
            headerLine++;

        if (headerLine == lines.Length)
            return new MagitekResult<IReadOnlyList<ColorRgba32>>.Failed("The palette file is empty");

        var header = lines[headerLine].Trim();

        if (header == _jascHeader)
            return ReadJasc(lines, headerLine + 1);
        if (header == _gplHeader)
            return ReadGpl(lines, headerLine + 1);

        return new MagitekResult<IReadOnlyList<ColorRgba32>>.Failed($"Line {headerLine + 1}: expected a '{_jascHeader}' or '{_gplHeader}' header");
    }

    private static MagitekResult<IReadOnlyList<ColorRgba32>> ReadJasc(string[] lines, int start)
    {
        if (start >= lines.Length || lines[start].Trim() != "0100")
            return Fail(start, "expected JASC version '0100'");

        if (start + 1 >= lines.Length || !int.TryParse(lines[start + 1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            return Fail(start + 1, "expected the number of colors");

        var colors = new List<ColorRgba32>(count);
        int line = start + 2;

        while (colors.Count < count)
        {
            if (line >= lines.Length)
                return Fail(line, $"expected {count} colors but found {colors.Count}");

            if (!TryParseRgb(lines[line], out var color))
                return Fail(line, $"'{lines[line].Trim()}' is not a color as 'R G B'");

            colors.Add(color);
            line++;
        }

        return new MagitekResult<IReadOnlyList<ColorRgba32>>.Success(colors);
    }

    private static MagitekResult<IReadOnlyList<ColorRgba32>> ReadGpl(string[] lines, int start)
    {
        var colors = new List<ColorRgba32>();

        for (int line = start; line < lines.Length; line++)
        {
            var trimmed = lines[line].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith("Name:") || trimmed.StartsWith("Columns:"))
                continue;

            if (!TryParseRgb(trimmed, out var color))
                return Fail(line, $"'{trimmed}' is not a color as 'R G B'");

            colors.Add(color);
        }

        return new MagitekResult<IReadOnlyList<ColorRgba32>>.Success(colors);
    }

    private static bool TryParseRgb(string line, out ColorRgba32 color)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        color = default;

        if (parts.Length < 3)
            return false;

        if (!byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var b))
            return false;

        color = new ColorRgba32(r, g, b, 255);
        return true;
    }

    private static MagitekResult<IReadOnlyList<ColorRgba32>> Fail(int lineIndex, string reason) =>
        new MagitekResult<IReadOnlyList<ColorRgba32>>.Failed($"Line {lineIndex + 1}: {reason}");
}
