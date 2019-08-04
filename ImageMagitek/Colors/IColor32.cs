using System;
using System.Numerics;

namespace ImageMagitek.Colors
{
    public interface IColor32
    {
        uint Color { get; set; }
        Vector4 ColorVector { get; set; }
        byte R { get; set; }
        byte G { get; set; }
        byte B { get; set; }
        byte A { get; set; }
        int Size { get; }
        int AlphaMax { get; }
        int RedMax { get; }
        int GreenMax { get; }
        int BlueMax { get; }
        void Deconstruct(out byte R, out byte G, out byte B, out byte A);
    }
}
