using System;
using System.Drawing;
using System.Reflection;
using System.Linq;
using MoreLinq;

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
                    if (ElementGrid[x, y] is ArrangerElementModel model)
                        arr.SetElement(model.ToArrangerElement(), x, y);
                }
            }

            return arr;
        }

        public static ScatteredArrangerModel FromScatteredArranger(ScatteredArranger arranger)
        {
            var model = new ScatteredArrangerModel()
            {
                Name = arranger.Name,
                ArrangerElementSize = arranger.ArrangerElementSize,
                ElementPixelSize = arranger.ElementPixelSize,
                Layout = arranger.Layout,
            };

            model.ElementGrid = new ArrangerElementModel[model.ArrangerElementSize.Width, model.ArrangerElementSize.Height];

            for (int x = 0; x < model.ElementGrid.GetLength(0); x++)
            {
                for (int y = 0; y < model.ElementGrid.GetLength(1); y++)
                {
                    model.ElementGrid[x, y] = ArrangerElementModel.FromArrangerElement(arranger.GetElement(x, y), x, y);
                }
            }

            return model;
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
