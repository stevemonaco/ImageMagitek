using System.IO;
using System.Threading.Tasks;
using ImageMagitek.Services;
using Monaco.PathTree;

namespace TileShop.CLI.Commands;

public class PrintHandler : ProjectCommandHandler<PrintOptions>
{
    public PrintHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(PrintOptions options)
    {
        var projectTree = await OpenProject(options.ProjectFileName);

        if (projectTree is null)
            return ExitCode.ProjectOpenError;

        foreach (var res in projectTree.EnumerateDepthFirst())
        {
            string key = projectTree.CreatePathKey(res);
            Output.WriteLine($"{res.Name}: Type '{res.Item.GetType().Name}'; Resource Key '{key}'");
        }

        return ExitCode.Success;
    }
}
