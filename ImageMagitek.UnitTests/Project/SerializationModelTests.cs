using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class SerializationModelTests
{
    [Fact]
    public void ForeignColorSourceModel_SameValue_Equal()
    {
        var a = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));
        var b = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));

        Assert.True(a.ResourceEquals(b));
    }

    [Fact]
    public void ForeignColorSourceModel_DifferentModel_NotEqual()
    {
        var bgr15 = new ProjectForeignColorSourceModel(new ColorBgr15(0x7C1F));
        var abgr16 = new ProjectForeignColorSourceModel(new ColorAbgr16(0x7C1F));

        Assert.False(bgr15.ResourceEquals(abgr16));
    }
}
