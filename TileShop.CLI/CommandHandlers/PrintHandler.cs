using System;
using ImageMagitek.Services;
using Monaco.PathTree;

namespace TileShop.CLI.Commands
{
    public class PrintHandler : ProjectCommandHandler<PrintOptions>
    {
        public PrintHandler(IProjectService projectService) :
            base(projectService)
        {
        }

        public override ExitCode Execute(PrintOptions options)
        {
            var project = OpenProject(options.ProjectFileName);

            if (project is null)
                return ExitCode.ProjectOpenError;

            foreach (var res in project.Tree.EnumerateDepthFirst())
            {
                string key = res.PathKey;
                Console.WriteLine($"{res.Name}: Type '{res.Value.GetType().Name}'; Resource Key '{key}'");
            }

            return ExitCode.Success;
        }
    }
}
