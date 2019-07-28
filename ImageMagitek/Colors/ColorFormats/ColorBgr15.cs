using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageMagitek.Colors
{
    public struct ColorBgr15 : IColor
    {
        public byte r;
        public byte g;
        public byte b;

        public ColorBgr15(byte blue, byte green, byte red)
        {
            b = blue;
            r = red;
            g = green;
        }

        public byte A => 0;

        public byte R { get => r; set => r = value; }

        public byte G { get => g; set => g = value; }

        public byte B { get => b; set => b = value; }

        public uint Color
        {
            get
            {
                uint value = r;
                value |= ((uint)g << 5);
                value |= ((uint)b << 10);
                return value;
            }
            set
            {
                r = (byte)(value & 0x1f);
                g = (byte)((value & 0x3e0) >> 5);
                g = (byte)((value & 0x7c0) >> 10);
            }
        }

        public int AlphaMax => 0;

        public int RedMax => 31;

        public int GreenMax => 31;

        public int BlueMax => 31;

        public void Deconstruct(out byte A, out byte R, out byte G, out byte B)
        {
            A = 0;
            R = r;
            G = g;
            B = b;
        }
    }
}
