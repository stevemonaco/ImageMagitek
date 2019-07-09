using System;
using System.Drawing;

namespace ImageMagitek.Project.SerializationModels
{
    internal class ScatteredArrangerModel : ProjectNodeModel
    {
        public ArrangerElementModel[,] ElementGrid { get; set; }
        public Size ArrangerElementSize { get; set; }
        public Size ElementPixelSize { get; set; }
        public ArrangerLayout Layout { get; set; }
        public ScatteredArranger ToScatteredArranger()
        {
            var arr = new ScatteredArranger(Name, Layout, ArrangerElementSize.Width, ArrangerElementSize.Height, ElementPixelSize.Width, ElementPixelSize.Height);

            for(int x = 0; x < ElementGrid.GetLength(0); x++)
            {
                for (int y = 0; y < ElementGrid.GetLength(1); y++)
                {
                    var el = ElementGrid[x, y].ToArrangerElement();
                    el.Width = arr.ElementPixelSize.Width;
                    el.Height = arr.ElementPixelSize.Height;

                    arr.ElementGrid[x, y] = el;
                }
            }

            return arr;
        }
    }
}
