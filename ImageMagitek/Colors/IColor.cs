using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Colors
{
    public interface IColor
    {
        uint Color { get; set; }
        int Size { get; }
    }
}
