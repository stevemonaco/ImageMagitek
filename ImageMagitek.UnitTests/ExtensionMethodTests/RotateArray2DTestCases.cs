using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

public class RotateArray2DTestCases
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

    public static IEnumerable<TestCaseData> RotateLeftCases
    {
        get
        {
            var leftEvenExpected = new byte[,]
            {
                    { 07, 17, 27, 37, 47, 57, 67, 77 },
                    { 06, 16, 26, 36, 46, 56, 66, 76 },
                    { 05, 15, 25, 35, 45, 55, 65, 75 },
                    { 04, 14, 24, 34, 44, 54, 64, 74 },
                    { 03, 13, 23, 33, 43, 53, 63, 73 },
                    { 02, 12, 22, 32, 42, 52, 62, 72 },
                    { 01, 11, 21, 31, 41, 51, 61, 71 },
                    { 00, 10, 20, 30, 40, 50, 60, 70 }
            };

            var leftOddExpected = new byte[,]
            {
                    { 06, 16, 26, 36, 46, 56, 66 },
                    { 05, 15, 25, 35, 45, 55, 65 },
                    { 04, 14, 24, 34, 44, 54, 64 },
                    { 03, 13, 23, 33, 43, 53, 63 },
                    { 02, 12, 22, 32, 42, 52, 62 },
                    { 01, 11, 21, 31, 41, 51, 61 },
                    { 00, 10, 20, 30, 40, 50, 60 }
            };

            yield return new TestCaseData(_evenSquareArray.Clone(), leftEvenExpected);
            yield return new TestCaseData(_oddSquareArray.Clone(), leftOddExpected);
            yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
        }
    }

    public static IEnumerable<TestCaseData> RotateRightCases
    {
        get
        {
            var leftEvenExpected = new byte[,]
            {
                    { 70, 60, 50, 40, 30, 20, 10, 00 },
                    { 71, 61, 51, 41, 31, 21, 11, 01 },
                    { 72, 62, 52, 42, 32, 22, 12, 02 },
                    { 73, 63, 53, 43, 33, 23, 13, 03 },
                    { 74, 64, 54, 44, 34, 24, 14, 04 },
                    { 75, 65, 55, 45, 35, 25, 15, 05 },
                    { 76, 66, 56, 46, 36, 26, 16, 06 },
                    { 77, 67, 57, 47, 37, 27, 17, 07 }
            };

            var leftOddExpected = new byte[,]
            {
                    { 60, 50, 40, 30, 20, 10, 00 },
                    { 61, 51, 41, 31, 21, 11, 01 },
                    { 62, 52, 42, 32, 22, 12, 02 },
                    { 63, 53, 43, 33, 23, 13, 03 },
                    { 64, 54, 44, 34, 24, 14, 04 },
                    { 65, 55, 45, 35, 25, 15, 05 },
                    { 66, 56, 46, 36, 26, 16, 06 }
            };

            yield return new TestCaseData(_evenSquareArray.Clone(), leftEvenExpected);
            yield return new TestCaseData(_oddSquareArray.Clone(), leftOddExpected);
            yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
        }
    }

    public static IEnumerable<TestCaseData> RotateTurnCases
    {
        get
        {
            var turnEvenExpected = new byte[,]
            {
                    { 77, 76, 75, 74, 73, 72, 71, 70 },
                    { 67, 66, 65, 64, 63, 62, 61, 60 },
                    { 57, 56, 55, 54, 53, 52, 51, 50 },
                    { 47, 46, 45, 44, 43, 42, 41, 40 },
                    { 37, 36, 35, 34, 33, 32, 31, 30 },
                    { 27, 26, 25, 24, 23, 22, 21, 20 },
                    { 17, 16, 15, 14, 13, 12, 11, 10 },
                    { 07, 06, 05, 04, 03, 02, 01, 00 }
            };

            var turnOddExpected = new byte[,]
            {
                    { 66, 65, 64, 63, 62, 61, 60 },
                    { 56, 55, 54, 53, 52, 51, 50 },
                    { 46, 45, 44, 43, 42, 41, 40 },
                    { 36, 35, 34, 33, 32, 31, 30 },
                    { 26, 25, 24, 23, 22, 21, 20 },
                    { 16, 15, 14, 13, 12, 11, 10 },
                    { 06, 05, 04, 03, 02, 01, 00 }
            };

            yield return new TestCaseData(_evenSquareArray.Clone(), turnEvenExpected);
            yield return new TestCaseData(_oddSquareArray.Clone(), turnOddExpected);
            yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
        }
    }
}
