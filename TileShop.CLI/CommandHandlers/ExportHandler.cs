using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Services;
using TileShop.CLI.Porters;

namespace TileShop.CLI.Commands;

public class ExportHandler : ProjectCommandHandler<ExportOptions>
{
    public ExportHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(ExportOptions options)
    {
        var project = await OpenProject(options.ProjectFileName);

        if (project is null)
            return ExitCode.ProjectOpenError;

        var results = options.ResourceKeys
            .Select(key => Exporter.ExportArranger(project, key, options.ExportDirectory, options.ForceOverwrite, Output))
            .ToList();

        return results.Contains(false) ? ExitCode.ExportOperationFailed : ExitCode.Success;
    }
}
