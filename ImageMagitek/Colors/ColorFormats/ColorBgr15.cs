using System;
using System.Numerics;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Colors;

public struct ColorBgr15 : IColor32
{
    private byte _r;
    private byte _g;
    private byte _b;
    private byte _a;

    public ColorBgr15(uint foreignColor)
    {
        _r = (byte)(foreignColor & 0x1f);
        _g = (byte)((foreignColor & 0x3e0) >> 5);
        _b = (byte)((foreignColor & 0x7c00) >> 10);
        _a = 0;
    }

    public ColorBgr15(byte red, byte green, byte blue)
    {
        R = red;
        G = green;
        B = blue;
        _a = 0;
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

    public byte A { get => 0; set => _a = 0; }

    public uint Color
    {
        get
        {
            uint value = _r;
            value |= (uint)_g << 5;
            value |= (uint)_b << 10;
            return value;
        }
        set
        {
            _r = (byte)(value & 0x1f);
            _g = (byte)((value & 0x3e0) >> 5);
            _b = (byte)((value & 0x7c00) >> 10);
        }
    }

    public int Size => 16;
    public int AlphaMax => 0;
    public int RedMax => 31;
    public int GreenMax => 31;
    public int BlueMax => 31;
    private static readonly Vector4 _maxVector = new Vector4(31f, 31f, 31f, 1f);

    public Vector4 ColorVector
    {
        get => new Vector4(_r, _g, _b, 1f) / _maxVector;
        set
        {
            var vec = value * _maxVector;
            _r = (byte)Math.Round(vec.X);
            _g = (byte)Math.Round(vec.Y);
            _b = (byte)Math.Round(vec.Z);
            _a = 0;
        }
    }

    public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
    {
        r = _r;
        g = _g;
        b = _b;
        a = 0;
    }
}
