using System;
using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using TileShop.CLI.Commands;

namespace TileShop.CLI;

/// <summary>
/// Parses the command line, runs one verb against a project and maps the outcome to an exit code
/// </summary>
public sealed class CliApplication
{
    private const string AppName = "TileShopCLI";
    private const string LogTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}";

    private readonly TextWriter _output;
    private readonly Func<ILoggerFactory, (IProjectService Projects, IResourceGenerationService Generation)> _bootstrap;
    private readonly string _defaultLogFileName;

    private readonly Argument<string> _projectArgument = new("project") { Description = "Project file to open" };
    private readonly Argument<string> _exportDirectoryArgument = new("directory") { Description = "Directory the PNGs are written to, mirroring the project tree" };
    private readonly Argument<string> _importDirectoryArgument = new("directory") { Description = "Directory the PNGs are read from, mirroring the project tree" };
    private readonly Argument<string[]> _exportKeysArgument = new("keys") { Description = "Resource keys of the scattered arrangers to export", Arity = ArgumentArity.OneOrMore };
    private readonly Argument<string[]> _importKeysArgument = new("keys") { Description = "Resource keys of the scattered arrangers and tilemaps to import", Arity = ArgumentArity.OneOrMore };

    private readonly Option<bool> _overwriteOption = new("--overwrite") { Description = "Replace PNGs that already exist instead of skipping them" };
    private readonly Option<string?> _logOption = new("--log") { Description = "Append warnings and errors to this file instead of the application directory's log", HelpName = "file" };
    private readonly Option<bool> _skipMissingFilesOption = new("-f") { Description = "Skip arrangers whose PNG does not exist instead of failing" };
    private readonly Option<bool> _skipBadKeysOption = new("-r") { Description = "Skip keys that are not scattered arrangers in the project instead of failing" };
    private readonly Option<ColorMatchStrategy> _matchOption = new("--match")
    {
        Description = "How image colors are matched to palette entries for indexed arrangers",
        HelpName = "exact|nearest|nearestrgb",
        DefaultValueFactory = _ => ColorMatchStrategy.Exact
    };
    private readonly Option<double?> _maxDistanceOption = new("--max-distance") { Description = "With nearest matching, report colors farther than this from every palette entry as unmatched", HelpName = "n" };
    private readonly Argument<string> _dataFileArgument = new("datafile") { Description = "Key of the data file the table definition reads" };
    private readonly Argument<string> _definitionArgument = new("definition") { Description = "Table definition file" };
    private readonly Argument<string> _folderArgument = new("folder") { Description = "Key of the folder to generate into, created when missing" };
    private readonly Option<bool> _dryRunOption = new("--dry-run") { Description = "Print the resources and report without writing anything" };
    private readonly Option<bool> _saveProjectOption = new("--save-project") { Description = "Also save other project resources that changed on load, such as legacy codec names" };
    private readonly Option<bool> _yesOption = new("--yes") { Description = "Confirm removing resources whose records no longer exist" };
    private readonly Option<bool> _overwriteEditedOption = new("--overwrite-edited") { Description = "Also rewrite or remove resources edited since generation" };
    private readonly Option<bool> _transparentIndexZeroOption = new("--transparent-index0") { Description = "Map fully transparent pixels to palette index 0 without color matching" };

    private readonly RootCommand _root;
    private readonly Command _printCommand;
    private readonly Command _exportCommand;
    private readonly Command _exportAllCommand;
    private readonly Command _importCommand;
    private readonly Command _importAllCommand;
    private readonly Command _generateCommand;

    /// <param name="bootstrap">Builds the project and generation services, printing startup issues; throws when the environment cannot load</param>
    /// <param name="defaultLogFileName">Log file used, rolling monthly, when <c>--log</c> is not given</param>
    public CliApplication(TextWriter output, Func<ILoggerFactory, (IProjectService Projects, IResourceGenerationService Generation)> bootstrap, string defaultLogFileName)
    {
        _output = output;
        _bootstrap = bootstrap;
        _defaultLogFileName = defaultLogFileName;

        _printCommand = new Command("print", "Prints every resource in the project with its type and resource key")
        {
            _projectArgument, _logOption
        };
        _printCommand.Aliases.Add("Print");

        _exportCommand = new Command("export", "Exports scattered arrangers to PNG by resource key")
        {
            _projectArgument, _exportDirectoryArgument, _exportKeysArgument, _overwriteOption, _logOption
        };
        _exportCommand.Aliases.Add("Export");

        _exportAllCommand = new Command("exportall", "Exports every scattered arranger to PNG, mirroring the project tree")
        {
            _projectArgument, _exportDirectoryArgument, _overwriteOption, _logOption
        };
        _exportAllCommand.Aliases.Add("ExportAll");

        _importCommand = new Command("import", "Imports PNGs into scattered arrangers by resource key")
        {
            _projectArgument, _importDirectoryArgument, _importKeysArgument, _logOption,
            _skipMissingFilesOption, _skipBadKeysOption, _matchOption, _maxDistanceOption, _transparentIndexZeroOption
        };
        _importCommand.Aliases.Add("Import");

        _importAllCommand = new Command("importall", "Imports a PNG into every scattered arranger, skipping read-only arrangers")
        {
            _projectArgument, _importDirectoryArgument, _logOption,
            _skipMissingFilesOption, _skipBadKeysOption, _matchOption, _maxDistanceOption, _transparentIndexZeroOption
        };
        _importAllCommand.Aliases.Add("ImportAll");

        _generateCommand = new Command("generate", "Generates resources into a project folder from a table definition over a data file")
        {
            _projectArgument, _dataFileArgument, _definitionArgument, _folderArgument, _dryRunOption, _saveProjectOption, _yesOption, _overwriteEditedOption, _logOption
        };
        _generateCommand.Aliases.Add("Generate");

        _matchOption.Validators.Add(result =>
        {
            if (!Enum.IsDefined(result.GetValueOrDefault<ColorMatchStrategy>()))
                result.AddError("--match must be exact, nearest or nearestrgb");
        });
        _importCommand.Validators.Add(ValidateMaxDistance);
        _importAllCommand.Validators.Add(ValidateMaxDistance);

        _root = new RootCommand("Exports a TileShop project's scattered arrangers to PNG, imports edited PNGs back, and generates resources from table definitions")
        {
            _printCommand, _exportCommand, _exportAllCommand, _importCommand, _importAllCommand, _generateCommand
        };
    }

    private void ValidateMaxDistance(System.CommandLine.Parsing.CommandResult result)
    {
        if (result.GetValue(_maxDistanceOption) is not { } maxDistance)
            return;

        if (maxDistance < 0)
            result.AddError("--max-distance cannot be negative");
        else if (result.GetValue(_matchOption) == ColorMatchStrategy.Exact)
            result.AddError("--max-distance requires --match nearest or nearestrgb");
    }

    public async Task<int> RunAsync(string[] args)
    {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _output.WriteLine($"{AppName} v{version} by Klarth");

        var parseResult = _root.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null });

        if (parseResult.Action is not null)
        {
            parseResult.Invoke(new InvocationConfiguration { Output = _output, Error = _output });

            if (parseResult.Errors.Count == 0)
                return (int)ExitCode.Success;

            using var parseErrorLogger = CreateLogger(null);
            return (int)Report(ExitCode.InvalidCommandArguments, parseErrorLogger);
        }

        using var logger = CreateLogger(parseResult.GetValue(_logOption));
        using var loggerFactory = new LoggerFactory();
        loggerFactory.AddSerilog(logger);

        IProjectService projectService;
        IResourceGenerationService generationService;
        try
        {
            (projectService, generationService) = _bootstrap(loggerFactory);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"{AppName} environment failed to load: {ex.Message}");
            logger.Fatal(ex, "{AppName} environment failed to load", AppName);
            return (int)Report(ExitCode.EnvironmentError, logger);
        }

        try
        {
            var code = await ExecuteAsync(parseResult, projectService, generationService);
            return (int)Report(code, logger);
        }
        finally
        {
            projectService.CloseProjects();
        }
    }

    private Task<ExitCode> ExecuteAsync(ParseResult parseResult, IProjectService projectService, IResourceGenerationService generationService)
    {
        var command = parseResult.CommandResult.Command;
        var project = parseResult.GetRequiredValue(_projectArgument);

        if (command == _printCommand)
            return new PrintHandler(projectService, _output).TryExecute(new PrintOptions(project));

        if (command == _exportCommand)
            return new ExportHandler(projectService, _output).TryExecute(new ExportOptions(project,
                parseResult.GetRequiredValue(_exportDirectoryArgument), parseResult.GetRequiredValue(_exportKeysArgument),
                parseResult.GetValue(_overwriteOption)));

        if (command == _exportAllCommand)
            return new ExportAllHandler(projectService, _output).TryExecute(new ExportAllOptions(project,
                parseResult.GetRequiredValue(_exportDirectoryArgument), parseResult.GetValue(_overwriteOption)));

        if (command == _generateCommand)
            return new GenerateHandler(projectService, generationService, _output).TryExecute(new GenerateOptions(project,
                parseResult.GetRequiredValue(_dataFileArgument), parseResult.GetRequiredValue(_definitionArgument),
                parseResult.GetRequiredValue(_folderArgument), parseResult.GetValue(_dryRunOption), parseResult.GetValue(_saveProjectOption),
                parseResult.GetValue(_yesOption), parseResult.GetValue(_overwriteEditedOption)));

        var importOptions = new ImageImportOptions(parseResult.GetValue(_matchOption),
            parseResult.GetValue(_transparentIndexZeroOption), 0, parseResult.GetValue(_maxDistanceOption));

        if (command == _importCommand)
            return new ImportHandler(projectService, _output).TryExecute(new ImportOptions(project,
                parseResult.GetRequiredValue(_importDirectoryArgument), parseResult.GetRequiredValue(_importKeysArgument),
                parseResult.GetValue(_skipMissingFilesOption), parseResult.GetValue(_skipBadKeysOption), importOptions));

        if (command == _importAllCommand)
            return new ImportAllHandler(projectService, _output).TryExecute(new ImportAllOptions(project,
                parseResult.GetRequiredValue(_importDirectoryArgument),
                parseResult.GetValue(_skipMissingFilesOption), parseResult.GetValue(_skipBadKeysOption), importOptions));

        throw new InvalidOperationException($"No handler for command '{command.Name}'");
    }

    private ExitCode Report(ExitCode code, Serilog.ILogger logger)
    {
        var description = code switch
        {
            ExitCode.Success => "Operation completed successfully",
            ExitCode.Exception => "Operation failed due to an exception",
            ExitCode.EnvironmentError => "Operation failed because the TileShop environment could not be loaded",
            ExitCode.InvalidCommandArguments => "Operation failed due to invalid command line options",
            ExitCode.ProjectOpenError => "Operation failed because the project could not be opened or validated",
            ExitCode.ImportOperationFailed => "Operation failed due to an import error",
            ExitCode.ExportOperationFailed => "Operation failed due to an export error",
            ExitCode.GenerateOperationFailed => "Operation failed due to a generation error",
            _ => $"Operation failed with an unknown exit code '{code}'"
        };

        _output.WriteLine(description);

        if (code != ExitCode.Success)
            logger.Error("{Description}", description);

        return code;
    }

    private Serilog.Core.Logger CreateLogger(string? logFileName)
    {
        return new LoggerConfiguration()
            .WriteTo.File(logFileName is null ? _defaultLogFileName : Path.GetFullPath(logFileName),
                rollingInterval: logFileName is null ? RollingInterval.Month : RollingInterval.Infinite,
                outputTemplate: LogTemplate, restrictedToMinimumLevel: LogEventLevel.Warning)
            .CreateLogger();
    }
}
