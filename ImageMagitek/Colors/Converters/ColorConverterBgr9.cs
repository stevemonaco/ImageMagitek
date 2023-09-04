using System;

namespace ImageMagitek.Colors.Converters;

public sealed class ColorConverterBgr9 : IColorConverter<ColorBgr9>
{
    private static readonly byte[] _toNativeTable = new byte[]
    {
            0, 36, 72, 109, 145, 182, 218, 255
    };

    public ColorBgr9 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)MathF.Round(nc.R / (255f / 7f));
        byte g = (byte)MathF.Round(nc.G / (255f / 7f));
        byte b = (byte)MathF.Round(nc.B / (255f / 7f));

        return new ColorBgr9(r, g, b);
    }

    public ColorRgba32 ToNativeColor(ColorBgr9 fc)
    {
        byte r = _toNativeTable[fc.R & 0xFE];
        byte g = _toNativeTable[fc.G & 0xFE];
        byte b = _toNativeTable[fc.B & 0xFE];
        byte a = 0xFF;

        return new ColorRgba32(r, g, b, a);
    }
}
