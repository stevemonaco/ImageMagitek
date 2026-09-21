using System;
using ImageMagitek.Colors;

namespace TileShop.UI.ViewModels;

public partial class Color32ViewModel : EditableColorBaseViewModel
{
    private IColor32 Working => (IColor32)WorkingColor;

    public int Red
    {
        get => Working.R;
        set => SetComponent(nameof(Red), value, (c, v) => c.R = v);
    }

    public int Green
    {
        get => Working.G;
        set => SetComponent(nameof(Green), value, (c, v) => c.G = v);
    }

    public int Blue
    {
        get => Working.B;
        set => SetComponent(nameof(Blue), value, (c, v) => c.B = v);
    }

    public int Alpha
    {
        get => Working.A;
        set => SetComponent(nameof(Alpha), value, (c, v) => c.A = v);
    }

    public int RedMax { get; }
    public int GreenMax { get; }
    public int BlueMax { get; }
    public int AlphaMax { get; }

    public override bool HasAlpha => AlphaMax > 0;

    public Color32ViewModel(IColor32 foreignColor, int index, IColorFactory colorFactory, ColorModel colorModel)
        : base(foreignColor, index, colorFactory, colorModel)
    {
        RedMax = foreignColor.RedMax;
        GreenMax = foreignColor.GreenMax;
        BlueMax = foreignColor.BlueMax;
        AlphaMax = foreignColor.AlphaMax;
    }

    protected override void ApplyWorkingColor(IColor color)
    {
        base.ApplyWorkingColor(color);
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(Alpha));
    }

    private void SetComponent(string propertyName, int value, Action<IColor32, byte> assign)
    {
        assign(Working, (byte)value);
        OnPropertyChanged(propertyName);
        NotifyWorkingColorChanged();
    }
}
