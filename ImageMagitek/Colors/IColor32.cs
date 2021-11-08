using System.Numerics;

namespace ImageMagitek.Colors;

public interface IColor32 : IColor
{
    Vector4 ColorVector { get; set; }
    byte R { get; set; }
    byte G { get; set; }
    byte B { get; set; }
    byte A { get; set; }

    int AlphaMax { get; }
    int RedMax { get; }
    int GreenMax { get; }
    int BlueMax { get; }
    void Deconstruct(out byte R, out byte G, out byte B, out byte A);
}
