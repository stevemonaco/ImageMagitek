namespace ImageMagitek.Colors.Converters;

public class ColorConverterBgr6 : IColorConverter<ColorBgr6>
{
    public ColorBgr6 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)(nc.R / 85);
        byte g = (byte)(nc.G / 85);
        byte b = (byte)(nc.B / 85);

        return new ColorBgr6(r, g, b);
    }

    public ColorRgba32 ToNativeColor(ColorBgr6 fc)
    {
        byte r = (byte)(fc.R * 85);
        byte g = (byte)(fc.G * 85);
        byte b = (byte)(fc.B * 85);
        const byte a = 0xFF;

        return new ColorRgba32(r, g, b, a);
    }
}
