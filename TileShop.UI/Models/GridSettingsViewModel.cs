using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.Models;
public partial class GridSettingsViewModel : ObservableObject
{
    [ObservableProperty] private int _widthSpacing;
    [ObservableProperty] private int _heightSpacing;
    [ObservableProperty] private int _shiftX;
    [ObservableProperty] private int _shiftY;

    [ObservableProperty] private ObservableCollection<Gridline> _gridlines;
    [ObservableProperty] private IBrush _backgroundBrush;
    [ObservableProperty] private IBrush _lineBrush;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _secondaryColor;
    [ObservableProperty] private Color _lineColor;
    [ObservableProperty] private bool _showGridlines;

    private GridSettingsViewModel()
    {
        _lineBrush = CreateLineBrush();
        _backgroundBrush = CreateBackgroundBrush();
        _gridlines = new();
    }

    public static GridSettingsViewModel CreateDefault(Arranger arranger, GridPreferences preferences)
    {
        var settings = new GridSettingsViewModel()
        {
            WidthSpacing = arranger.ElementPixelSize.Width,
            HeightSpacing = arranger.ElementPixelSize.Height,
            LineColor = ParseHex(preferences.LineColor, GridPreferences.DefaultLineColor),
            PrimaryColor = ParseHex(preferences.PrimaryColor, GridPreferences.DefaultPrimaryColor),
            SecondaryColor = ParseHex(preferences.SecondaryColor, GridPreferences.DefaultSecondaryColor)
        };

        //if (WorkingArranger.Layout == ElementLayout.Single)
        //{
        //    CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height, 8, 8);
        //}
        //else if (WorkingArranger.Layout == ElementLayout.Tiled)
        //{
        //    base.CreateGridlines();
        //}

        settings.Gridlines = settings.CreateGridlines(0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height, settings.WidthSpacing, settings.HeightSpacing);
        settings.LineBrush = settings.CreateLineBrush();
        settings.BackgroundBrush = settings.CreateBackgroundBrush();

        return settings;
    }

    public GridPreferences ToPreferences() => new(ToHex(LineColor), ToHex(PrimaryColor), ToHex(SecondaryColor));

    private static Color ParseHex(string hex, string fallbackHex) =>
        Color.TryParse(hex, out var color) ? color : Color.Parse(fallbackHex);

    private static string ToHex(Color color) => $"#{color.ToUInt32():X8}";

    /// <summary>
    /// Creates a checkered pattern brush
    /// </summary>
    public IBrush CreateBackgroundBrush()
    {
        var drawingA = new GeometryDrawing()
        {
            Brush = new ImmutableSolidColorBrush(PrimaryColor),
            Geometry = StreamGeometry.Parse("M0,0 L2,0 2,2, 0,2Z")
        };

        var drawingB = new GeometryDrawing()
        {
            Brush = new ImmutableSolidColorBrush(SecondaryColor),
            Geometry = StreamGeometry.Parse("M0,1 L2,1 2,2, 1,2 1,0 0,0Z")
        };

        var drawingGroup = new DrawingGroup()
        {
            Children = { drawingA, drawingB }
        };

        var drawing = new DrawingImage(drawingGroup);

        var image = new Image() { Width = WidthSpacing * 2, Height = HeightSpacing * 2, Source = drawing };

        return new VisualBrush
        {
            DestinationRect = new RelativeRect(0, 0, WidthSpacing * 2, HeightSpacing * 2, RelativeUnit.Absolute),
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            Visual = image,
            Transform = new TranslateTransform(WidthSpacing - ShiftX, HeightSpacing - ShiftY)
        };
    }

    public IBrush CreateLineBrush() => new ImmutableSolidColorBrush(LineColor);

    public void AdjustGridlines(Arranger arranger)
    {
        Gridlines = CreateGridlines(ShiftX, ShiftY, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height, WidthSpacing, HeightSpacing);
    }

    private ObservableCollection<Gridline> CreateGridlines(int x1, int y1, int x2, int y2, int xSpacing, int ySpacing)
    {
        var gridlines = new ObservableCollection<Gridline>();
        for (int x = x1; x <= x2; x += xSpacing) // Vertical gridlines
        {
            var gridline = new Gridline(x, 0, x, y2);
            gridlines.Add(gridline);
        }

        for (int y = y1; y <= y2; y += ySpacing) // Horizontal gridlines
        {
            var gridline = new Gridline(0, y, x2, y);
            gridlines.Add(gridline);
        }

        return gridlines;
    }
}

