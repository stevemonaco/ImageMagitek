namespace ImageMagitek.Colors.Converters;
public sealed class ColorConverterRgb15 : IColorConverter<ColorRgb15>
{
    public ColorRgb15 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)(nc.R >> 3);
        byte g = (byte)(nc.G >> 3);
        byte b = (byte)(nc.B >> 3);

        return new ColorRgb15(r, g, b);
    }

    public ColorRgba32 ToNativeColor(ColorRgb15 fc)
    {
        byte r = (byte)(fc.R << 3);
        byte g = (byte)(fc.G << 3);
        byte b = (byte)(fc.B << 3);
        byte a = 0xFF;

        return new ColorRgba32(r, g, b, a);
    }
}
