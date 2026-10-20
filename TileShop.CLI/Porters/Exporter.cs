using System;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters;

public static class Exporter
{
    /// <returns>False if the resource could not be exported; an existing PNG skipped without <paramref name="forceOverwrite"/> counts as success</returns>
    public static bool ExportArranger(ProjectTree projectTree, string arrangerKey, string projectRoot, bool forceOverwrite, TextWriter output)
    {
        if (!projectTree.TryGetNode(arrangerKey, out var node))
        {
            output.WriteLine($"Exporting '{arrangerKey}'...Resource key not found in project");
            return false;
        }

        if (node.Item is not (MetatileMap or Arranger and not SequentialArranger))
        {
            output.WriteLine($"Exporting '{arrangerKey}'...Resource key is not a Scattered Arranger, Tilemap or Metatile Map");
            return false;
        }

        var relativeFile = Path.Combine(projectTree.CreatePaths(node).ToArray());
        var exportFileName = Path.Combine(projectRoot, $"{relativeFile}.png");

        output.Write($"Exporting '{arrangerKey}' to '{exportFileName}'...");

        var missing = node.Item is MetatileMap missingMap ? missingMap.FindMissingDataSource() : ((Arranger)node.Item).FindMissingDataSource();
        if (missing is not null)
        {
            output.WriteLine($"Data file '{missing.Name}' is missing at '{missing.FileLocation}'");
            return false;
        }

        var mapError = node.Item switch
        {
            MetatileMap m => m.MapError,
            TilemapArranger t => t.MapError,
            _ => null
        };
        if (mapError is not null)
        {
            output.WriteLine(mapError);
            return false;
        }

        if (File.Exists(exportFileName) && !forceOverwrite)
        {
            output.WriteLine("File already exists and was skipped to not overwrite it");
            return true;
        }

        try
        {
            if (Path.GetDirectoryName(exportFileName) is { Length: > 0 } path)
                Directory.CreateDirectory(path);

            if (node.Item is MetatileMap map)
            {
                map.Composite().ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else if (node.Item is Arranger { ColorType: PixelColorType.Indexed } indexed)
            {
                new IndexedImage(indexed).ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else if (node.Item is Arranger { ColorType: PixelColorType.Direct } direct)
            {
                new DirectImage(direct).ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else
            {
                output.WriteLine($"Color type '{((Arranger)node.Item).ColorType}' is not supported");
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            output.WriteLine(ex.Message);
            return false;
        }

        output.WriteLine("Completed successfully");
        return true;
    }
}
