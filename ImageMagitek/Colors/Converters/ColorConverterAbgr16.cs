namespace ImageMagitek.Colors.Converters;

/// <summary>
/// Converts PlayStation ABGR1555 colors losslessly using the hardware STP semantics: 0x0000 is transparent,
/// STP on black is opaque black, STP on any other color is semi-transparent, and no STP is opaque.
/// </summary>
public sealed class ColorConverterAbgr16 : IColorConverter<ColorAbgr16>
{
    private const byte _alphaTransparent = 0;
    private const byte _alphaSemiTransparent = 128;
    private const byte _alphaOpaque = 255;

    public ColorAbgr16 ToForeignColor(ColorRgba32 nc)
    {
        if (nc.A < 64)
            return new ColorAbgr16(0);

        byte r = (byte)(nc.R >> 3);
        byte g = (byte)(nc.G >> 3);
        byte b = (byte)(nc.B >> 3);
        bool isBlack = (r | g | b) == 0;
        byte stp = (byte)(isBlack || nc.A < 192 ? 1 : 0);

        return new ColorAbgr16(r, g, b, stp);
    }

    public ColorRgba32 ToNativeColor(ColorAbgr16 fc)
    {
        byte r = (byte)(fc.R << 3);
        byte g = (byte)(fc.G << 3);
        byte b = (byte)(fc.B << 3);
        bool isBlack = (fc.R | fc.G | fc.B) == 0;

        byte a = (fc.A, isBlack) switch
        {
            (0, true) => _alphaTransparent,
            (1, false) => _alphaSemiTransparent,
            _ => _alphaOpaque
        };

        return new ColorRgba32(r, g, b, a);
    }
}
