using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagitek.Services;

namespace TileShop.UI.ViewModels;

public static class StartupIssueFormatter
{
    private const int MaximumListed = 20;

    /// <summary>
    /// Lists each issue's file name and the first line of its reason, at most <see cref="MaximumListed"/>, then the log location
    /// </summary>
    public static string Format(IReadOnlyList<StartupIssue> issues, string logDirectory)
    {
        var builder = new StringBuilder();

        foreach (var issue in issues.Take(MaximumListed))
        {
            var firstLine = issue.Message.Split('\n')[0].TrimEnd('\r');
            builder.AppendLine($"{Path.GetFileName(issue.Path)}: {firstLine}");
        }

        if (issues.Count > MaximumListed)
            builder.AppendLine($"and {issues.Count - MaximumListed} more");

        builder.AppendLine();
        builder.Append($"Details are in the log in {logDirectory}");

        return builder.ToString();
    }
}
