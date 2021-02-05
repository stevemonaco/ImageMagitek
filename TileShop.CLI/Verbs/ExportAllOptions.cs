using CommandLine;

namespace TileShop.CLI.Commands
{
    [Verb("ExportAll", HelpText = "Exports all project resources")]
    public class ExportAllOptions
    {
        [Value(0, Required = true, HelpText = "Project to export resources from")]
        public string ProjectFileName { get; set; }

        [Value(1, Required = true, HelpText = "The directory where all resources will be exported to")]
        public string ExportDirectory { get; set; }

        [Option('f', longName: "force", HelpText = "Forces an overwrite of existing files")]
        public bool ForceOverwrite { get; set; }
    }
}
