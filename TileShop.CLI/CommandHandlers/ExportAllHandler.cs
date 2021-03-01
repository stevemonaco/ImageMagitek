using Monaco.PathTree;
using ImageMagitek.Services;
using ImageMagitek;
using TileShop.CLI.Porters;
using System.Linq;

namespace TileShop.CLI.Commands
{
    public class ExportAllHandler : ProjectCommandHandler<ExportAllOptions>
    {
        public ExportAllHandler(IProjectService projectService) :
            base(projectService)
        {
        }

        public override ExitCode Execute(ExportAllOptions options)
        {
            var projectTree = OpenProject(options.ProjectFileName);

            if (projectTree is null)
                return ExitCode.ProjectOpenError;

            foreach (var node in projectTree.EnumerateDepthFirst().Where(x => x.Item is ScatteredArranger))
            {
                Exporter.ExportArranger(projectTree, node.PathKey, options.ExportDirectory, options.ForceOverwrite);
            }

            return ExitCode.Success;
        }
    }
}
