using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Project;
using ImageMagitek.Services;

namespace TileShop.CLI.Commands;

public abstract class ProjectCommandHandler<T>
{
    protected IProjectService ProjectService { get; }
    protected TextWriter Output { get; }

    public ProjectCommandHandler(IProjectService projectService, TextWriter output)
    {
        ProjectService = projectService;
        Output = output;
    }

    public virtual async Task<ExitCode> TryExecute(T options)
    {
        try
        {
            return await Execute(options);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            return ExitCode.Exception;
        }
    }

    public abstract Task<ExitCode> Execute(T options);

    public virtual async Task<ProjectTree?> OpenProject(string projectFileName)
    {
        var openResult = await ProjectService.OpenProjectFileAsync(projectFileName);

        return openResult.Match<ProjectTree?>(
            success =>
            {
                return success.Result;
            },
            fail =>
            {
                var errorMessages = Enumerable.Range(0, fail.Reasons.Count)
                    .Select(x => $"{x + 1}: {fail.Reasons[x]}")
                    .ToList();

                PrintProjectErrors(projectFileName, errorMessages);
                return null;
            });
    }

    public virtual void PrintProjectErrors(string projectFileName, IList<string> errorMessages)
    {
        Output.WriteLine($"Project '{projectFileName}' contained {errorMessages.Count} errors");

        foreach (var message in errorMessages)
            Output.WriteLine(message);
    }
}
