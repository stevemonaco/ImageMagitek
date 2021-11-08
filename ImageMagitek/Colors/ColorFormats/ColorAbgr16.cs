using System;
using System.Numerics;

namespace ImageMagitek.Colors;

public struct ColorAbgr16 : IColor32
{
    public byte r;
    public byte g;
    public byte b;
    public byte a;

    public ColorAbgr16(uint foreignColor)
    {
        r = (byte)(foreignColor & 0x1f);
        g = (byte)((foreignColor & 0x3e0) >> 5);
        b = (byte)((foreignColor & 0x7c00) >> 10);
        a = (byte)((foreignColor & 0x8000) >> 15);
    }

    public ColorAbgr16(byte red, byte green, byte blue, byte alpha)
    {
        r = red;
        g = green;
        b = blue;
        a = alpha;
    }

    public byte R { get => r; set => r = value; }
    public byte G { get => g; set => g = value; }
    public byte B { get => b; set => b = value; }
    public byte A { get => a; set => a = value; }

    public uint Color
    {
        get
        {
            uint value = r;
            value |= ((uint)g << 5);
            value |= ((uint)b << 10);
            value |= ((uint)a << 15);
            return value;
        }
        set
        {
            r = (byte)(value & 0x1f);
            g = (byte)((value & 0x3e0) >> 5);
            b = (byte)((value & 0x7c00) >> 10);
            a = (byte)((value & 0x8000) >> 15);
        }
    }

    public int Size => 16;

    public int AlphaMax => 1;

    public int RedMax => 31;

    public int GreenMax => 31;

    public int BlueMax => 31;

    private static Vector4 _maxVector = new Vector4(31f, 31f, 31f, 1f);
    public Vector4 ColorVector
    {
        get => new Vector4(r, g, b, a) / _maxVector;
        set
        {
            var vec = value * _maxVector;
            r = (byte)Math.Round(vec.X);
            g = (byte)Math.Round(vec.Y);
            b = (byte)Math.Round(vec.Z);
            a = (byte)Math.Round(vec.W);
        }
    }

    public void Deconstruct(out byte A, out byte R, out byte G, out byte B)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}
