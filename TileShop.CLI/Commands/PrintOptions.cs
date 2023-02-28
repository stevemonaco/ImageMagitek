using CommandLine;

namespace TileShop.CLI.Commands;

[Verb("Print", HelpText = "Prints all resource keys")]
public class PrintOptions
{
    [Value(0, Required = true, HelpText = "Project to print resources from")]
    public string ProjectFileName { get; set; } = default!;

    [Option("log", HelpText = "Log file name")]
    public string? LogFileName { get; set; }
}
