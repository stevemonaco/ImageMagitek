using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface IColor
    {
        uint Color { get; set; }
        byte A { get; }
        byte R { get; }
        byte G { get; }
        byte B { get; }
        int AlphaMax { get; }
        int RedMax { get; }
        int GreenMax { get; }
        int BlueMax { get; }
        void Deconstruct(out byte A, out byte R, out byte G, out byte B);
    }
}
