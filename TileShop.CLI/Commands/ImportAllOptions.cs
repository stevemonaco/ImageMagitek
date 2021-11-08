using CommandLine;

namespace TileShop.CLI.Commands;

[Verb("ImportAll", HelpText = "Imports all project resources, skipping resources that cannot be located")]
public class ImportAllOptions
{
    [Value(0, Required = true, HelpText = "Project to import resources into")]
    public string ProjectFileName { get; set; }

    [Value(1, Required = true, HelpText = "Directory containing all resources to be imported from")]
    public string ImportDirectory { get; set; }

    [Option("log", HelpText = "Log file name")]
    public string LogFileName { get; set; }

    [Option('f', HelpText = "Skip missing files")]
    public bool SkipMissingFiles { get; set; }

    [Option('r', HelpText = "Skip bad resource keys")]
    public bool SkipBadResourceKeys { get; set; }
}
