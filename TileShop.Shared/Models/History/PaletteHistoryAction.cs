using System;
using System.Linq;
using ImageMagitek.Colors;

namespace TileShop.Shared.Models;

/// <summary>
/// Palette edit that is restored from a snapshot of the palette taken after it ran
/// </summary>
public sealed class PaletteHistoryAction : HistoryAction
{
    public override string Name { get; }
    public PaletteSnapshot After { get; }

    public PaletteHistoryAction(string name, PaletteSnapshot after)
    {
        Name = name;
        After = after;
    }
}

/// <summary>
/// Copy of a palette's working colors, sources and settings at a point in time
/// </summary>
public sealed record PaletteSnapshot(IColor[] Colors, IColorSource[] Sources, bool ZeroIndexTransparent, ColorModel ColorModel)
{
    public static PaletteSnapshot Capture(Palette palette, IColorFactory colorFactory)
    {
        var colors = Enumerable.Range(0, palette.Entries)
            .Select(i => colorFactory.CloneColor(palette.GetForeignColor(i)))
            .ToArray();
        var sources = palette.ColorSources.Select(x => CloneSource(x, colorFactory)).ToArray();

        return new PaletteSnapshot(colors, sources, palette.ZeroIndexTransparent, palette.ColorModel);
    }

    /// <summary>
    /// Returns the palette to this snapshot. Sources are cloned again since the palette mutates them.
    /// </summary>
    public void Restore(Palette palette, IColorFactory colorFactory)
    {
        palette.SetColorModel(ColorModel, Sources.Select(x => CloneSource(x, colorFactory)));

        palette.ZeroIndexTransparent = ZeroIndexTransparent;

        for (int i = 0; i < Colors.Length; i++)
            palette.SetForeignColor(i, colorFactory.CloneColor(Colors[i]));
    }

    public static IColorSource CloneSource(IColorSource source, IColorFactory colorFactory) => source switch
    {
        FileColorSource file => new FileColorSource(file.Offset, file.Endian),
        ProjectNativeColorSource native => new ProjectNativeColorSource(native.Value),
        ProjectForeignColorSource foreign => new ProjectForeignColorSource(colorFactory.CloneColor(foreign.Value)),
        _ => throw new NotSupportedException($"Color source of type '{source.GetType()}' cannot be cloned")
    };
}
