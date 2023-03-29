namespace ImageMagitek.Colors.Converters;

public enum AlphaBitTransparency { Transparent, BlackTransparent, SemiTransparent, Opaque }
public sealed class ColorConverterAbgr16 : IColorConverter<ColorAbgr16>
{
    private const byte _alphaTransparent = 0;
    private const byte _alphaSemiTransparent = 128;
    private const byte _alphaOpaque = 255;
    private AlphaBitTransparency _transparency;

    public ColorConverterAbgr16() : this(AlphaBitTransparency.Opaque) { }

    public ColorConverterAbgr16(AlphaBitTransparency transparency)
    {
        _transparency = transparency;
    }

    public ColorAbgr16 ToForeignColor(ColorRgba32 nc)
    {
        byte r = (byte)(nc.r >> 3);
        byte g = (byte)(nc.g >> 3);
        byte b = (byte)(nc.b >> 3);
        byte a = (byte)(nc.a <= _alphaSemiTransparent ? 0 : 1);

        return new ColorAbgr16(r, g, b, a);
    }

    public ColorRgba32 ToNativeColor(ColorAbgr16 fc)
    {
        byte r = (byte)(fc.r << 3);
        byte g = (byte)(fc.g << 3);
        byte b = (byte)(fc.b << 3);
        byte a = _alphaOpaque;

        if (fc.A == 1)
        {
            switch (_transparency)
            {
                case AlphaBitTransparency.Transparent:
                    a = _alphaTransparent;
                    break;
                case AlphaBitTransparency.BlackTransparent:
                    if (fc.Color == 0)
                        a = _alphaTransparent;
                    else
                        a = _alphaOpaque;
                    break;
                case AlphaBitTransparency.SemiTransparent:
                    a = _alphaSemiTransparent;
                    break;
                case AlphaBitTransparency.Opaque:
                    a = _alphaOpaque;
                    break;
            }
        }

        return new ColorRgba32(r, g, b, a);
    }
}
