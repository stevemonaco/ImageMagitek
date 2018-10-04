using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatSerializer
    {
        GraphicsFormat LoadFromFile(string fileName);
    }
}
