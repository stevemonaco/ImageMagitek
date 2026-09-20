using System.Collections.Generic;
using ImageMagitek;
using TileShop.Shared.Services;

namespace TileShop.Shared.Models;

/// <summary>
/// State the user changes while using the app, persisted across sessions in the user's profile
/// </summary>
public sealed class UserPreferences
{
    public ThemeStyle Theme { get; set; } = ThemeStyle.Dark;
    public List<string> RecentProjectFiles { get; set; } = [];
    public bool EnableArrangerSymmetryTools { get; set; }
    public GridPreferences Grid { get; set; } = new();
    public AddPalettePreferences AddPalette { get; set; } = new();
    public AddArrangerPreferences AddArranger { get; set; } = new();
}

/// <summary>
/// Grid colors as #AARRGGBB hex strings
/// </summary>
public sealed record GridPreferences(
    string LineColor = GridPreferences.DefaultLineColor,
    string PrimaryColor = GridPreferences.DefaultPrimaryColor,
    string SecondaryColor = GridPreferences.DefaultSecondaryColor)
{
    public const string DefaultLineColor = "#C4CC8484";
    public const string DefaultPrimaryColor = "#00000000";
    public const string DefaultSecondaryColor = "#19808080";
}

public sealed record AddPalettePreferences(
    string ColorModel = "RGBA32",
    bool ZeroIndexTransparent = true);

public sealed record AddArrangerPreferences(
    PixelColorType ColorType = PixelColorType.Indexed,
    ElementLayout Layout = ElementLayout.Tiled,
    int TiledArrangerElementWidth = 16,
    int TiledArrangerElementHeight = 8,
    int TiledElementPixelWidth = 8,
    int TiledElementPixelHeight = 8,
    int SingleArrangerPixelWidth = 256,
    int SingleArrangerPixelHeight = 256);
