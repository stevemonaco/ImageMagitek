using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MoreLinq;

namespace ImageMagitek.Project.Serialization
{
    static class SerializationMapperExtensions
    {
        public static DataFile MapToResource(this DataFileModel df) => 
            new DataFile(df.Name, df.Location);

        public static DataFileModel MapToModel(this DataFile df)
        {
            return new DataFileModel()
            {
                Name = df.Name,
                Location = df.Location
            };
        }

        public static ImageProject MapToResource(this ImageProjectModel model)
        {
            return new ImageProject()
            {
                Name = model.Name,
                Root = model.Root
            };
        }

        public static ImageProjectModel MapToModel(this ImageProject project)
        {
            return new ImageProjectModel()
            {
                Name = project.Name,
                Root = project.Root
            };
        }

        public static PaletteModel MapToModel(this Palette pal, Dictionary<IProjectResource, string> resourceMap)
        {
            var model = new PaletteModel()
            {
                Name = pal.Name,
                ColorModel = pal.ColorModel,
                FileAddress = pal.FileAddress,
                Entries = pal.Entries,
                ZeroIndexTransparent = pal.ZeroIndexTransparent,
            };

            if (pal.DataFile is object && resourceMap.TryGetValue(pal.DataFile, out var dataFileKey))
                model.DataFileKey = dataFileKey;

            return model;
        }

        public static ResourceFolderModel MapToModel(this ResourceFolder folder)
        {
            return new ResourceFolderModel()
            {
                Name = folder.Name
            };
        }

        public static ResourceFolder MapToResource(this ResourceFolderModel model) =>
            new ResourceFolder(model.Name);

        public static ScatteredArrangerModel MapToModel(this ScatteredArranger arranger, Dictionary<IProjectResource, string> resourceMap)
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
                        model.ElementGrid[x, y] = MapToModel(el, x, y);
                    }
                }
            }

            return model;
            
            ArrangerElementModel MapToModel(ArrangerElement el, int elemX, int elemY)
            {
                var model = new ArrangerElementModel
                {
                    FileAddress = el.FileAddress,
                    PositionX = elemX,
                    PositionY = elemY,
                    CodecName = el.Codec.Name
                };

                if (el.DataFile is object && resourceMap.TryGetValue(el.DataFile, out var dataFileKey))
                {
                    model.DataFileKey = dataFileKey;
                }

                if (el.Palette is object && resourceMap.TryGetValue(el.Palette, out var paletteKey))
                {
                    model.PaletteKey = paletteKey;
                }

                return model;
            }
        }

        /// <summary>
        /// Returns the enumeration of all Elements in the grid in a left-to-right, row-by-row order
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ArrangerElementModel> EnumerateElements(this ScatteredArrangerModel model) =>
            model.EnumerateElements(0, 0, model.ArrangerElementSize.Width, model.ArrangerElementSize.Height);

        /// <summary>
        /// Returns the enumeration of a subsection of Elements in the grid in a left-to-right, row-by-row order
        /// </summary>
        /// <param name="elemX">Starting x-coordinate in element coordinates</param>
        /// <param name="elemY">Starting y-coordinate in element coordinates</param>
        /// <param name="width">Number of elements to enumerate in x-direction</param>
        /// <param name="height">Number of elements to enumerate in y-direction</param>
        /// <returns></returns>
        public static IEnumerable<ArrangerElementModel> EnumerateElements(this ScatteredArrangerModel model, 
            int elemX, int elemY, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return model.ElementGrid[x + elemX, y + elemY];
        }

        public static string FindMostFrequentPropertyValue(this ScatteredArrangerModel model, string propertyName)
        {
            Type T = typeof(ArrangerElementModel);
            PropertyInfo P = T.GetProperty(propertyName);

            var query = from ArrangerElementModel el in model.ElementGrid
                        where el is object
                        group el by P.GetValue(el) into grp
                        select new { key = grp.Key, count = grp.Count() };

            return query.MaxBy(x => x.count).FirstOrDefault()?.key as string;
        }
    }
}
