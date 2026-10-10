using ImageMagitek.Image.Import;

namespace TileShop.CLI.Commands;

public sealed record ImportAllOptions(string ProjectFileName, string ImportDirectory,
    bool SkipMissingFiles, bool SkipBadResourceKeys, ImageImportOptions ImageOptions);
