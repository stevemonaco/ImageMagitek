using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests;
public partial class NativeColorTests
{
    public static TheoryData<ColorRgba32, IColor32, ColorModel> ToForeignTestCases => new()
    {
        // Native -> BGR15
        { new ColorRgba32(0, 0, 0, 0), new ColorBgr15(0, 0, 0), ColorModel.Bgr15 },
        { new ColorRgba32(200, 128, 39, 0), new ColorBgr15(25, 16, 4), ColorModel.Bgr15 },
        { new ColorRgba32(0, 255, 0, 50), new ColorBgr15(0, 31, 0), ColorModel.Bgr15 },
        { new ColorRgba32(48, 248, 248, 0), new ColorBgr15(6, 31, 31), ColorModel.Bgr15 },
        { new ColorRgba32(55, 255, 255, 0), new ColorBgr15(6, 31, 31), ColorModel.Bgr15 },
    };
}
