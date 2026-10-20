using System;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Order in which a tilemap's entries are stored
/// <para>Linear stores rows left to right, top to bottom</para>
/// <para>Snes32 stores 32x32-entry screens left to right, then top to bottom, each screen stored linearly</para>
/// </summary>
public enum MapScreenLayout { Linear, Snes32 }

public static class MapScreenLayoutExtensions
{
    public const int ScreenSize = 32;

    /// <summary>
    /// The index of the entry that holds cell (<paramref name="x"/>, <paramref name="y"/>) of a map <paramref name="width"/> cells wide
    /// </summary>
    public static int EntryIndex(this MapScreenLayout layout, int x, int y, int width)
    {
        if (layout == MapScreenLayout.Linear || width <= ScreenSize)
            return y * width + x;

        var screensX = width / ScreenSize;
        var screen = y / ScreenSize * screensX + x / ScreenSize;
        return screen * ScreenSize * ScreenSize + y % ScreenSize * ScreenSize + x % ScreenSize;
    }

    /// <summary>
    /// Why a map of <paramref name="width"/> cells cannot use the layout, or null when it can
    /// </summary>
    public static string? FindWidthError(this MapScreenLayout layout, int width) =>
        layout == MapScreenLayout.Snes32 && width > ScreenSize && width % ScreenSize != 0
            ? $"A {nameof(MapScreenLayout.Snes32)} map wider than {ScreenSize} cells must be a multiple of {ScreenSize} cells wide, not {width}"
            : null;

    public static string ToXmlName(this MapScreenLayout layout) => layout switch
    {
        MapScreenLayout.Linear => "linear",
        MapScreenLayout.Snes32 => "snes32",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    public static MapScreenLayout FromXmlName(string? name) => name switch
    {
        null or "linear" => MapScreenLayout.Linear,
        "snes32" => MapScreenLayout.Snes32,
        _ => throw new ArgumentException($"Unknown map screen layout '{name}'", nameof(name))
    };
}
