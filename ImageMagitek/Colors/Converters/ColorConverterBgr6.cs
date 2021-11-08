namespace ImageMagitek.Colors.Converters;

public class ColorConverterBgr6 : IColorConverter<ColorBgr6>
{
    public ColorBgr6 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)(nc.r / 85);
        byte b = (byte)(nc.b / 85);
        byte g = (byte)(nc.g / 85);

        return new ColorBgr6(r, g, b);
    }

    public ColorRgba32 ToNativeColor(ColorBgr6 fc)
    {
        byte r = (byte)(fc.r * 85);
        byte g = (byte)(fc.g * 85);
        byte b = (byte)(fc.b * 85);
        byte a = 0xFF;

        return new ColorRgba32(r, g, b, a);
    }
}
