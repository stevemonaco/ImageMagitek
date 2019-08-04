using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public static class ColorFactory
    {
        public static IColor32 NewColor(ColorModel model)
        {
            switch (model)
            {
                case ColorModel.RGB24:
                    break;
                case ColorModel.ARGB32:
                    return new ColorRgba32();
                case ColorModel.BGR15:
                    return new ColorBgr15();
                case ColorModel.ABGR16:
                    break;
                case ColorModel.RGB15:
                    break;
                case ColorModel.NES:
                    break;
            }
            throw new NotImplementedException();
        }
    }
}
