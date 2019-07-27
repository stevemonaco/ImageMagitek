using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface IPalette
    {

        bool IsReadOnly { get; }

        NativeColor GetNativeColor(int index);
        ForeignColor GetForeignColor(int index);
        Color GetColor(int index);
        byte GetIndexByNativeColor(NativeColor color, bool exactColorOnly);
        void SetPaletteForeignColor(int index, ForeignColor foreignColor);
        void SetPaletteForeignColor(int index, byte A, byte R, byte G, byte B);

        NativeColor this[int index] { get; }
    }
}
