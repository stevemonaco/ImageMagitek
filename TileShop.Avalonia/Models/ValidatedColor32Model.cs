using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.UI.Models;

public partial class ValidatedColor32Model : ObservableObject
{
    private IColor32 _foreignColor;
    private readonly IColorFactory _colorFactory;

    public IColor32 WorkingColor { get; set; }

    [ObservableProperty] private Color _color;

    public int Red
    {
        get => WorkingColor.R;
        set
        {
            WorkingColor.R = (byte)value;
            OnPropertyChanged(nameof(Red));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Blue
    {
        get => WorkingColor.B;
        set
        {
            WorkingColor.B = (byte)value;
            OnPropertyChanged(nameof(Blue));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Green
    {
        get => WorkingColor.G;
        set
        {
            WorkingColor.G = (byte)value;
            OnPropertyChanged(nameof(Green));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Alpha
    {
        get => WorkingColor.A;
        set
        {
            WorkingColor.A = (byte)value;
            OnPropertyChanged(nameof(Alpha));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    [ObservableProperty] private int _redMax;
    [ObservableProperty] private int _greenMax;
    [ObservableProperty] private int _blueMax;
    [ObservableProperty] private int _alphaMax;
    [ObservableProperty] private int _index;

    public bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }
    
    public ValidatedColor32Model(IColor32 foreignColor, int index, IColorFactory colorFactory)
    {
        _foreignColor = foreignColor;
        Index = index;
        _colorFactory = colorFactory;

        WorkingColor = (IColor32)_colorFactory.CloneColor(foreignColor);
        var nativeColor = _colorFactory.ToNative(foreignColor);
        Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);

        Red = foreignColor.R;
        Green = foreignColor.G;
        Blue = foreignColor.B;
        Alpha = foreignColor.A;
        RedMax = foreignColor.RedMax;
        GreenMax = foreignColor.GreenMax;
        BlueMax = foreignColor.BlueMax;
        AlphaMax = foreignColor.AlphaMax;
    }

    public void SaveColor()
    {
        _foreignColor = (IColor32)_colorFactory.CloneColor(WorkingColor);
        OnPropertyChanged(nameof(CanSaveColor));
    }
}
