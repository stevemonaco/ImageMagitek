using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Monaco.PathTree;
using ImageMagitek;
using ImageMagitek.Metatiles;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ExportAllHandler : ProjectCommandHandler<ExportAllOptions>
{
    public ExportAllHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(ExportAllOptions options)
    {
        var projectTree = await OpenProject(options.ProjectFileName);

        if (projectTree is null)
            return ExitCode.ProjectOpenError;

        var results = projectTree.EnumerateDepthFirst()
            .Where(x => x.Item is ScatteredArranger or TilemapArranger or MetatileMap)
            .Select(node => Exporter.ExportArranger(projectTree, projectTree.CreatePathKey(node), options.ExportDirectory, options.ForceOverwrite, Output))
            .ToList();

        return results.Contains(false) ? ExitCode.ExportOperationFailed : ExitCode.Success;
    }
}
