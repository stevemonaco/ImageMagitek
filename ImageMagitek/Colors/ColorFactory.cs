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
    }
}
