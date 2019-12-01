using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public interface IIndexedGraphicsCodec : IGraphicsCodec
    {
        void Decode(IndexedImage image, ArrangerElement el);
        void Encode(IndexedImage image, ArrangerElement el);
    }
}
