using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Image.Import;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters;

public enum ImportResult { Success, MissingFile, BadResourceKey, ReadOnly, UnmatchedColors, ImportFailed, Skipped }

public static class Importer
{
    /// <param name="skipReadOnly">Reports an arranger made read-only by a codec that cannot encode or by compressed data as skipped (<see cref="ImportResult.ReadOnly"/>)
    /// rather than failed; a read-only data file always fails</param>
    public static ImportResult ImportImage(ProjectTree projectTree, string imageFileName, string arrangerKey,
        ImageImportOptions options, bool skipReadOnly, TextWriter output)
    {
        output.Write($"Importing '{imageFileName}' to '{arrangerKey}'...");

        projectTree.TryGetNode(arrangerKey, out var node);
        if (node?.Item is MetatileMap)
        {
            output.WriteLine("Skipped: metatile maps cannot be imported");
            return ImportResult.Skipped;
        }

        if (node?.Item is not IArrangerView arranger || arranger is not (ScatteredArranger or TilemapArranger))
        {
            output.WriteLine("Resource key does not exist or is not a Scattered Arranger or Tilemap");
            return ImportResult.BadResourceKey;
        }

        if (arranger.GetReadOnlyReason() is { } reason)
        {
            var isSkipped = skipReadOnly && !ReadsReadOnlyFile(arranger);
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

        if (arranger is TilemapArranger { MapError: { } mapError })
        {
            output.WriteLine(mapError);
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

                foreach (var conflict in preview.Report.SharedTileConflicts)
                {
                    var first = conflict.Cells[0];
                    output.WriteLine($"  Shared tile at 0x{conflict.Address.ByteOffset:X} differs between {conflict.Cells.Count} cells, first at ({first.X}, {first.Y}); not imported");
                }

                return ImportResult.Success;
            },
            fail =>
            {
                output.WriteLine(fail.Reason);
                return ImportResult.ImportFailed;
            });
    }

    private static bool ReadsReadOnlyFile(IArrangerView arranger)
    {
        var sources = arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Source);
        if (arranger is TilemapArranger tilemap)
            sources = sources.Append(tilemap.TileSource);

        return sources.Any(x => x is not CompressedDataSource && x.IsReadOnly);
    }
}
