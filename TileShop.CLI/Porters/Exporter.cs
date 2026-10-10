using System;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.CLI.Porters;

public static class Exporter
{
    /// <returns>False if the arranger could not be exported; an existing PNG skipped without <paramref name="forceOverwrite"/> counts as success</returns>
    public static bool ExportArranger(ProjectTree projectTree, string arrangerKey, string projectRoot, bool forceOverwrite, TextWriter output)
    {
        if (!projectTree.TryGetNode(arrangerKey, out var node))
        {
            output.WriteLine($"Exporting '{arrangerKey}'...Resource key not found in project");
            return false;
        }

        if (node.Item is not ScatteredArranger arranger)
        {
            output.WriteLine($"Exporting '{arrangerKey}'...Resource key is not a Scattered Arranger");
            return false;
        }

        var relativeFile = Path.Combine(projectTree.CreatePaths(node).ToArray());
        var exportFileName = Path.Combine(projectRoot, $"{relativeFile}.png");

        output.Write($"Exporting '{arrangerKey}' to '{exportFileName}'...");

        if (arranger.FindMissingDataSource() is { } missing)
        {
            output.WriteLine($"Data file '{missing.Name}' is missing at '{missing.FileLocation}'");
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

            if (arranger.ColorType == PixelColorType.Indexed)
            {
                new IndexedImage(arranger).ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else if (arranger.ColorType == PixelColorType.Direct)
            {
                new DirectImage(arranger).ExportImage(exportFileName, new ImageSharpFileAdapter());
            }
            else
            {
                output.WriteLine($"Color type '{arranger.ColorType}' is not supported");
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            output.WriteLine(ex.Message);
            return false;
        }

        output.WriteLine("Completed successfully");
        return true;
    }
}
