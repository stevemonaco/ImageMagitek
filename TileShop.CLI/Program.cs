using System;
using System.Linq;
using System.Reflection;
using System.IO;
using ImageMagitek.Services;
using Serilog;
using Microsoft.Extensions.Logging;
using CommandLine;
using TileShop.CLI.Commands;

using LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;
using ImageMagitek.Project.Serialization;
using CommandLine.Text;
using CommunityToolkit.Diagnostics;

namespace TileShop.CLI;

class Program
{
    public static string DefaultLogFileName = "errorlogCLI.txt";
    public static IProjectService? ProjectService;
    public static LoggerFactory LoggerFactory = CreateLoggerFactory(DefaultLogFileName);
    private static string AppName => "TileShopCLI";
    private static string AppVersion => "0.992";

    static int Main(string[] args)
    {
        Console.WriteLine($"{AppName} v{AppVersion} by Klarth");

        //LoggerFactory = CreateLoggerFactory(DefaultLogFileName);
        var verbs = LoadVerbs();

        ExitCode code = ExitCode.Unset;

        var parser = new Parser(with => {
            with.CaseSensitive = false;
            with.AutoHelp = true;
            with.AutoVersion = true;
        });

        var parserResult = parser.ParseArguments(args, verbs)
            .WithNotParsed(errors =>
            {
                code = ExitCode.InvalidCommandArguments;
            })
            .WithParsed(options =>
            {
                var logFileName = GetFullLogFileName(options);
                if (!BootstrapTileShop(logFileName))
                    code = ExitCode.EnvironmentError;
                else
                    code = ExecuteHandler(options);
            });

        var errorCodeDescription = code switch
        {
            ExitCode.Success => "Operation completed successfully",
            ExitCode.Unset => "Operation exited without setting an exit code",
            ExitCode.Exception => "Operation failed due to an exception",
            ExitCode.EnvironmentError => "Operation failed because the TileShop environment could not be loaded",
            ExitCode.InvalidCommandArguments => $"Operation failed due to invalid command line options",
            ExitCode.ProjectOpenError => "Operation failed because the project could not be opened or validated",
            ExitCode.ImportOperationFailed => "Operation failed due to an import error",
            ExitCode.ExportOperationFailed => "Operation failed due to an export error",
            _ => $"Operation failed with an unknown exit code '{code}'"
        };

        if (code == ExitCode.InvalidCommandArguments)
        {
            DisplayHelp(parserResult);
        }

        if (code != ExitCode.Success)
        {
            Log.Error(errorCodeDescription);
        }
        else
        {
            Log.Information(errorCodeDescription);
        }

        return (int)code;
    }

    private static void DisplayHelp<T>(ParserResult<T> result)
    {
        var helpText = HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = false;
            h.Copyright = string.Empty;
            h.Heading = HeadingInfo.Empty;
            return h;
        },
        e => e,
        verbsIndex: true);
        Console.WriteLine(helpText);
    }

    private static Type[] LoadVerbs()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
    }

    private static bool BootstrapTileShop(string logFileName)
    {
        try
        {
            var bootstrapper = new BootstrapService(LoggerFactory.CreateLogger<BootstrapService>());

            var settingsFileName = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultConfigurationFileName);
            var codecPath = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultCodecPath);
            var codecSchemaFileName = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultCodecSchemaFileName);
            var palettePath = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultPalettePath);
            var pluginPath = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultPluginPath);
            var resourceSchemaFileName = Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultResourceSchemaFileName);

            var settings = bootstrapper.ReadConfiguration(settingsFileName);
            if (settings is null)
                throw new InvalidOperationException($"Failed to read configuration file '{settingsFileName}'");

            var colorFactory = bootstrapper.CreateColorFactory();
            var codecService = bootstrapper.CreateCodecService(codecPath, codecSchemaFileName);
            var paletteService = bootstrapper.CreatePaletteService(colorFactory);
            var paletteStore = bootstrapper.CreatePaletteStore(paletteService, palettePath, settings);

            if (paletteStore.NesPalette is not null)
                colorFactory.SetNesPalette(paletteStore.NesPalette);

            //var pluginService = bootstrapper.CreatePluginService(pluginPath, codecService);

            var defaultResources = paletteStore.GlobalPalettes;
            var serializerFactory = new XmlProjectSerializerFactory(resourceSchemaFileName,
                codecService.CodecFactory, colorFactory, defaultResources);
            ProjectService = bootstrapper.CreateProjectService(serializerFactory, colorFactory);

            return true;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, $"TileShopCLI environment failed to load:\n{ex.StackTrace}\n");
            return false;
        }
    }

    /// <summary>
    /// Executes the handler for an options object
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    private static ExitCode ExecuteHandler(object options)
    {
        if (ProjectService is null)
            throw new InvalidOperationException($"{nameof(ProjectService)} was not initialized");

        ExitCode code = ExitCode.Unset;

        switch (options)
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

    /// <summary>
    /// Gets the log file name from options object.
    /// If present, the log is stored relative to the working directory.
    /// If not present, the log is stored within the application directory with the default log name
    /// </summary>
    /// <param name="options"></param>
    /// <returns>A fully qualified file name to the log file</returns>
    private static string GetFullLogFileName(object options)
    {
        var logFileName = options switch
        {
            PrintOptions printOptions when printOptions.LogFileName is not null => printOptions.LogFileName,
            ExportOptions exportOptions when exportOptions.LogFileName is not null => exportOptions.LogFileName,
            ExportAllOptions exportAllOptions when exportAllOptions.LogFileName is not null => exportAllOptions.LogFileName,
            ImportOptions importOptions when importOptions.LogFileName is not null => importOptions.LogFileName,
            ImportAllOptions importAllOptions when importAllOptions.LogFileName is not null => importAllOptions.LogFileName,
            _ => Path.Combine(AppContext.BaseDirectory, DefaultLogFileName)
        };

        if (!Path.IsPathFullyQualified(logFileName))
            logFileName = Path.Combine(Directory.GetCurrentDirectory(), logFileName);

        return logFileName;
    }

    /// <summary>
    /// Creates a LoggerFactory from the given log filename
    /// </summary>
    /// <param name="logFileName"></param>
    /// <returns></returns>
    private static LoggerFactory CreateLoggerFactory(string logFileName)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logFileName, rollingInterval: RollingInterval.Month,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}",
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();

        var factory = new LoggerFactory();
        factory.AddSerilog(Log.Logger);
        return factory;
    }
}
