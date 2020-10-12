using System;
using System.Drawing;
using System.Reflection;
using System.Linq;
using MoreLinq;
using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization
{
    internal class ScatteredArrangerModel : ProjectNodeModel
    {
        public ArrangerElementModel[,] ElementGrid { get; set; }
        public Size ArrangerElementSize { get; set; }
        public Size ElementPixelSize { get; set; }
        public ArrangerLayout Layout { get; set; }
        public PixelColorType ColorType { get; set; }

        public static ScatteredArrangerModel FromScatteredArranger(ScatteredArranger arranger)
        {
            var model = new ScatteredArrangerModel()
            {
                Name = arranger.Name,
                ArrangerElementSize = arranger.ArrangerElementSize,
                ElementPixelSize = arranger.ElementPixelSize,
                Layout = arranger.Layout,
                ColorType = arranger.ColorType
            };

            model.ElementGrid = new ArrangerElementModel[model.ArrangerElementSize.Width, model.ArrangerElementSize.Height];

            for (int x = 0; x < model.ElementGrid.GetLength(0); x++)
            {
                for (int y = 0; y < model.ElementGrid.GetLength(1); y++)
                {
                    if (arranger.GetElement(x, y) is ArrangerElement el)
                    {
                        var elModel = ArrangerElementModel.FromArrangerElement(el, x, y);
                        model.ElementGrid[x, y] = elModel;
                    }
                }
            }

            return model;
        }

        /// <summary>
        /// Returns the enumeration of all Elements in the grid in a left-to-right, row-by-row order
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ArrangerElementModel> EnumerateElements() =>
            EnumerateElements(0, 0, ArrangerElementSize.Width, ArrangerElementSize.Height);

        /// <summary>
        /// Returns the enumeration of a subsection of Elements in the grid in a left-to-right, row-by-row order
        /// </summary>
        /// <param name="elemX">Starting x-coordinate in element coordinates</param>
        /// <param name="elemY">Starting y-coordinate in element coordinates</param>
        /// <param name="width">Number of elements to enumerate in x-direction</param>
        /// <param name="height">Number of elements to enumerate in y-direction</param>
        /// <returns></returns>
        public IEnumerable<ArrangerElementModel> EnumerateElements(int elemX, int elemY, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return ElementGrid[x + elemX, y + elemY];
        }

        public string FindMostFrequentPropertyValue(string propertyName)
        {
            Type T = typeof(ArrangerElementModel);
            PropertyInfo P = T.GetProperty(propertyName);

            var query = from ArrangerElementModel el in ElementGrid
                        group el by P.GetValue(el) into grp
                        select new { key = grp.Key, count = grp.Count() };

            return query.MaxBy(x => x.count).First().key as string;
        }
    }
}
