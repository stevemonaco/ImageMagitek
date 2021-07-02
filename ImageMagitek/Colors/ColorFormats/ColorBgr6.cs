using System;
using System.Numerics;

namespace ImageMagitek.Colors
{
    public struct ColorBgr6 : IColor32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public ColorBgr6(uint foreignColor)
        {
            r = (byte)(foreignColor & 0x3);
            g = (byte)((foreignColor & 0xc) >> 2);
            b = (byte)((foreignColor & 0x30) >> 4);
            a = 0;
        }

        public ColorBgr6(byte red, byte green, byte blue)
        {
            r = red;
            g = green;
            b = blue;
            a = 0;
        }

        public byte R { get => r; set => r = value; }
        public byte G { get => g; set => g = value; }
        public byte B { get => b; set => b = value; }
        public byte A { get => 0; set => a = 0; }

        public uint Color
        {
            get
            {
                uint value = r;
                value |= (uint)g << 2;
                value |= (uint)b << 4;
                return value;
            }
            set
            {
                r = (byte)(value & 0x3);
                g = (byte)((value & 0xc) >> 2);
                b = (byte)((value & 0x30) >> 4);
            }
        }

        public int Size => 8;

        public int AlphaMax => 0;

        public int RedMax => 3;

        public int GreenMax => 3;

        public int BlueMax => 3;

        private static Vector4 _maxVector = new Vector4(3f, 3f, 3f, 1f);
        public Vector4 ColorVector
        {
            get => new Vector4(r, g, b, 1f) / _maxVector;
            set
            {
                var vec = value * _maxVector;
                r = (byte)Math.Round(vec.X);
                g = (byte)Math.Round(vec.Y);
                b = (byte)Math.Round(vec.Z);
                a = 0;
            }
        }

        public void Deconstruct(out byte A, out byte R, out byte G, out byte B)
        {
            A = 0;
            R = r;
            G = g;
            B = b;
        }
    }
}
