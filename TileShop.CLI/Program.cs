using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Serilog;
using Microsoft.Extensions.Logging;

using LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;

namespace TileShop.CLI
{
    public enum ExitCode { Success = 0, InvalidCommandArguments = -1, ProjectValidationError = -2 }

    class Program
    {
        static readonly HashSet<string> _commands = new HashSet<string>
        {
            "export", "exportall", "import", "importall", "print", "resave"
        };

        static int Main(string[] args)
        {
            Console.WriteLine("ImageMagitek v0.1");
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

            var loggerFactory = CreateLoggerFactory(BootstrapService.DefaultLogFileName);
            var bootstrapper = new BootstrapService(loggerFactory.CreateLogger<BootstrapService>());

            var settings = bootstrapper.ReadConfiguration(BootstrapService.DefaultConfigurationFileName);
            var codecService = bootstrapper.CreateCodecService(BootstrapService.DefaultCodecPath, BootstrapService.DefaultCodecSchemaFileName);
            var paletteService = bootstrapper.CreatePaletteService(BootstrapService.DefaultPalettePath, settings);
            var projectService = bootstrapper.CreateProjectService(BootstrapService.DefaultProjectSchemaFileName, paletteService, codecService);

            var openResult = projectService.OpenProjectFile(projectFileName);

            ProjectTree project = openResult.Match(
                success =>
                {
                    return success.Result;
                },
                fail =>
                {
                    var message = $"Project '{projectFileName}' contained {fail.Reasons.Count} errors{Environment.NewLine}" +
                        string.Join(Environment.NewLine, fail.Reasons);
                    return null;
                });

            if (project is null)
                return (int)ExitCode.ProjectValidationError;

            var processor = new CommandProcessor(project);

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

        private static LoggerFactory CreateLoggerFactory(string logName)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File(logName, rollingInterval: RollingInterval.Month,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}")
                .CreateLogger();

            var factory = new LoggerFactory();
            factory.AddSerilog(Log.Logger);
            return factory;
        }
    }
}
