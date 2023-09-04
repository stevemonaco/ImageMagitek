using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Colors;

[StructLayout(LayoutKind.Explicit)]
public struct ColorRgba32 : IColor32
{
    [FieldOffset(0)]
    private byte _r;
    [FieldOffset(1)]
    private byte _g;
    [FieldOffset(2)]
    private byte _b;
    [FieldOffset(3)]
    private byte _a;
    [FieldOffset(0)]
    private uint _color;

    public ColorRgba32(byte red, byte green, byte blue, byte alpha)
    {
        Unsafe.SkipInit(out _color);

        _r = red;
        _g = green;
        _b = blue;
        _a = alpha;
    }

    public ColorRgba32(uint packedColor)
    {
        Unsafe.SkipInit(out _r);
        Unsafe.SkipInit(out _g);
        Unsafe.SkipInit(out _b);
        Unsafe.SkipInit(out _a);

        _color = packedColor;
    }

    public uint Color { get => _color; set => _color = value; }

    public byte R { get => _r; set => _r = value; }
    public byte G { get => _g; set => _g = value; }
    public byte B { get => _b; set => _b = value; }
    public byte A { get => _a; set => _a = value; }

    public int Size => 32;
    public int RedMax => 255;
    public int GreenMax => 255;
    public int BlueMax => 255;
    public int AlphaMax => 255;
    private static readonly Vector4 _maxVector = new Vector4(255, 255, 255, 255);

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

    public Rgba32 ToRgba32() => new Rgba32(_color);

    public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
    {
        r = _r;
        g = _g;
        b = _b;
        a = _a;
    }
}
