using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public interface IDirectGraphicsCodec : IGraphicsCodec
    {
        void Decode(Image<Rgba32> image, ArrangerElement el);
        void Encode(Image<Rgba32> image, ArrangerElement el);
    }
}
