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
    [ObservableProperty] private Color _primaryColor = DefaultPrimaryColor;
    [ObservableProperty] private Color _secondaryColor = DefaultSecondaryColor;
    [ObservableProperty] private Color _lineColor = DefaultLineColor;
    [ObservableProperty] private bool _showGridlines;

    public static Color DefaultPrimaryColor { get; } = Color.FromArgb(0, 0, 0, 0);
    public static Color DefaultSecondaryColor { get; } = Color.FromArgb(25, 128, 128, 128);
    public static Color DefaultLineColor { get; } = Color.FromArgb(196, 204, 132, 132);

    private int _width;
    private int _height;
    private int _viewDx;
    private int _viewDy;

    private GridSettingsViewModel()
    {
        _lineBrush = CreateLineBrush();
        _backgroundBrush = CreateBackgroundBrush();
        _gridlines = new();
    }

    public static GridSettingsViewModel CreateDefault<TPixel>(ImageBase<TPixel> image) where TPixel : unmanaged
    {
        GridSettingsViewModel settings;

        if (image.Arranger.Layout == ElementLayout.Tiled)
        {
            settings = new GridSettingsViewModel()
            {
                WidthSpacing = image.Arranger.ElementPixelSize.Width,
                HeightSpacing = image.Arranger.ElementPixelSize.Height,
                ShiftX = image.Width % image.Arranger.ElementPixelSize.Width,
                ShiftY = image.Height % image.Arranger.ElementPixelSize.Height,
            };
        }
        else
        {
            settings = new GridSettingsViewModel()
            {
                WidthSpacing = 8,
                HeightSpacing = 8,
                ShiftX = image.Width % 8,
                ShiftY = image.Height % 8,
            };
        }

        settings._width = image.Width;
        settings._height = image.Height;
        settings._viewDx = image.Left;
        settings._viewDy = image.Top;

        settings.Gridlines = settings.CreateGridlines();
        settings.LineBrush = settings.CreateLineBrush();
        settings.BackgroundBrush = settings.CreateBackgroundBrush();

        //settings.Gridlines = settings.CreateGridlines(settings.WidthSpacing - settings.ShiftX, settings.HeightSpacing - settings.ShiftY, image.Width, image.Height, settings.WidthSpacing, settings.HeightSpacing);

        return settings;
    }

    public static GridSettingsViewModel CreateDefault(Arranger arranger, int xStart, int yStart, int width, int height)
    {
        var settings = new GridSettingsViewModel()
        {
            WidthSpacing = arranger.ElementPixelSize.Width,
            HeightSpacing = arranger.ElementPixelSize.Height
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

    private ObservableCollection<Gridline> CreateGridlines()
    {
        //int x1 = (ShiftX)
        int x1 = WidthSpacing - ShiftX;
        if (x1 == WidthSpacing)
            x1 = 0;

        int y1 = HeightSpacing - ShiftY;
        if (y1 == HeightSpacing)
            y1 = 0;

        var gridlines = new ObservableCollection<Gridline>();
        for (int x = x1; x <= _width; x += WidthSpacing) // Vertical gridlines
        {
            var gridline = new Gridline(x, 0, x, _height);
            gridlines.Add(gridline);
        }

        for (int y = y1; y <= _height; y += HeightSpacing) // Horizontal gridlines
        {
            var gridline = new Gridline(0, y, _width, y);
            gridlines.Add(gridline);
        }

        return gridlines;
    }
}

