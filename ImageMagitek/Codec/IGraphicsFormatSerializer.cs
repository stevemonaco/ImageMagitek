using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek
{
    public interface IGraphicsFormatSerializer
    {
        GraphicsFormat LoadFromFile(string fileName);
    }
}
