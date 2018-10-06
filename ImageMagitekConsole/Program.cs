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
        static readonly HashSet<string> Commands = new HashSet<string> { "export", "exportall", "import", "importall" };

        static void Main(string[] args)
        {
            Console.WriteLine("ImageMagitek v0.05");
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ImageMagitek project.xml (ExportAll|ImportAll) ProjectRoot");
                Console.WriteLine("ImageMagitek project.xml (Export|Import) ProjectRoot ResourceKey1 ResourceKey2 ...");
            }

            string projectFileName = args[0];

            var command = args[1].ToLower();
            if (!Commands.Contains(command))
            {
                Console.WriteLine($"Invalid command {command}");
                return;
            }

            var projectRoot = args[2];

            if (!Directory.Exists(projectRoot))
                Directory.CreateDirectory(projectRoot);        

            // Load default graphic formats and palettes
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

            var resourceManager = new ResourceManager(formats);
            resourceManager.LoadProject(projectFileName, Path.GetDirectoryName(Path.GetFullPath(projectFileName)));

            var processor = new CommandProcessor(resourceManager, formats);

            switch (command)
            {
                case "export":
                    foreach(var key in args.Skip(3))
                        processor.ExportArranger(key, projectRoot);
                    break;
                case "exportall":
                    processor.ExportAllArrangers(projectRoot);
                    break;
                case "import":
                    foreach (var key in args.Skip(3))
                        processor.ImportImage(Path.Combine(projectRoot, key + ".bmp"), key);
                    break;
                case "importall":
                    processor.ImportAllImages(projectRoot);
                    break;
            }
        }
    }
}
