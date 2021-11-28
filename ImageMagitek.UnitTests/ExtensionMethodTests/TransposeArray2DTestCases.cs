using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

public class TransposeArray2DTestCases
{
    private static byte[,] _scalarArray = new byte[1, 1] { { 1 } };
    private static byte[,] _evenSquareArray = new byte[,]
    {
            { 00, 01, 02, 03, 04, 05, 06, 07 },
            { 10, 11, 12, 13, 14, 15, 16, 17 },
            { 20, 21, 22, 23, 24, 25, 26, 27 },
            { 30, 31, 32, 33, 34, 35, 36, 37 },
            { 40, 41, 42, 43, 44, 45, 46, 47 },
            { 50, 51, 52, 53, 54, 55, 56, 57 },
            { 60, 61, 62, 63, 64, 65, 66, 67 },
            { 70, 71, 72, 73, 74, 75, 76, 77 }
    };

    private static byte[,] _oddSquareArray = new byte[,]
    {
            { 00, 01, 02, 03, 04, 05, 06 },
            { 10, 11, 12, 13, 14, 15, 16 },
            { 20, 21, 22, 23, 24, 25, 26 },
            { 30, 31, 32, 33, 34, 35, 36 },
            { 40, 41, 42, 43, 44, 45, 46 },
            { 50, 51, 52, 53, 54, 55, 56 },
            { 60, 61, 62, 63, 64, 65, 66 }
    };

    public static IEnumerable<TestCaseData> TransposeCases
    {
        get
        {
            var _evenExpected = new byte[,]
            {
                    { 00, 10, 20, 30, 40, 50, 60, 70 },
                    { 01, 11, 21, 31, 41, 51, 61, 71 },
                    { 02, 12, 22, 32, 42, 52, 62, 72 },
                    { 03, 13, 23, 33, 43, 53, 63, 73 },
                    { 04, 14, 24, 34, 44, 54, 64, 74 },
                    { 05, 15, 25, 35, 45, 55, 65, 75 },
                    { 06, 16, 26, 36, 46, 56, 66, 76 },
                    { 07, 17, 27, 37, 47, 57, 67, 77 }
            };

            var _oddExpected = new byte[,]
            {
                    { 00, 10, 20, 30, 40, 50, 60 },
                    { 01, 11, 21, 31, 41, 51, 61 },
                    { 02, 12, 22, 32, 42, 52, 62 },
                    { 03, 13, 23, 33, 43, 53, 63 },
                    { 04, 14, 24, 34, 44, 54, 64 },
                    { 05, 15, 25, 35, 45, 55, 65 },
                    { 06, 16, 26, 36, 46, 56, 66 }
            };

            yield return new TestCaseData(_evenSquareArray.Clone(), _evenExpected);
            yield return new TestCaseData(_oddSquareArray.Clone(), _oddExpected);
            yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
        }
    }
}
