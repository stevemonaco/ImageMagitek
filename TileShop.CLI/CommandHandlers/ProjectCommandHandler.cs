using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Project;
using ImageMagitek.Services;

namespace TileShop.CLI.Commands;

public abstract class ProjectCommandHandler<T>
{
    protected IProjectService ProjectService { get; set; }

    public ProjectCommandHandler(IProjectService projectService)
    {
        ProjectService = projectService;
    }

    public virtual ExitCode TryExecute(T options)
    {
        try
        {
            return Execute(options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            return ExitCode.Exception;
        }
    }

    public abstract ExitCode Execute(T options);

    public virtual ProjectTree OpenProject(string projectFileName)
    {
        var openResult = ProjectService.OpenProjectFile(projectFileName);

        return openResult.Match(
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
        var headerMessage = $"Project '{projectFileName}' contained {errorMessages.Count} errors";
        Console.WriteLine(headerMessage);

        foreach (var message in errorMessages)
            Console.WriteLine(message);
    }
}
