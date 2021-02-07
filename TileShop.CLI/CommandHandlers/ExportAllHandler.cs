﻿using Monaco.PathTree;
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
            var project = OpenProject(options.ProjectFileName);

            if (project is null)
                return ExitCode.ProjectOpenError;

            foreach (var node in project.Tree.EnumerateDepthFirst().Where(x => x.Value is ScatteredArranger))
            {
                Exporter.ExportArranger(project, node.PathKey, options.ExportDirectory, options.ForceOverwrite);
            }

            return ExitCode.Success;
        }
    }
}