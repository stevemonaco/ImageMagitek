using System;
using System.Collections.Generic;
using System.IO;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using TileShop.Shared.Services;

namespace TileShop.UI.ViewModels;

public static class StartupIssueNotifier
{
    /// <summary>
    /// Posts one warning per issue, titled with the file name and the first line of its reason
    /// </summary>
    public static void Post(INotificationService notifications, IEnumerable<StartupIssue> issues)
    {
        foreach (var issue in issues)
        {
            var firstLine = issue.Message.Split('\n')[0].TrimEnd('\r');
            notifications.Post(NotificationSeverity.Warning, "Startup", $"{Path.GetFileName(issue.Path)}: {firstLine}",
                issue.Message + Environment.NewLine + issue.Path);
        }
    }
}
