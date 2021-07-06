using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.UnitTests.ExtensionMethodTests
{
    public class MirrorArray2DTestCases
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

        private static byte[,] _rectangleArray = new byte[,]
        {
            { 00, 01, 02, 03, 04, 05, 06 },
            { 10, 11, 12, 13, 14, 15, 16 },
            { 20, 21, 22, 23, 24, 25, 26 }
        };

        public static IEnumerable<TestCaseData> MirrorHorizontalCases
        {
            get
            {
                var _horizontalEvenExpected = new byte[,]
                {
                    { 07, 06, 05, 04, 03, 02, 01, 00 },
                    { 17, 16, 15, 14, 13, 12, 11, 10 },
                    { 27, 26, 25, 24, 23, 22, 21, 20 },
                    { 37, 36, 35, 34, 33, 32, 31, 30 },
                    { 47, 46, 45, 44, 43, 42, 41, 40 },
                    { 57, 56, 55, 54, 53, 52, 51, 50 },
                    { 67, 66, 65, 64, 63, 62, 61, 60 },
                    { 77, 76, 75, 74, 73, 72, 71, 70 }
                };

                var _horizontalOddExpected = new byte[,]
                {
                    { 06, 05, 04, 03, 02, 01, 00 },
                    { 16, 15, 14, 13, 12, 11, 10 },
                    { 26, 25, 24, 23, 22, 21, 20 },
                    { 36, 35, 34, 33, 32, 31, 30 },
                    { 46, 45, 44, 43, 42, 41, 40 },
                    { 56, 55, 54, 53, 52, 51, 50 },
                    { 66, 65, 64, 63, 62, 61, 60 },
                };

                var _horizontalRectangleExpected = new byte[,]
                {
                    { 06, 05, 04, 03, 02, 01, 00 },
                    { 16, 15, 14, 13, 12, 11, 10 },
                    { 26, 25, 24, 23, 22, 21, 20 }
                };

                yield return new TestCaseData(_evenSquareArray.Clone(), _horizontalEvenExpected);
                yield return new TestCaseData(_oddSquareArray.Clone(), _horizontalOddExpected);
                yield return new TestCaseData(_rectangleArray.Clone(), _horizontalRectangleExpected);
                yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
            }
        }

        public static IEnumerable<TestCaseData> MirrorVerticalCases
        {
            get
            {
                var _verticalEvenExpected = new byte[,]
                {
                    { 70, 71, 72, 73, 74, 75, 76, 77 },
                    { 60, 61, 62, 63, 64, 65, 66, 67 },
                    { 50, 51, 52, 53, 54, 55, 56, 57 },
                    { 40, 41, 42, 43, 44, 45, 46, 47 },
                    { 30, 31, 32, 33, 34, 35, 36, 37 },
                    { 20, 21, 22, 23, 24, 25, 26, 27 },
                    { 10, 11, 12, 13, 14, 15, 16, 17 },
                    { 00, 01, 02, 03, 04, 05, 06, 07 }
                };

                var _verticalOddExpected = new byte[,]
                {
                    { 60, 61, 62, 63, 64, 65, 66 },
                    { 50, 51, 52, 53, 54, 55, 56 },
                    { 40, 41, 42, 43, 44, 45, 46 },
                    { 30, 31, 32, 33, 34, 35, 36 },
                    { 20, 21, 22, 23, 24, 25, 26 },
                    { 10, 11, 12, 13, 14, 15, 16 },
                    { 00, 01, 02, 03, 04, 05, 06 }
                };

                var _verticalRectangleExpected = new byte[,]
                {
                    { 20, 21, 22, 23, 24, 25, 26 },
                    { 10, 11, 12, 13, 14, 15, 16 },
                    { 00, 01, 02, 03, 04, 05, 06 }
                };

                yield return new TestCaseData(_evenSquareArray.Clone(), _verticalEvenExpected);
                yield return new TestCaseData(_oddSquareArray.Clone(), _verticalOddExpected);
                yield return new TestCaseData(_rectangleArray.Clone(), _verticalRectangleExpected);
                yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
            }
        }

        public static IEnumerable<TestCaseData> MirrorBothCases
        {
            get
            {
                var _bothEvenExpected = new byte[,]
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

                var _bothOddExpected = new byte[,]
                {
                    { 66, 65, 64, 63, 62, 61, 60 },
                    { 56, 55, 54, 53, 52, 51, 50 },
                    { 46, 45, 44, 43, 42, 41, 40 },
                    { 36, 35, 34, 33, 32, 31, 30 },
                    { 26, 25, 24, 23, 22, 21, 20 },
                    { 16, 15, 14, 13, 12, 11, 10 },
                    { 06, 05, 04, 03, 02, 01, 00 }
                };

                var _bothRectangleExpected = new byte[,]
                {
                    { 26, 25, 24, 23, 22, 21, 20 },
                    { 16, 15, 14, 13, 12, 11, 10 },
                    { 06, 05, 04, 03, 02, 01, 00 }
                };

                yield return new TestCaseData(_evenSquareArray.Clone(), _bothEvenExpected);
                yield return new TestCaseData(_oddSquareArray.Clone(), _bothOddExpected);
                yield return new TestCaseData(_rectangleArray.Clone(), _bothRectangleExpected);
                yield return new TestCaseData(_scalarArray.Clone(), _scalarArray);
            }
        }
    }
}
