using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Tables;

namespace TileShop.CLI.Commands;

/// <summary>
/// Generates resources into a project from a table definition, or prints what would be generated (CLI-COMMANDS-070 to -079)
/// </summary>
public class GenerateHandler : ProjectCommandHandler<GenerateOptions>
{
    public GenerateHandler(IProjectService projectService, TextWriter output) :
        base(projectService, output)
    {
    }

    public override async Task<ExitCode> Execute(GenerateOptions options)
    {
        var projectTree = await OpenProject(options.ProjectFileName);

        if (projectTree is null)
            return ExitCode.ProjectOpenError;

        var reader = new XmlTableDefinitionReader(Path.Combine(AppContext.BaseDirectory, "_schemas"));
        var definitionResult = reader.LoadFromFile(options.DefinitionFileName);

        if (definitionResult.HasFailed)
        {
            foreach (var line in XmlTableDefinitionReader.DescribeErrors(options.DefinitionFileName, definitionResult.AsError.Reasons))
                Output.WriteLine(line);

            return ExitCode.GenerateOperationFailed;
        }

        var planResult = ProjectService.PlanGeneration(projectTree, options.DataFileKey, definitionResult.AsSuccess.Result, options.FolderKey,
            options.OverwriteEdited);
        if (planResult.HasFailed)
        {
            Output.WriteLine(planResult.AsError.Reason);
            return ExitCode.GenerateOperationFailed;
        }

        var plan = planResult.AsSuccess.Result;

        if (plan.Regeneration is { } regeneration)
            return await Regenerate(projectTree, plan, regeneration, options);

        if (options.DryRun)
        {
            foreach (var resource in plan.Resources)
                Output.WriteLine($"{resource.Type} {resource.Key} ({resource.Summary})");

            PrintReport(plan.Report, "Would create");
            return ExitCode.Success;
        }

        var generateResult = await ProjectService.GenerateResourcesAsync(projectTree, plan, options.SaveProject);
        if (generateResult.HasFailed)
        {
            Output.WriteLine(generateResult.AsError.Reason);
            return ExitCode.GenerateOperationFailed;
        }

        PrintReport(plan.Report, "Created");
        return ExitCode.Success;
    }

    private async Task<ExitCode> Regenerate(ProjectTree projectTree, GenerationPlan plan, RegenerationPlan regeneration, GenerateOptions options)
    {
        if (options.DryRun)
        {
            PrintLines(regeneration.ReportLines(plan.Report, true));
            return ExitCode.Success;
        }

        if (regeneration.Removed.Count > 0 && !options.Yes)
        {
            Output.WriteLine($"Regeneration would remove {regeneration.Removed.Count} resources; rerun with --yes to remove them");
            PrintLines(regeneration.Removed.Select(x => x.CurrentKey!));
            return ExitCode.GenerateOperationFailed;
        }

        var generateResult = await ProjectService.GenerateResourcesAsync(projectTree, plan, options.SaveProject, options.Yes);
        if (generateResult.HasFailed)
        {
            Output.WriteLine(generateResult.AsError.Reason);
            return ExitCode.GenerateOperationFailed;
        }

        PrintLines(regeneration.ReportLines(plan.Report, false));
        return ExitCode.Success;
    }

    private void PrintLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
            Output.WriteLine(line);
    }

    private void PrintReport(GenerationReport report, string verb) => PrintLines(report.CountLines(verb).Concat(report.IssueLines()));
}
