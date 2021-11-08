using System;

namespace ImageMagitek.Colors;

public struct ColorNes : ITableColor
{
    private uint _color;
    public uint Color
    {
        get => _color;
        set => _color = Math.Clamp(value, 0, (uint)ColorMax);
    }

    public int Size => 8;
    public int ColorMax => 63;

    public ColorNes(uint foreignColor)
    {
        _color = Math.Clamp(foreignColor, 0, 63);
    }
}
