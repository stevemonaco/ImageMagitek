using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public sealed class ColorConverterAbgr16 : IColorConverter<ColorAbgr16>
    {
        private const byte AlphaTransparent = 0;
        private const byte AlphaSemiTransparent = 128;
        private const byte AlphaOpaque = 255;

        public ColorAbgr16 ToForeignColor(ColorRgba32 nc)
        {
            byte r = (byte)(nc.r >> 3);
            byte b = (byte)(nc.b >> 3);
            byte g = (byte)(nc.g >> 3);
            byte a = (byte)(nc.a <= AlphaSemiTransparent ? 0 : 1);

            return new ColorAbgr16(r, g, b, a);
        }

        public ColorRgba32 ToNativeColor(ColorAbgr16 fc)
        {
            byte r = (byte)(fc.r << 3);
            byte g = (byte)(fc.g << 3);
            byte b = (byte)(fc.b << 3);
            byte a = fc.a == 0 ? AlphaTransparent : AlphaOpaque;

            return new ColorRgba32(r, g, b, a);
        }
    }
}
