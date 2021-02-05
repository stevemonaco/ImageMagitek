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

                Importer.ImportImage(project, imageFileName, resourceKey);
            }

            return ExitCode.Success;
        }
    }
}
