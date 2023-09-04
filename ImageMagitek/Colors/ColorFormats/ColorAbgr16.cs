using System;
using System.Numerics;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Colors;

public struct ColorAbgr16 : IColor32
{
    private byte _r;
    private byte _g;
    private byte _b;
    private byte _a;

    public ColorAbgr16(uint foreignColor)
    {
        _r = (byte)(foreignColor & 0x1f);
        _g = (byte)((foreignColor & 0x3e0) >> 5);
        _b = (byte)((foreignColor & 0x7c00) >> 10);
        _a = (byte)((foreignColor & 0x8000) >> 15);
    }

    public ColorAbgr16(byte red, byte green, byte blue, byte alpha)
    {
        R = red;
        G = green;
        B = blue;
        A = alpha;
    }

    public byte R
    {
        get => _r;
        set
        {
            Guard.IsInRange(value, 0, RedMax+1);
            _r = value;
        }
    }

    public byte G
    {
        get => _g;
        set
        {
            Guard.IsInRange(value, 0, GreenMax+1);
            _g = value;
        }
    }

    public byte B
    {
        get => _b;
        set
        {
            Guard.IsInRange(value, 0, BlueMax+1);
            _b = value;
        }
    }

    public byte A
    {
        get => _a;
        set
        {
            Guard.IsInRange(value, 0, AlphaMax+1);
            _a = value;
        }
    }

    public uint Color
    {
        get
        {
            uint value = _r;
            value |= (uint)_g << 5;
            value |= (uint)_b << 10;
            value |= (uint)_a << 15;
            return value;
        }
        set
        {
            _r = (byte)(value & 0x1f);
            _g = (byte)((value & 0x3e0) >> 5);
            _b = (byte)((value & 0x7c00) >> 10);
            _a = (byte)((value & 0x8000) >> 15);
        }
    }

    public int Size => 16;
    public int AlphaMax => 1;
    public int RedMax => 31;
    public int GreenMax => 31;
    public int BlueMax => 31;
    private static readonly Vector4 _maxVector = new Vector4(31f, 31f, 31f, 1f);

    public Vector4 ColorVector
    {
        get => new Vector4(_r, _g, _b, _a) / _maxVector;
        set
        {
            var vec = value * _maxVector;
            _r = (byte)Math.Round(vec.X);
            _g = (byte)Math.Round(vec.Y);
            _b = (byte)Math.Round(vec.Z);
            _a = (byte)Math.Round(vec.W);
        }
    }

    public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
    {
        r = _r;
        g = _g;
        b = _b;
        a = _a;
    }
}
