using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Image.Import;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters;

public enum ImportResult { Success, MissingFile, BadResourceKey, ReadOnly, UnmatchedColors, ImportFailed }

public static class Importer
{
    /// <param name="skipReadOnly">Reports an arranger made read-only by a codec that cannot encode as skipped (<see cref="ImportResult.ReadOnly"/>) rather than failed; a read-only data file always fails</param>
    public static ImportResult ImportImage(ProjectTree projectTree, string imageFileName, string arrangerKey,
        ImageImportOptions options, bool skipReadOnly, TextWriter output)
    {
        output.Write($"Importing '{imageFileName}' to '{arrangerKey}'...");

        if (!projectTree.TryGetItem<ScatteredArranger>(arrangerKey, out var arranger) || arranger is null)
        {
            output.WriteLine($"Resource key does not exist or is not a {nameof(ScatteredArranger)}");
            return ImportResult.BadResourceKey;
        }

        if (arranger.GetReadOnlyReason() is { } reason)
        {
            var isSkipped = skipReadOnly && !arranger.EnumerateElements().OfType<ArrangerElement>().Any(x => x.Source.IsReadOnly);
            output.WriteLine(isSkipped
                ? $"Skipped: arranger is read-only ({reason})"
                : $"Arranger is read-only because it {reason}");
            return isSkipped ? ImportResult.ReadOnly : ImportResult.ImportFailed;
        }

        if (arranger.FindMissingDataSource() is { } missing)
        {
            output.WriteLine($"Data file '{missing.Name}' is missing at '{missing.FileLocation}'");
            return ImportResult.ImportFailed;
        }

        if (!File.Exists(imageFileName))
        {
            output.WriteLine("File does not exist");
            return ImportResult.MissingFile;
        }

        var prepareResult = ImageImporter.Prepare(arranger, imageFileName, options, new ImageSharpFileAdapter());

        return prepareResult.Match(
            success =>
            {
                var preview = success.Result;

                if (!preview.CanCommit)
                {
                    output.WriteLine(preview.Report.ToSummary());
                    return ImportResult.UnmatchedColors;
                }

                preview.Commit();
                output.WriteLine($"Completed successfully ({preview.Report.ToSummary()})");
                return ImportResult.Success;
            },
            fail =>
            {
                output.WriteLine(fail.Reason);
                return ImportResult.ImportFailed;
            });
    }
}
