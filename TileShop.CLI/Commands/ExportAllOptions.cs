namespace TileShop.CLI.Commands;

public sealed record ExportAllOptions(string ProjectFileName, string ExportDirectory, bool ForceOverwrite);
