using System;
using System.IO;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters
{
    public static class Importer
    {
        public static bool ImportImage(ProjectTree projectTree, string imageFileName, string arrangerKey)
        {
            Console.Write($"Importing '{imageFileName}' to '{arrangerKey}'...");

            if (!File.Exists(imageFileName))
            {
                Console.WriteLine($"File does not exist");
                return false;
            }

            if (!projectTree.Tree.TryGetValue(arrangerKey, out ScatteredArranger arranger))
            {
                Console.WriteLine($"Resource key does not exist or is not a {nameof(ScatteredArranger)}");
                return false;
            }

            if (arranger.ColorType == PixelColorType.Indexed)
            {
                var image = new IndexedImage(arranger);
                image.ImportImage(imageFileName, new ImageSharpFileAdapter(), ColorMatchStrategy.Exact);
                image.SaveImage();
            }
            else if (arranger.ColorType == PixelColorType.Direct)
            {
                var image = new DirectImage(arranger);
                image.ImportImage(imageFileName, new ImageSharpFileAdapter());
                image.SaveImage();
            }

            Console.WriteLine("Completed successfully");
            return true;
        }
    }
}
