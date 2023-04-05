using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.UI.Models;

public partial class ValidatedTableColorModel : ObservableObject
{
    private ITableColor _foreignColor;
    private readonly IColorFactory _colorFactory;

    public ITableColor WorkingColor { get; set; }

    [ObservableProperty] private Color _color;
    [ObservableProperty] private int _index;

    public bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }

    public ValidatedTableColorModel(ITableColor foreignColor, int index, IColorFactory colorFactory)
    {
        _foreignColor = foreignColor;
        Index = index;
        _colorFactory = colorFactory;

        WorkingColor = (ITableColor)_colorFactory.CloneColor(foreignColor);
        var nativeColor = _colorFactory.ToNative(foreignColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
    }

    public void SaveColor()
    {
        _foreignColor = (ITableColor)_colorFactory.CloneColor(WorkingColor);
        OnPropertyChanged(nameof(CanSaveColor));
    }
}
