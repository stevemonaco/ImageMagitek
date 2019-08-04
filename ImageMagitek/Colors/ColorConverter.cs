using System;
using System.Collections.Generic;
using System.Text;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Colors
{
    public static class ColorConverter
    {
        public static ColorConverterBgr15 Bgr15 { get; } = new ColorConverterBgr15();

        public static ColorRgba32 ToNative(IColor32 color)
        {
            switch(color)
            {
                case ColorBgr15 colorBgr15:
                    return Bgr15.ToNativeColor(colorBgr15);
                case ColorRgba32 _:
                    return new ColorRgba32(color.Color);
                default:
                    var defaultColor = new ColorRgba32();
                    defaultColor.ColorVector = color.ColorVector;
                    return defaultColor;
            }
        }

        public static IColor32 ToForeign(ColorRgba32 color, ColorModel colorModel)
        {
            switch (colorModel)
            {
                case ColorModel.BGR15:
                    return Bgr15.ToForeignColor(color);
                case ColorModel.RGB24:
                    throw new NotImplementedException();
                case ColorModel.ARGB32:
                    throw new NotImplementedException();
                case ColorModel.ABGR16:
                    throw new NotImplementedException();
                case ColorModel.RGB15:
                    throw new NotImplementedException();
                case ColorModel.NES:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
