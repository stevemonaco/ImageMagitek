using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek;
using ImageMagitek.Services;
using Monaco.PathTree;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ImportAllHandler : ProjectCommandHandler<ImportAllOptions>
{
    public ImportAllHandler(IProjectService projectService) :
        base(projectService)
    {
    }

    public override async Task<ExitCode> Execute(ImportAllOptions options)
    {
        var project = await OpenProject(options.ProjectFileName);

        if (project is null)
            return ExitCode.ProjectOpenError;

        foreach (var node in project.EnumerateDepthFirst().Where(x => x.Item is ScatteredArranger))
        {
            var relativeFile = Path.Combine(project.CreatePaths(node).ToArray());
            var imageFileName = Path.Combine(options.ImportDirectory, $"{relativeFile}.png");

            var result = Importer.ImportImage(project, imageFileName, project.CreatePathKey(node));

            if (result == ImportResult.MissingFile && options.SkipMissingFiles is false)
            {
                return ExitCode.ImportOperationFailed;
            }
            else if (result == ImportResult.BadResourceKey && options.SkipBadResourceKeys is false)
            {
                return ExitCode.ImportOperationFailed;
            }
        }

        return ExitCode.Success;
    }
}
