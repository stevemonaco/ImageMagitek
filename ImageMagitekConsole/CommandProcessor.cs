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

                Console.WriteLine($"{res.Name}({level}): type '{res.Value.GetType().ToString()}' path '{key}'");
            }

            return true;
        }

        public bool ExportArranger(string arrangerKey, string projectRoot)
        {
            ResourceTree.TryGetNode(arrangerKey, out var node);

            var relativeFile = Path.Combine(node.Paths.ToArray());
            var exportFileName = Path.Combine(projectRoot, relativeFile + ".bmp");

            var arranger = node.Value as ScatteredArranger;

            Console.WriteLine($"Exporting {arranger.Name} to {exportFileName}...");
            Directory.CreateDirectory(Path.GetDirectoryName(exportFileName));

            using (var image = new ArrangerImage())
            using (var fs = File.Create(exportFileName, 32 * 1024, FileOptions.SequentialScan))
            {
                image.Render(arranger);
                image.Image.SaveAsBmp(fs);
            }

            return true;
        }

        public bool ExportAllArrangers(string projectRoot)
        {
            foreach (var node in ResourceTree.EnumerateDepthFirst())
            {
                if(node.Value is ScatteredArranger)
                    ExportArranger(node.PathKey, projectRoot);
            }

            return true;
        }

        public bool ImportImage(string imageFileName, string arrangerKey)
        {
            Console.WriteLine($"Importing {imageFileName} to {arrangerKey}...");

            ResourceTree.TryGetValue(arrangerKey, out ScatteredArranger arranger);

            using (var image = new ArrangerImage())
            {
                image.LoadImage(imageFileName);
                image.SaveImage(arranger);
            }

            return true;
        }

        public bool ImportAllImages(string projectRoot)
        {
            foreach (var node in ResourceTree.EnumerateDepthFirst().Where(x => x.Value is ScatteredArranger))
            {
                var arranger = node.Value as ScatteredArranger;
                var relativeFile = Path.Combine(node.Paths.ToArray());
                var imageFileName = Path.Combine(projectRoot, relativeFile + ".bmp");
                if(File.Exists(imageFileName))
                    ImportImage(imageFileName, node.PathKey);
            }
            return true;
        }

        public bool ResaveProject(string newProjectFile)
        {
            var writer = new XmlGameDescriptorWriter();
            return writer.WriteProject(ResourceTree, newProjectFile);
        }
    }
}
