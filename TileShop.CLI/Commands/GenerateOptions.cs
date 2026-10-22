namespace TileShop.CLI.Commands;

public sealed record GenerateOptions(string ProjectFileName, string DataFileKey, string DefinitionFileName, string FolderKey, bool DryRun, bool SaveProject,
    bool Yes, bool OverwriteEdited);
