using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using ImageMagitek;
using ImageMagitek.Codec;
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

        public bool ExportAllArrangers(string basePath)
        {
            //Configuration.Default.ImageFormatsManager.SetEncoder(ImageFormats.Bmp, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32 });
            foreach (var res in Resources.TraverseDepthFirst().OfType<Arranger>())
            {
                var exportFileName = Path.Combine(basePath, res.ResourceKey) + ".bmp";
                Console.WriteLine($"Exporting {res.Name} to {exportFileName}...");
                var rm = new RenderManager();
                rm.Render(res);

                Directory.CreateDirectory(Path.GetDirectoryName(exportFileName));
                using (var fs = File.Create(exportFileName, 32 * 1024, FileOptions.SequentialScan))
                    rm.Image.SaveAsBmp(fs);
            }

            return true;
        }
    }
}
