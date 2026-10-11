using System.IO;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class StartupIssueNotifierTests
{
    [Fact]
    public void Post_EachIssue_OneWarningNamingFileAndFirstLine()
    {
        var service = new NotificationService(NullLogger<NotificationService>.Instance, a => a());
        var badPath = Path.Combine("app", "_codecs", "Bad.xml");
        StartupIssue[] issues =
        [
            new(badPath, "Unexpected end of file\nat line 3"),
            new(Path.Combine("app", "_layouts", "Odd.json"), "pattern is empty\r\nsecond line")
        ];

        StartupIssueNotifier.Post(service, issues);

        Assert.Equal(2, service.Notifications.Count);
        Assert.All(service.Notifications, x =>
        {
            Assert.Equal(NotificationSeverity.Warning, x.Severity);
            Assert.Equal("Startup", x.Source);
        });
        Assert.Contains(service.Notifications, x => x.Title == "Odd.json: pattern is empty");
        var bad = Assert.Single(service.Notifications, x => x.Title == "Bad.xml: Unexpected end of file");
        Assert.Contains("at line 3", bad.Detail);
        Assert.Contains(badPath, bad.Detail);
    }
}
