using System.Collections.Generic;
using CommandLine;

namespace TileShop.CLI.Commands;

[Verb("Import", HelpText = "Imports one or more project resources by resource key, skipping resources that cannot be located")]
public class ImportOptions
{
    [Value(0, Required = true, HelpText = "Project to import resources into")]
    public string ProjectFileName { get; set; }

    [Value(1, Required = true, HelpText = "Directory containing all resources to be imported from")]
    public string ImportDirectory { get; set; }

    [Value(2, Required = true, Min = 1, HelpText = "Project resource keys to import")]
    public IEnumerable<string> ResourceKeys { get; set; }

    [Option("log", HelpText = "Log file name")]
    public string LogFileName { get; set; }

    [Option('f', HelpText = "Skip missing files")]
    public bool SkipMissingFiles { get; set; }

    [Option('r', HelpText = "Skip bad resource keys")]
    public bool SkipBadResourceKeys { get; set; }
}
