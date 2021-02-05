using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Services;
using Monaco.PathTree;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands
{
    public class ImportAllHandler : ProjectCommandHandler<ImportAllOptions>
    {
        public ImportAllHandler(IProjectService projectService) :
            base(projectService)
        {
        }

        public override ExitCode Execute(ImportAllOptions options)
        {
            var project = OpenProject(options.ProjectFileName);

            if (project is null)
                return ExitCode.ProjectOpenError;

            foreach (var node in project.Tree.EnumerateDepthFirst().Where(x => x.Value is ScatteredArranger))
            {
                var relativeFile = Path.Combine(node.Paths.ToArray());
                var imageFileName = Path.Combine(options.ImportDirectory, $"{relativeFile}.png");

                Importer.ImportImage(project, imageFileName, node.PathKey);
            }

            return ExitCode.Success;
        }
    }
}
