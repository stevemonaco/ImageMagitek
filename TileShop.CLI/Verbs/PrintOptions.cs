using CommandLine;

namespace TileShop.CLI.Commands
{
    [Verb("Print", HelpText = "Prints all resource keys")]
    public class PrintOptions
    {
        [Value(0, Required = true)]
        public string ProjectFileName { get; set; }
    }
}
