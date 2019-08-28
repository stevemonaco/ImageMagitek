using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public static class ColorFactory
    {
        public static IColor32 CreateColor(ColorModel model, uint color = 0)
        {
            switch (model)
            {
                case ColorModel.RGB24:
                    break;
                case ColorModel.RGBA32:
                    return new ColorRgba32(color);
                case ColorModel.ARGB32:
                    break;
                case ColorModel.BGR15:
                    var colorBgr15 = new ColorBgr15();
                    colorBgr15.Color = color;
                    return colorBgr15;
                case ColorModel.ABGR16:
                    var colorAbgr16 = new ColorAbgr16();
                    colorAbgr16.Color = color;
                    return colorAbgr16;
                case ColorModel.RGB15:
                    break;
                case ColorModel.NES:
                    break;
            }
            throw new NotImplementedException();
        }

        public static IColor32 CloneColor(IColor32 color)
        {
            switch(color)
            {
                case ColorRgba32 _:
                    return new ColorRgba32(color.Color);
                case ColorBgr15 _:
                    return new ColorBgr15(color.R, color.G, color.B);
                case ColorAbgr16 _:
                    return new ColorAbgr16(color.R, color.G, color.B, color.A);
            }
            throw new NotImplementedException();
        }
    }
}
