using System.IO;
using System.Threading.Tasks;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ImportHandler : ProjectCommandHandler<ImportOptions>
{
    public ImportHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(ImportOptions options)
    {
        var project = await OpenProject(options.ProjectFileName);

        if (project is null)
            return ExitCode.ProjectOpenError;

        foreach (var resourceKey in options.ResourceKeys)
        {
            var relativeFile = Path.Combine(resourceKey.Split(['\\', '/']));
            var imageFileName = Path.Combine(options.ImportDirectory, $"{relativeFile}.png");

            var result = Importer.ImportImage(project, imageFileName, resourceKey, options.ImageOptions, false, Output);

            var isSkipped = (result == ImportResult.MissingFile && options.SkipMissingFiles) ||
                (result == ImportResult.BadResourceKey && options.SkipBadResourceKeys);

            if (result != ImportResult.Success && !isSkipped)
                return ExitCode.ImportOperationFailed;
        }

        return ExitCode.Success;
    }
}
