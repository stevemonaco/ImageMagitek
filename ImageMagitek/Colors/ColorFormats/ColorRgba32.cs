using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Colors;

[StructLayout(LayoutKind.Explicit)]
public struct ColorRgba32 : IColor32
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

    public ColorRgba32(uint packedColor)
    {
        r = default;
        g = default;
        b = default;
        a = default;
        color = packedColor;
    }

    public uint Color { get => color; set => color = value; }

    public byte R { get => r; set => r = value; }
    public byte G { get => g; set => g = value; }
    public byte B { get => b; set => b = value; }
    public byte A { get => a; set => a = value; }

    public int Size => 32;
    public int RedMax => 255;
    public int GreenMax => 255;
    public int BlueMax => 255;
    public int AlphaMax => 255;

    private static Vector4 _maxVector = new Vector4(255, 255, 255, 255);
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

    public Rgba32 ToRgba32() => new Rgba32(color);

    public void Deconstruct(out byte R, out byte G, out byte B, out byte A)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}
