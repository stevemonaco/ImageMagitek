using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests;
public partial class ForeignColorTests
{
    public static TheoryData<IColor32, ColorRgba32> ToNativeTestCases => new()
    {
        // Native -> BGR15
        { new ColorBgr15(0, 0, 0), new ColorRgba32(0, 0, 0, 255) },
        { new ColorBgr15(25, 16, 4), new ColorRgba32(200, 128, 32, 255) },
        { new ColorBgr15(0, 31, 0), new ColorRgba32(0, 248, 0, 255) },
        { new ColorBgr15(31, 31, 31), new ColorRgba32(248, 248, 248, 255) },

        { new ColorBgr9(0, 0, 0), new ColorRgba32(0, 0, 0, 255) },
        { new ColorBgr9(7, 7, 7), new ColorRgba32(255, 255, 255, 255) },
        { new ColorBgr9(3, 5, 1), new ColorRgba32(109, 182, 36, 255) },
    };
}
