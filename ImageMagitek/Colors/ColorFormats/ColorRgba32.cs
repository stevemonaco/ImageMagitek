using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageMagitek.Colors
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ColorRgba32 : IColor
    {
        [FieldOffset(0)]
        public byte r;
        [FieldOffset(1)]
        public byte g;
        [FieldOffset(2)]
        public byte b;
        [FieldOffset(3)]
        public byte a;
        [FieldOffset(0)]
        public uint color;

        public ColorRgba32(byte red, byte green, byte blue, byte alpha)
        {
            color = default;

            r = red;
            g = green;
            b = blue;
            a = alpha;
        }

        public uint Color { get => color; set => color = value; }

        public byte A { get => a; set => a = value; }

        public byte R { get => r; set => r = value; }

        public byte G { get => g; set => g = value; }

        public byte B { get => b; set => b = value; }

        public int AlphaMax => 255;

        public int RedMax => 255;

        public int GreenMax => 255;

        public int BlueMax => 255;

        public void Deconstruct(out byte A, out byte R, out byte G, out byte B)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }
    }
}
