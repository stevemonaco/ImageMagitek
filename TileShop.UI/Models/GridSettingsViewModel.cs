using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.Models;
public partial class GridSettingsViewModel : ObservableObject
{
    private const int _singleLayoutSpacing = 8;

    [ObservableProperty] private int _widthSpacing;
    [ObservableProperty] private int _heightSpacing;
    [ObservableProperty] private int _originX;
    [ObservableProperty] private int _originY;

    [ObservableProperty] private IReadOnlyList<Gridline> _gridlines = [];
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;
    [ObservableProperty] private Color _lineColor;
    [ObservableProperty] private bool _showGridlines;

    private GridSettingsViewModel()
    {
    }

    public static GridSettingsViewModel CreateDefault(Arranger arranger, GridPreferences preferences)
    {
        var settings = new GridSettingsViewModel()
        {
            LineColor = ParseHex(preferences.LineColor, GridPreferences.DefaultLineColor),
            PrimaryColor = ParseHex(preferences.PrimaryColor, GridPreferences.DefaultPrimaryColor),
            SecondaryColor = ParseHex(preferences.SecondaryColor, GridPreferences.DefaultSecondaryColor)
        };

        settings.ResetSpacing(arranger);
        return settings;
    }

    /// <summary>
    /// Spacing that lines up with the arranger's elements, or an 8x8 grid for single-image arrangers
    /// </summary>
    public static (int Width, int Height) DefaultSpacing(Arranger arranger) =>
        arranger.Layout == ElementLayout.Single
            ? (_singleLayoutSpacing, _singleLayoutSpacing)
            : (arranger.ElementPixelSize.Width, arranger.ElementPixelSize.Height);

    public GridPreferences ToPreferences() => new(ToHex(LineColor), ToHex(PrimaryColor), ToHex(SecondaryColor));

    public GridSettingsSnapshot Capture() =>
        new(WidthSpacing, HeightSpacing, OriginX, OriginY, LineColor, PrimaryColor, SecondaryColor);

    public void Apply(GridSettingsSnapshot snapshot, Arranger arranger)
    {
        WidthSpacing = snapshot.WidthSpacing;
        HeightSpacing = snapshot.HeightSpacing;
        OriginX = snapshot.OriginX;
        OriginY = snapshot.OriginY;
        LineColor = snapshot.LineColor;
        PrimaryColor = snapshot.PrimaryColor;
        SecondaryColor = snapshot.SecondaryColor;

        AdjustGridlines(arranger);
    }

    /// <summary>
    /// Restores the default spacing for the arranger and moves the origin back to (0, 0)
    /// </summary>
    public void ResetSpacing(Arranger arranger)
    {
        (WidthSpacing, HeightSpacing) = DefaultSpacing(arranger);
        OriginX = 0;
        OriginY = 0;

        AdjustGridlines(arranger);
    }

    /// <summary>
    /// Regenerates the gridlines to cover the arranger's current pixel size
    /// </summary>
    public void AdjustGridlines(Arranger arranger)
    {
        Gridlines = CreateGridlines(arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    private List<Gridline> CreateGridlines(int width, int height)
    {
        var gridlines = new List<Gridline>();

        if (WidthSpacing < 1 || HeightSpacing < 1)
            return gridlines;

        for (int x = WrapOrigin(OriginX, WidthSpacing); x <= width; x += WidthSpacing)
            gridlines.Add(new Gridline(x, 0, x, height));

        for (int y = WrapOrigin(OriginY, HeightSpacing); y <= height; y += HeightSpacing)
            gridlines.Add(new Gridline(0, y, width, y));

        return gridlines;
    }

    private static int WrapOrigin(int origin, int spacing) => ((origin % spacing) + spacing) % spacing;

    private static Color ParseHex(string hex, string fallbackHex) =>
        Color.TryParse(hex, out var color) ? color : Color.Parse(fallbackHex);

    private static string ToHex(Color color) => $"#{color.ToUInt32():X8}";
}
