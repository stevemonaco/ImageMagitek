using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Colors
{
    /// <summary>
    /// Manages the storage and conversion of internal 32bit ARGB colors
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct NativeColor
    {
        private const int AlphaShift = 24;
        private const int RedShift = 16;
        private const int GreenShift = 8;
        private const int BlueShift = 0;

        [FieldOffset(0)]
        public byte R;
        [FieldOffset(1)]
        public byte G;
        [FieldOffset(2)]
        public byte B;
        [FieldOffset(3)]
        public byte A;

        /// <summary>
        /// Gets or sets the native 32bit ARGB Color
        /// </summary>
        [FieldOffset(0)]
        public uint Color;

        public NativeColor(uint color) : this()
        {
            Color = color;
        }

        public NativeColor(byte alpha, byte red, byte green, byte blue) : this()
        {
            A = alpha;
            R = red;
            G = green;
            B = blue;
        }

        #region Color Channel Helper Functions

        /// <summary>
        /// Gets the native alpha channel intensity
        /// </summary>
        /// <returns></returns>
        /*public byte A()
        {
            return (byte)((Color >> AlphaShift) & 0xFF);
        }

        /// <summary>
        /// Gets the native red channel intensity
        /// </summary>
        /// <returns></returns>
        public byte R()
        {
            return (byte)((Color >> RedShift) & 0xFF);
        }

        /// <summary>
        /// Gets the native green channel intensity
        /// </summary>
        /// <returns></returns>
        public byte G()
        {
            return (byte)((Color >> GreenShift) & 0xFF);
        }

        /// <summary>
        /// Gets the native blue channel intensity
        /// </summary>
        /// <returns></returns>
        public byte B()
        {
            return (byte)((Color >> BlueShift) & 0xFF);
        }*/

        public (byte A, byte R, byte G, byte B) Split() => (this.A, this.R, this.G, this.B);

        #endregion

        #region Conversion Functions
        /// <summary>
        /// Converts into a Foreign Color
        /// </summary>
        /// <param name="colorModel">ColorModel of ForeignColor</param>
        /// <returns>Foreign color value</returns>
        public ForeignColor ToForeignColor(ColorModel colorModel)
        {
            ForeignColor fc = (ForeignColor) 0;

            switch(colorModel)
            {
                case ColorModel.BGR15:
                    fc.Color = ((uint)B >> 3) << 10;
                    fc.Color |= ((uint)G >> 3) << 5;
                    fc.Color |= ((uint)R >> 3);
                    break;
                case ColorModel.ABGR16:
                    fc.Color = ((uint)B >> 3) << 10;
                    fc.Color |= ((uint)G >> 3) << 5;
                    fc.Color |= ((uint)R >> 3);
                    fc.Color |= ((uint)A << 15);
                    break;
                case ColorModel.RGB15:
                    fc.Color = (uint)B >> 3;
                    fc.Color |= ((uint)G >> 3) << 5;
                    fc.Color |= ((uint)R >> 3) << 10;
                    break;
                default:
                    throw new ArgumentException($"{nameof(ToForeignColor)} unsupported {nameof(ColorModel)} {colorModel.ToString()}");
            }

            return fc;
        }

        /// <summary>
        /// Converts to a Color
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public System.Drawing.Color ToColor()
        {
            return System.Drawing.Color.FromArgb((int)Color);
        }

        public Rgba32 ToRgba32() => new Rgba32(R, G, B, A);
        #endregion

        #region Cast operators
        public static explicit operator NativeColor(uint color)
        {
            return new NativeColor(color);
        }

        public static explicit operator uint(NativeColor color)
        {
            return color.Color;
        }
        #endregion
    }
}
