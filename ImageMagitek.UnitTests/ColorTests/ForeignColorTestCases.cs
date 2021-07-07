using NUnit.Framework;
using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests
{
    public class ForeignColorTestCases
    {
        public static IEnumerable<TestCaseData> ToNativeTestCases
        {
            get
            {
                // Native -> BGR15
                yield return new TestCaseData(new ColorBgr15(0, 0, 0), new ColorRgba32(0, 0, 0, 255));
                yield return new TestCaseData(new ColorBgr15(25, 16, 4), new ColorRgba32(200, 128, 32, 255));
                yield return new TestCaseData(new ColorBgr15(0, 31, 0), new ColorRgba32(0, 248, 0, 255));
                yield return new TestCaseData(new ColorBgr15(31, 31, 31), new ColorRgba32(248, 248, 248, 255));
            }
        }
    }
}
