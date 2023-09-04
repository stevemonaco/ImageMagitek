namespace ImageMagitek.Colors.Converters;

public sealed class ColorConverterBgr15 : IColorConverter<ColorBgr15>
{
    public ColorBgr15 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)(nc.R >> 3);
        byte g = (byte)(nc.G >> 3);
        byte b = (byte)(nc.B >> 3);

        return new ColorBgr15(r, g, b);
    }

    public ColorRgba32 ToNativeColor(ColorBgr15 fc)
    {
        byte r = (byte)(fc.R << 3);
        byte g = (byte)(fc.G << 3);
        byte b = (byte)(fc.B << 3);
        byte a = 0xFF;

        return new ColorRgba32(r, g, b, a);
    }
}
