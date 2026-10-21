using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Services;
using ImageMagitek.Tables;

namespace TileShop.CLI.Commands;

/// <summary>
/// Generates resources into a project from a table definition, or prints what would be generated (CLI-COMMANDS-070 to -074)
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

        var planResult = ProjectService.PlanGeneration(projectTree, options.DataFileKey, definitionResult.AsSuccess.Result, options.FolderKey);
        if (planResult.HasFailed)
        {
            Output.WriteLine(planResult.AsError.Reason);
            return ExitCode.GenerateOperationFailed;
        }

        var plan = planResult.AsSuccess.Result;

        if (options.DryRun)
        {
            foreach (var resource in plan.Resources)
                Output.WriteLine($"{resource.Type} {resource.Key} ({resource.Summary})");

            PrintReport(plan.Report, "Would create");
            return ExitCode.Success;
        }

        var generateResult = await ProjectService.GenerateResourcesAsync(projectTree, plan);
        if (generateResult.HasFailed)
        {
            Output.WriteLine(generateResult.AsError.Reason);
            return ExitCode.GenerateOperationFailed;
        }

        PrintReport(plan.Report, "Created");
        return ExitCode.Success;
    }

    private void PrintReport(GenerationReport report, string verb)
    {
        foreach (var line in report.CountLines(verb).Concat(report.IssueLines()))
            Output.WriteLine(line);
    }
}
