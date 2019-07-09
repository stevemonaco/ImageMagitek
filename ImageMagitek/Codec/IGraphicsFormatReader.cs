using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatReader
    {
        GraphicsFormat LoadFromFile(string fileName);
    }
}
