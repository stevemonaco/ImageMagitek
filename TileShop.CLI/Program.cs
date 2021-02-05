using System;
using System.Linq;
using System.Reflection;
using ImageMagitek.Services;
using Serilog;
using Microsoft.Extensions.Logging;
using CommandLine;
using TileShop.CLI.Commands;

using LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;
using System.Diagnostics;

namespace TileShop.CLI
{
    class Program
    {
        public static IProjectService ProjectService { get; set; }

        static int Main(string[] args)
        {
            string version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;

            Console.WriteLine($"TileShopCLI v{version} by Klarth");

            var loggerFactory = CreateLoggerFactory(BootstrapService.DefaultLogFileName);
            var bootstrapper = new BootstrapService(loggerFactory.CreateLogger<BootstrapService>());

            var settings = bootstrapper.ReadConfiguration(BootstrapService.DefaultConfigurationFileName);
            var codecService = bootstrapper.CreateCodecService(BootstrapService.DefaultCodecPath, BootstrapService.DefaultCodecSchemaFileName);
            var paletteService = bootstrapper.CreatePaletteService(BootstrapService.DefaultPalettePath, settings);
            ProjectService = bootstrapper.CreateProjectService(BootstrapService.DefaultProjectSchemaFileName, paletteService, codecService);

            var verbs = LoadVerbs();

            ExitCode code = ExitCode.Unset;

            var parser = new Parser(with => with.CaseSensitive = false);
            var parserResult = parser.ParseArguments(args, verbs)
                .WithParsed(options => code = ExecuteHandler(options))
                .WithNotParsed(x => code = ExitCode.InvalidCommandArguments);

            var errorCodeDescription = code switch
            {
                ExitCode.Success => "Operation completed successfully",
                ExitCode.Unset => "Operation exited without setting an exit code",
                ExitCode.Exception => "Operation failed due to an exception",
                ExitCode.InvalidCommandArguments => "Operation failed due to invalid command line options",
                ExitCode.ProjectOpenError => "Operation failed because the project could not be opened or validated",
                _ => $"Operation failed with an unknown exit code '{code}'"
            };

            Console.WriteLine($"{errorCodeDescription}");

            return (int) ExitCode.Success;
        }

        private static Type[] LoadVerbs()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
        }

        private static ExitCode ExecuteHandler(object obj)
        {
            ExitCode code = ExitCode.Unset;

            switch (obj)
            {
                case PrintOptions printOptions:
                    var printHandler = new PrintHandler(ProjectService);
                    code = printHandler.TryExecute(printOptions);
                    break;
                case ExportOptions exportOptions:
                    var exportHandler = new ExportHandler(ProjectService);
                    code = exportHandler.TryExecute(exportOptions);
                    break;
                case ExportAllOptions exportAllOptions:
                    var exportAllHandler = new ExportAllHandler(ProjectService);
                    code = exportAllHandler.TryExecute(exportAllOptions);
                    break;
                case ImportOptions importOptions:
                    var importHandler = new ImportHandler(ProjectService);
                    code = importHandler.TryExecute(importOptions);
                    break;
                case ImportAllOptions importAllOptions:
                    var importAllHandler = new ImportAllHandler(ProjectService);
                    code = importAllHandler.TryExecute(importAllOptions);
                    break;
            }

            return code;
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
