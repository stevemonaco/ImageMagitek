using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public class ColorConverterAbgr16 : IColorConverter<ColorAbgr16>
    {
        public ColorAbgr16 ToForeignColor(ColorRgba32 nc)
        {
            byte r = (byte)(nc.r >> 3);
            byte b = (byte)(nc.b >> 3);
            byte g = (byte)(nc.g >> 3);
            byte a = (byte)(nc.a == 0 ? 0 : 1);

            return new ColorAbgr16(r, g, b, a);
        }

        public ColorRgba32 ToNativeColor(ColorAbgr16 fc)
        {
            byte r = (byte)(fc.r << 3);
            byte g = (byte)(fc.g << 3);
            byte b = (byte)(fc.b << 3);
            byte a = (byte)(fc.a == 0 ? 0 : 0xFF);

            return new ColorRgba32(r, g, b, a);
        }
    }
}
