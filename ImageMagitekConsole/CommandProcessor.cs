using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using ImageMagitek;
using ImageMagitek.Project;

namespace ImageMagitekConsole
{
    public class CommandProcessor
    {
        private readonly IDictionary<string, GraphicsFormat> GraphicsFormats;
        private readonly ResourceManager Resources;

        public CommandProcessor(ResourceManager resourceManager, IDictionary<string, GraphicsFormat> formats)
        {
            Resources = resourceManager;
            GraphicsFormats = formats;
        }

        public bool PrintResources()
        {
            foreach (var res in Resources.TraverseDepthFirst())
            {
                string resourceKey = res.ResourceKey;
                int level = resourceKey.Split('\\').Length;

                Console.WriteLine($"{res.Name}({level}): {Resources.GetResourceType(res.ResourceKey).ToString()}");
            }

            return true;
        }

        public bool ExportArranger(string arrangerKey, string projectRoot)
        {
            var arranger = Resources.GetResource<ScatteredArranger>(arrangerKey);

            var exportFileName = Path.Combine(projectRoot, arrangerKey + ".bmp");
            Console.WriteLine($"Exporting {arranger.Name} to {exportFileName}...");

            Directory.CreateDirectory(Path.GetDirectoryName(exportFileName));

            using (var rm = new RenderManager())
            using (var fs = File.Create(exportFileName, 32 * 1024, FileOptions.SequentialScan))
            {
                rm.Render(arranger);
                rm.Image.SaveAsBmp(fs);
            }

            return true;
        }

        public bool ExportAllArrangers(string projectRoot)
        {
            foreach (var res in Resources.TraverseDepthFirst().OfType<ScatteredArranger>())
                ExportArranger(res.ResourceKey, projectRoot);

            return true;
        }


        public bool ImportImage(string imageFileName, string arrangerKey)
        {
            var arranger = Resources.GetResource<ScatteredArranger>(arrangerKey);

            using (var rm = new RenderManager())
            {
                rm.LoadImage(imageFileName);
                rm.SaveImage(arranger);
            }

            return true;
        }

        public bool ImportAllImages(string projectRoot)
        {
            foreach(var arranger in Resources.TraverseDepthFirst().OfType<ScatteredArranger>())
            {
                string imageFileName = Path.Combine(projectRoot, arranger.ResourceKey + ".bmp");
                if(File.Exists(imageFileName))
                    ImportImage(imageFileName, arranger.ResourceKey);
            }
            return true;
        }
    }
}
