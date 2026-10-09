using System.IO;
using System.Linq;
using ImageMagitek.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class StartupIssueFormatterTests
{
    private static readonly string _logPath = Path.Combine(Path.GetTempPath(), "TileShop");

    [Fact]
    public void Format_ListsFilesReasonsAndLogPath()
    {
        StartupIssue[] issues =
        [
            new(Path.Combine("app", "_codecs", "Bad.xml"), "Unexpected end of file\nat line 3"),
            new(Path.Combine("app", "_layouts", "Odd.json"), "pattern is empty")
        ];

        var text = StartupIssueFormatter.Format(issues, _logPath);

        Assert.Contains("Bad.xml: Unexpected end of file", text);
        Assert.DoesNotContain("at line 3", text);
        Assert.Contains("Odd.json: pattern is empty", text);
        Assert.Contains(_logPath, text);
    }

    [Fact]
    public void Format_MoreThan20_SummarizesRest()
    {
        var issues = Enumerable.Range(0, 23).Select(i => new StartupIssue($"file{i}.xml", "reason")).ToList();

        var text = StartupIssueFormatter.Format(issues, _logPath);

        Assert.Contains("file19.xml", text);
        Assert.DoesNotContain("file20.xml", text);
        Assert.Contains("and 3 more", text);
    }
}
