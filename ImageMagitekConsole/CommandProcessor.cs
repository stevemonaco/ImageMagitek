using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using ImageMagitek;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace ImageMagitekConsole
{
    public class CommandProcessor
    {
        private readonly PathTree<ProjectResourceBase> ResourceTree;

        public CommandProcessor(PathTree<ProjectResourceBase> resourceTree)
        {
            ResourceTree = resourceTree;
        }

        public bool PrintResources()
        {
            foreach (var res in ResourceTree.EnumerateDepthFirst())
            {
                string key = res.PathKey;
                int level = key.Split('\\').Length;

                Console.WriteLine($"{res.Name}({level}): {res.Value.GetType().ToString()}");
            }

            return true;
        }

        public bool ExportArranger(string arrangerKey, string projectRoot)
        {
            ResourceTree.TryGetValue(arrangerKey, out ScatteredArranger arranger);

            var exportFileName = Path.Combine(projectRoot, arrangerKey + ".bmp");
            Console.WriteLine($"Exporting {arranger.Name} to {exportFileName}...");

            Directory.CreateDirectory(Path.GetDirectoryName(exportFileName));

            using (var rm = new ArrangerImage())
            using (var fs = File.Create(exportFileName, 32 * 1024, FileOptions.SequentialScan))
            {
                rm.Render(arranger);
                rm.Image.SaveAsBmp(fs);
            }

            return true;
        }

        public bool ExportAllArrangers(string projectRoot)
        {
            foreach (var res in ResourceTree.EnumerateDepthFirst().OfType<IPathTreeNode<ScatteredArranger>>())
                ExportArranger(res.PathKey, projectRoot);

            return true;
        }

        public bool ImportImage(string imageFileName, string arrangerKey)
        {
            Console.WriteLine($"Importing {imageFileName} to {arrangerKey}...");

            ResourceTree.TryGetValue(arrangerKey, out ScatteredArranger arranger);

            using (var rm = new ArrangerImage())
            {
                rm.LoadImage(imageFileName);
                rm.SaveImage(arranger);
            }

            return true;
        }

        public bool ImportAllImages(string projectRoot)
        {
            foreach(var arranger in ResourceTree.EnumerateDepthFirst().OfType<ScatteredArranger>())
            {
                string imageFileName = Path.Combine(projectRoot, arranger.ResourceKey + ".bmp");
                if(File.Exists(imageFileName))
                    ImportImage(imageFileName, arranger.ResourceKey);
            }
            return true;
        }
    }
}
