using System.Collections.Generic;
using ImageMagitek;
using ImageMagitek.Colors;
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
    public NumericBase JumpToOffsetBase { get; set; } = NumericBase.Hexadecimal;
    public GridPreferences Grid { get; set; } = new();
    public AddPalettePreferences AddPalette { get; set; } = new();
    public AddArrangerPreferences AddArranger { get; set; } = new();
    public ImportImagePreferences ImportImage { get; set; } = new();

    /// <summary>
    /// Name of the NES master palette in _palettes, or null to use appsettings.json
    /// </summary>
    public string? NesPalette { get; set; }
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
    public const string DefaultPrimaryColor = "#FFC0C0C0";
    public const string DefaultSecondaryColor = "#FF808080";
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

/// <summary>
/// Last choices made in the import image dialog
/// </summary>
/// <param name="MapTransparentToIndexZero">Null follows the palette's ZeroIndexTransparent setting</param>
/// <param name="OnionSkinOpacity">Blend from the current arranger (0) to the import (1)</param>
public sealed record ImportImagePreferences(
    ColorMatchStrategy MatchStrategy = ColorMatchStrategy.Exact,
    bool? MapTransparentToIndexZero = null,
    bool ShowDiff = false,
    double OnionSkinOpacity = 1);
