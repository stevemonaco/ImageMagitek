using System.IO;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands
{
    public class ImportHandler : ProjectCommandHandler<ImportOptions>
    {
        public ImportHandler(IProjectService projectService) :
            base(projectService)
        {
        }

        public override ExitCode Execute(ImportOptions options)
        {
            var project = OpenProject(options.ProjectFileName);

            if (project is null)
                return ExitCode.ProjectOpenError;

            foreach (var resourceKey in options.ResourceKeys)
            {
                var relativeFile = Path.Combine(resourceKey.Split(new[] { '\\', '/' }));
                var imageFileName = Path.Combine(options.ImportDirectory, $"{relativeFile}.png");

                var result = Importer.ImportImage(project, imageFileName, resourceKey);

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
}
