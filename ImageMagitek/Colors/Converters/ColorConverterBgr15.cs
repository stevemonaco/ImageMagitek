using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors.Converters
{
    public sealed class ColorConverterBgr15 : IColorConverter<ColorBgr15, ColorRgba32>
    {
        public ColorBgr15 ToForeignColor(ColorRgba32 nc)
        {
            byte b = (byte)(nc.b >> 3);
            byte g = (byte)(nc.g >> 3);
            byte r = (byte)(nc.r >> 3);

            return new ColorBgr15(b, g, r);
        }

        public ColorRgba32 ToNativeColor(ColorBgr15 fc)
        {
            byte r = (byte)(fc.r << 3);
            byte g = (byte)(fc.g << 3);
            byte b = (byte)(fc.b << 3);
            byte a = 0xFF;

            return new ColorRgba32(r, g, b, a);
        }
    }
}
