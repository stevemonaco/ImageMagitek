using System.Windows.Media;
using ImageMagitek.Colors;
using Stylet;

namespace TileShop.WPF.Models;

public class ValidatedTableColorModel : PropertyChangedBase
{
    private ITableColor _foreignColor;
    private readonly IColorFactory _colorFactory;

    public ITableColor WorkingColor { get; set; }

    private Color _color;
    public Color Color
    {
        get => _color;
        set => SetAndNotify(ref _color, value);
    }

    public bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }

    private int _index;
    public int Index
    {
        get => _index;
        set => SetAndNotify(ref _index, value);
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
