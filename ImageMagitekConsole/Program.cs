using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Project;

namespace ImageMagitekConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ImageMagitek v0.01");
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ImageMagitek project.xml (ExportAll|ImportAll) DestinationRoot");
                Console.WriteLine("ImageMagitek project.xml (Export|Import) DestinationRoot ResourceName1 ResourceName2 ...");
            }


            var codecPath = Path.Combine(Directory.GetCurrentDirectory(), "codecs");
            var formats = new Dictionary<string, GraphicsFormat>();
            var serializer = new XmlGraphicsFormatSerializer();
            foreach (var formatFileName in Directory.GetFiles(codecPath).Where(x => x.EndsWith(".xml")))
            {
                var format = serializer.LoadFromFile(formatFileName);
                formats.Add(format.Name, format);
            }

            var palPath = Path.Combine(Directory.GetCurrentDirectory(), "pal");
            var palettes = new List<Palette>();
            foreach(var paletteFileName in Directory.GetFiles(palPath).Where(x => x.EndsWith(".pal")))
            {
                var pal = new Palette(Path.GetFileNameWithoutExtension(paletteFileName));
                pal.LoadPalette(paletteFileName);
                palettes.Add(pal);
            }

            string projectFileName = args[0];
            var resourceManager = new ResourceManager(formats);
            resourceManager.LoadProject(projectFileName, Path.GetDirectoryName(Path.GetFullPath(projectFileName)));

            var command = args[1];

            var destinationRoot = args[2];
            if (!Directory.Exists(destinationRoot))
                Directory.CreateDirectory(destinationRoot);
            var processor = new CommandProcessor(resourceManager, formats);
            processor.ExportAllArrangers(destinationRoot);
        }
    }
}
