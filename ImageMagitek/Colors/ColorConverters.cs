using System;
using System.Collections.Generic;
using System.Text;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Colors
{
    public static class ColorConverters
    {
        public static ColorConverterBgr15 Bgr15 { get; } = new ColorConverterBgr15();
    }
}
