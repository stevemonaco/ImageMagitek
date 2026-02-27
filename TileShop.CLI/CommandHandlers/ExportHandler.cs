using System.Threading.Tasks;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ExportHandler : ProjectCommandHandler<ExportOptions>
{
    public ExportHandler(IProjectService projectService) :
        base(projectService)
    {
    }

    public override async Task<ExitCode> Execute(ExportOptions options)
    {
        var project = await OpenProject(options.ProjectFileName);

        if (project is null)
            return ExitCode.ProjectOpenError;

        foreach (var resourceKey in options.ResourceKeys)
        {
            Exporter.ExportArranger(project, resourceKey, options.ExportDirectory, options.ForceOverwrite);
        }

        return ExitCode.Success;
    }
}
