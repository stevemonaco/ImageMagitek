using System.Linq;
using ImageMagitek.Project;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class ResourceNameTests
{
    public static TheoryData<string, bool, string> RejectedNames => new()
    {
        { "", false, "empty" },
        { " a", false, "start with whitespace" },
        { "a ", false, "end with whitespace" },
        { "a.", false, "end with '.'" },
        { "a<b", false, "'<'" },
        { "a>b", false, "'>'" },
        { "a:b", false, "':'" },
        { "a\"b", false, "'\"'" },
        { "a/b", false, "'/'" },
        { "a\\b", false, "'\\'" },
        { "a|b", false, "'|'" },
        { "a?b", false, "'?'" },
        { "a*b", false, "'*'" },
        { "\u0001", false, "control character" },
        { "con", false, "'con' is a reserved device name" },
        { "Nul.sfc", false, "'Nul' is a reserved device name" },
        { "COM1", false, "reserved device name" },
        { "LPT¹", false, "reserved device name" },
        { new string('a', 101), false, "100 characters" },
        { string.Concat(Enumerable.Repeat("\U0001F600", 61)), false, "240 bytes" },
        { "x.XML", true, "'.xml'" },
    };

    [Theory]
    [MemberData(nameof(RejectedNames))]
    public void Validate_Rejects(string name, bool isFolder, string expectedMessage)
    {
        var result = ResourceName.Validate(name, isFolder);

        Assert.True(result.HasFailed);
        Assert.Contains(expectedMessage, result.AsError.Reason);
    }

    public static TheoryData<string, bool> AcceptedNames => new()
    {
        { "FF2.sfc", false },
        { "New Folder (2)", true },
        { "Console", false },
        { "COM10", false },
        { ".hidden", false },
        { "Ünïcödé", false },
        { new string('a', 100), false },
        { "x.xml", false },
    };

    [Theory]
    [MemberData(nameof(AcceptedNames))]
    public void Validate_Accepts(string name, bool isFolder)
    {
        var result = ResourceName.Validate(name, isFolder);

        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
    }

    [Fact]
    public void AreSame_CaseVariants_AreSame() => Assert.True(ResourceName.AreSame("Data", "data"));

    [Fact]
    public void AreSame_ComposedAndDecomposed_AreSame() => Assert.True(ResourceName.AreSame("é", "é"));

    [Fact]
    public void AreSame_DifferentNames_AreNotSame() => Assert.False(ResourceName.AreSame("data", "data2"));

    [Fact]
    public void AreSame_LoneSurrogate_DoesNotThrow() => Assert.True(ResourceName.AreSame("a\uD800", "A\uD800"));
}
