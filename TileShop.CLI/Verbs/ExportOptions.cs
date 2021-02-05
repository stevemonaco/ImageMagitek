using System.Collections.Generic;
using CommandLine;

namespace TileShop.CLI.Commands
{
    [Verb("Export", HelpText = "Exports one or more project resources by resource key")]
    public class ExportOptions
    {
        [Value(0, Required = true, HelpText = "Project to export resources from")]
        public string ProjectFileName { get; set; }

        [Value(1, Required = true, HelpText = "Directory where all resources will be exported to")]
        public string ExportDirectory { get; set; }

        [Value(2, Required = true, Min = 1, HelpText = "Project resource keys to export")]
        public IEnumerable<string> ResourceKeys { get; set; }

        [Option('f', longName: "force", HelpText = "Forces an overwrite of existing files")]
        public bool ForceOverwrite { get; set; }
    }
}
