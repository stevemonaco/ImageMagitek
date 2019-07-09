using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek
{
    public interface IGraphicsFormatReader
    {
        GraphicsFormat LoadFromFile(string fileName);
    }
}
