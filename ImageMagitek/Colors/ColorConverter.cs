using System;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Colors
{
    public static class ColorConverter
    {
        public static ColorConverterBgr15 Bgr15 { get; } = new ColorConverterBgr15();
        public static ColorConverterAbgr16 Abgr16 { get; } = new ColorConverterAbgr16();

        public static ColorRgba32 ToNative(IColor32 color)
        {
            switch(color)
            {
                case ColorBgr15 colorBgr15:
                    return Bgr15.ToNativeColor(colorBgr15);
                case ColorAbgr16 colorAbgr16:
                    return Abgr16.ToNativeColor(colorAbgr16);
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
                case ColorModel.ABGR16:
                    return Abgr16.ToForeignColor(color);
                case ColorModel.RGB24:
                    throw new NotImplementedException();
                case ColorModel.ARGB32:
                    throw new NotImplementedException();
                case ColorModel.RGBA32:
                    return new ColorRgba32(color.Color);
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
