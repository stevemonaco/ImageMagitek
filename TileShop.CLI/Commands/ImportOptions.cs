using System.Collections.Generic;
using ImageMagitek.Image.Import;

namespace TileShop.CLI.Commands;

public sealed record ImportOptions(string ProjectFileName, string ImportDirectory, IReadOnlyList<string> ResourceKeys,
    bool SkipMissingFiles, bool SkipBadResourceKeys, ImageImportOptions ImageOptions);
