using System;
using ImageMagitek.Services;
using Monaco.PathTree;
using Serilog;

namespace TileShop.CLI.Commands;

public class PrintHandler : ProjectCommandHandler<PrintOptions>
{
    public PrintHandler(IProjectService projectService) :
        base(projectService)
    {
    }

    public override ExitCode Execute(PrintOptions options)
    {
        var projectTree = OpenProject(options.ProjectFileName);

        if (projectTree is null)
            return ExitCode.ProjectOpenError;

        foreach (var res in projectTree.EnumerateDepthFirst())
        {
            string key = projectTree.CreatePathKey(res);
            Log.Information($"{res.Name}: Type '{res.Item.GetType().Name}'; Resource Key '{key}'");
        }

        return ExitCode.Success;
    }
}
