using System;
using System.IO;
using ImageMagitek;
using ImageMagitek.Image.Import;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters;

public enum ImportResult { Success, MissingFile, BadResourceKey, UnmatchedColors, ImportFailed }

public static class Importer
{
    public static ImportResult ImportImage(ProjectTree projectTree, string imageFileName, string arrangerKey)
    {
        Console.Write($"Importing '{imageFileName}' to '{arrangerKey}'...");

        if (!File.Exists(imageFileName))
        {
            Console.WriteLine($"File does not exist");
            return ImportResult.MissingFile;
        }

        if (!projectTree.TryGetItem<ScatteredArranger>(arrangerKey, out var arranger) || arranger is null)
        {
            Console.WriteLine($"Resource key does not exist or is not a {nameof(ScatteredArranger)}");
            return ImportResult.BadResourceKey;
        }

        var prepareResult = ImageImporter.Prepare(arranger, imageFileName, ImageImportOptions.Default, new ImageSharpFileAdapter());

        return prepareResult.Match(
            success =>
            {
                var preview = success.Result;

                if (!preview.CanCommit)
                {
                    Console.WriteLine(preview.Report.ToSummary());
                    return ImportResult.UnmatchedColors;
                }

                preview.Commit();
                Console.WriteLine($"Completed successfully ({preview.Report.ToSummary()})");
                return ImportResult.Success;
            },
            fail =>
            {
                Console.WriteLine(fail.Reason);
                return ImportResult.ImportFailed;
            });
    }
}
