using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace ImageMagitekConsole
{
    public enum ExitCode { Success = 0, InvalidCommandArguments = -1, ProjectValidationError = -2 }

    class Program
    {
        static readonly HashSet<string> _commands = new HashSet<string> { "export", "exportall", "import", "importall", "print", "resave" };
        static readonly string _projectSchemaFileName = Path.Combine("schema", "GameDescriptorSchema.xsd");
        static readonly string _codecSchemaFileName = Path.Combine("schema", "CodecSchema.xsd");

        static int Main(string[] args)
        {
            Console.WriteLine("ImageMagitek v0.06");
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ImageMagitek project.xml (ExportAll|ImportAll) ProjectRoot");
                Console.WriteLine("ImageMagitek project.xml (Export|Import) ProjectRoot ResourceKey1 ResourceKey2 ...");
            }

            string projectFileName = args[0];

            var command = args[1].ToLower();
            if (!_commands.Contains(command))
            {
                Console.WriteLine($"Invalid command {command}");
                return (int) ExitCode.InvalidCommandArguments;
            }

            string projectRoot = null;
            if (args.Length >= 3)
            {
                projectRoot = args[2];

                if (!Directory.Exists(projectRoot))
                    Directory.CreateDirectory(projectRoot);
            }

            // Load default graphic formats and palettes
            var codecPath = Path.Combine(Directory.GetCurrentDirectory(), "codecs");
            var formats = new Dictionary<string, GraphicsFormat>();
            var serializer = new XmlGraphicsFormatReader(_codecSchemaFileName);
            foreach (var formatFileName in Directory.GetFiles(codecPath).Where(x => x.EndsWith(".xml")))
            {
                var codecResult = serializer.LoadFromFile(formatFileName);
                codecResult.Switch(success =>
                {
                    formats.Add(success.Result.Name, success.Result);
                },
                fail =>
                {
                    Console.WriteLine($"Codec '{formatFileName}' contained {fail.Reasons.Count} error(s):");
                    Console.WriteLine(string.Join(Environment.NewLine, fail.Reasons));
                });
            }

            var palPath = Path.Combine(Directory.GetCurrentDirectory(), "pal");
            var palettes = new List<Palette>();

            foreach (var paletteFileName in Directory.GetFiles(palPath).Where(x => x.EndsWith(".json")))
            {
                string json = File.ReadAllText(paletteFileName);
                var pal = PaletteJsonSerializer.ReadPalette(json);
                palettes.Add(pal);
            }

            var defaultPalette = palettes.Single(x => x.Name.Contains("DefaultRgba32"));

            var schemaFileName = Path.Combine(Directory.GetCurrentDirectory(), "schema", "GameDescriptorValidator.xsd");
            var deserializer = new XmlGameDescriptorReader(schemaFileName, new CodecFactory(formats, defaultPalette));

            //IPathTree<IProjectResource> tree = default;
            var projectResult = deserializer.ReadProject(projectFileName);

            IPathTree<IProjectResource> tree = projectResult.Match(
                success => success.Result,
                fail =>
                {
                    Console.WriteLine($"'{projectFileName}' could not be parsed and contained {fail.Reasons.Count} errors");
                    foreach (var error in fail.Reasons)
                        Console.WriteLine(error);
                    return default;
                }
                );

            if (tree is null)
                return (int) ExitCode.ProjectValidationError;

            var processor = new CommandProcessor(tree, defaultPalette);

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
                case "print":
                    processor.PrintResources();
                    break;
                case "resave":
                    var newFileName = Path.GetFullPath(Path.GetFileNameWithoutExtension(projectFileName) + "-resave.xml");
                    processor.ResaveProject(newFileName);
                    break;
            }

            return (int) ExitCode.Success;
        }
    }
}
