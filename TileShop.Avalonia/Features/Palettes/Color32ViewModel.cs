using ImageMagitek.Colors;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace TileShop.UI.ViewModels;

public partial class Color32ViewModel : EditableColorBaseViewModel
{
    private IColor32 _foreignColor;
    private readonly IColorFactory _colorFactory;

    public override bool CanSaveColor
    {
        get => WorkingColor.Color != _foreignColor.Color;
    }

    public int Red
    {
        get => ((IColor32)WorkingColor).R;
        set
        {
            ((IColor32)WorkingColor).R = (byte)value;
            OnPropertyChanged(nameof(Red));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Blue
    {
        get => ((IColor32)WorkingColor).B;
        set
        {
            ((IColor32)WorkingColor).B = (byte)value;
            OnPropertyChanged(nameof(Blue));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Green
    {
        get => ((IColor32)WorkingColor).G;
        set
        {
            ((IColor32)WorkingColor).G = (byte)value;
            OnPropertyChanged(nameof(Green));
            var nativeColor = _colorFactory.ToNative(WorkingColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }

    public int Alpha
    {
        get => ((IColor32)WorkingColor).A;
        set
        {
            ((IColor32)WorkingColor).A = (byte)value;
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

    [SetsRequiredMembers]
    public Color32ViewModel(IColor32 foreignColor, int index, IColorFactory colorFactory)
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
