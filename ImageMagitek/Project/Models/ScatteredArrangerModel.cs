using System;
using System.Drawing;

namespace ImageMagitek.Project.Models
{
    internal class ScatteredArrangerModel
    {
        public string Name { get; set; }
        public ArrangerElementModel[,] ElementGrid { get; set; }
        public Size ArrangerElementSize { get; set; }
        public Size ElementPixelSize { get; set; }
        public ArrangerLayout Layout { get; set; }
        public ScatteredArranger ToScatteredArranger()
        {
            var arr = new ScatteredArranger(Layout, ArrangerElementSize.Width, ArrangerElementSize.Height, ElementPixelSize.Width, ElementPixelSize.Height);

            for(int x = 0; x < ElementGrid.GetLength(0); x++)
            {
                for (int y = 0; y < ElementGrid.GetLength(1); y++)
                {
                    var el = ElementGrid[x, y].ToArrangerElement();
                    el.Width = arr.ElementPixelSize.Width;
                    el.Height = arr.ElementPixelSize.Height;
                    el.X2 = el.X1 + el.Width - 1;
                    el.Y2 = el.Y1 + el.Height - 1;

                    arr.ElementGrid[x, y] = el;
                }
            }

            return arr;
        }
    }
}
