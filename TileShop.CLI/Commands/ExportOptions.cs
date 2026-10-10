using System.Collections.Generic;

namespace TileShop.CLI.Commands;

public sealed record ExportOptions(string ProjectFileName, string ExportDirectory, IReadOnlyList<string> ResourceKeys,
    bool ForceOverwrite);
