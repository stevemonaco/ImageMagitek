using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.UnitTests
{
    public class ForeignColorTestCases
    {
        public static IEnumerable<TestCaseData> ToNativeTestCases
        {
            get
            {
                // Native -> BGR15
                yield return new TestCaseData(new ForeignColor(0, 0, 0, 0, ColorModel.BGR15), new NativeColor(255, 0, 0, 0), ColorModel.BGR15);
                yield return new TestCaseData(new ForeignColor(0, 25, 16, 4, ColorModel.BGR15), new NativeColor(255, 200, 128, 32), ColorModel.BGR15);
                yield return new TestCaseData(new ForeignColor(1, 0, 31, 0, ColorModel.BGR15), new NativeColor(255, 0, 248, 0), ColorModel.BGR15);
                yield return new TestCaseData(new ForeignColor(0, 31, 31, 31, ColorModel.BGR15), new NativeColor(255, 248, 248, 248), ColorModel.BGR15);
            }
        }
    }
}
