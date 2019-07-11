using System.Drawing;

namespace ImageMagitek.ExtensionMethods
{
    public static class ColorExtensions
    {
        public static NativeColor ToNativeColor(this Color c)
        {
            return new NativeColor((uint)c.ToArgb());
        }

        //private uint ColorToUint(Color color)
        //{
        //    uint c = ((uint)color.A) << 24 | ((uint)color.R) << 16 | ((uint)color.G) << 8 | ((uint)color.B);
        //    return c;
        //}
    }
}
