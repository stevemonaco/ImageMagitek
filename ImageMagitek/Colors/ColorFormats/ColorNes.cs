using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Colors;

public struct ColorNes : ITableColor
{
    private uint _color;
    public uint Color
    {
        get => _color;
        set
        {
            Guard.IsInRange(value, 0, ColorMax);
            _color = value;
        }
    }

    public int Size => 8;
    public int ColorMax => 63;

    public ColorNes(uint foreignColor)
    {
        Color = foreignColor;
    }
}
