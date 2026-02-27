using System.Linq;
using System.Threading.Tasks;
using Monaco.PathTree;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ExportAllHandler : ProjectCommandHandler<ExportAllOptions>
{
    public ExportAllHandler(IProjectService projectService) :
        base(projectService)
    {
    }

    public override async Task<ExitCode> Execute(ExportAllOptions options)
    {
        var projectTree = await OpenProject(options.ProjectFileName);

        if (projectTree is null)
            return ExitCode.ProjectOpenError;

        foreach (var node in projectTree.EnumerateDepthFirst().Where(x => x.Item is ScatteredArranger))
        {
            Exporter.ExportArranger(projectTree, projectTree.CreatePathKey(node), options.ExportDirectory, options.ForceOverwrite);
        }

        return ExitCode.Success;
    }
}
