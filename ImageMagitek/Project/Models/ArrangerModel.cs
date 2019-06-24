using System;
using System.Drawing;

namespace ImageMagitek.Project.Models
{
    internal class ArrangerModel
    {
        public ArrangerElement[,] ElementGrid { get; set; }

        public Size ArrangerElementSize { get; }

        public Size ArrangerPixelSize { get; }

        public Size ElementPixelSize { get; }

        public ArrangerLayout Layout { get; protected set; }
    }
}
