using System.Collections.Generic;
using NUnit.Framework;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    public class NativeColorTestCases
    {
        public static IEnumerable<TestCaseData> ToForeignTestCases
        {
            get
            {
                // Native -> BGR15
                yield return new TestCaseData(new ColorRgba32(0, 0, 0, 0), new ColorBgr15(0, 0, 0), ColorModel.BGR15);
                yield return new TestCaseData(new ColorRgba32(200, 128, 39, 0), new ColorBgr15(25, 16, 4), ColorModel.BGR15);
                yield return new TestCaseData(new ColorRgba32(0, 255, 0, 50), new ColorBgr15(0, 31, 0), ColorModel.BGR15);
                yield return new TestCaseData(new ColorRgba32(48, 248, 248, 0), new ColorBgr15(6, 31, 31), ColorModel.BGR15);
                yield return new TestCaseData(new ColorRgba32(55, 255, 255, 0), new ColorBgr15(6, 31, 31), ColorModel.BGR15);
            }
        }
    }
}
