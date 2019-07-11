using System.Collections.Generic;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    public class NativeColorTestCases
    {
        public static IEnumerable<TestCaseData> ToForeignTestCases
        {
            get
            {
                // Native -> BGR15
                yield return new TestCaseData(new NativeColor(0, 0, 0, 0), new ForeignColor(0, 0, 0, 0, ColorModel.BGR15), ColorModel.BGR15);
                yield return new TestCaseData(new NativeColor(0, 200, 128, 39), new ForeignColor(0, 25, 16, 4, ColorModel.BGR15), ColorModel.BGR15);
                yield return new TestCaseData(new NativeColor(50, 0, 255, 0), new ForeignColor(0, 0, 31, 0, ColorModel.BGR15), ColorModel.BGR15);
                yield return new TestCaseData(new NativeColor(0, 248, 248, 248), new ForeignColor(0, 31, 31, 31, ColorModel.BGR15), ColorModel.BGR15);
                yield return new TestCaseData(new NativeColor(0, 255, 255, 255), new ForeignColor(0, 31, 31, 31, ColorModel.BGR15), ColorModel.BGR15);
            }
        }
    }
}
