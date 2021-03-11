using System;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters
{
    public static class Exporter
    {
        public static bool ExportArranger(ProjectTree projectTree, string arrangerKey, string projectRoot, bool forceOverwrite)
        {
            if (!projectTree.TryGetNode(arrangerKey, out var node))
            {
                Console.WriteLine($"Exporting '{arrangerKey}'...Resource key not found in project");
                return false;
            }

            var relativeFile = Path.Combine(projectTree.CreatePaths(node).ToArray());
            var exportFileName = Path.Combine(projectRoot, $"{relativeFile}.png");

            var arranger = node.Item as ScatteredArranger;

            Console.Write($"Exporting '{arrangerKey}' to '{exportFileName}'...");

            if (!Directory.Exists(Path.GetDirectoryName(exportFileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(exportFileName));

            if (File.Exists(exportFileName) && forceOverwrite == false)
            {
                Console.WriteLine($"File already exists and was skipped to not overwrite it");
                return false;
            }

            if (arranger.ColorType == PixelColorType.Indexed)
            {
                var image = new IndexedImage(arranger);
                image.ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else if (arranger.ColorType == PixelColorType.Direct)
            {
                var image = new DirectImage(arranger);
                image.ExportImage(exportFileName, new ImageSharpFileAdapter());
            }

            Console.WriteLine("Completed successfully");
            return true;
        }
    }
}
