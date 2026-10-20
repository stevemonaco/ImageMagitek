using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek;
using ImageMagitek.Metatiles;
using ImageMagitek.Services;
using Monaco.PathTree;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ImportAllHandler : ProjectCommandHandler<ImportAllOptions>
{
    public ImportAllHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(ImportAllOptions options)
    {
        var project = await OpenProject(options.ProjectFileName);

        if (project is null)
            return ExitCode.ProjectOpenError;

        foreach (var node in project.EnumerateDepthFirst().Where(x => x.Item is ScatteredArranger or TilemapArranger or MetatileMap))
        {
            var relativeFile = Path.Combine(project.CreatePaths(node).ToArray());
            var imageFileName = Path.Combine(options.ImportDirectory, $"{relativeFile}.png");

            var result = Importer.ImportImage(project, imageFileName, project.CreatePathKey(node), options.ImageOptions, true, Output);

            var isSkipped = result is ImportResult.ReadOnly or ImportResult.Skipped ||
                (result == ImportResult.MissingFile && options.SkipMissingFiles) ||
                (result == ImportResult.BadResourceKey && options.SkipBadResourceKeys);

            if (result != ImportResult.Success && !isSkipped)
                return ExitCode.ImportOperationFailed;
        }

        return ExitCode.Success;
    }
}
