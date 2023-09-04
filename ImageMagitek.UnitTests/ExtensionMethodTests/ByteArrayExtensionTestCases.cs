using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests;

public class ByteArrayExtensionTestCases
{
    private static readonly byte[] _emptyArray = new byte[] { };
    private static readonly byte[] _singleArray = new byte[] { 0b10011101 };
    private static readonly byte[] _largeArray = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001 };

    public static IEnumerable<TestCaseData> ShiftLeftCases
    {
        get
        {
            yield return new TestCaseData(_emptyArray.Clone(), 0, _emptyArray);
            yield return new TestCaseData(_emptyArray.Clone(), 5, _emptyArray);

            yield return new TestCaseData(_singleArray.Clone(), 0, _singleArray);
            yield return new TestCaseData(_singleArray.Clone(), 4, new byte[] { 0b11010000 });
            yield return new TestCaseData(_singleArray.Clone(), 7, new byte[] { 0b10000000 });

            yield return new TestCaseData(_largeArray.Clone(), 0, _largeArray);
            yield return new TestCaseData(_largeArray.Clone(), 5, new byte[] { 0b01111111, 0b11101010, 0b10111001, 0b10010000, 0b00100000 });
            yield return new TestCaseData(_largeArray.Clone(), 7, new byte[] { 0b11111111, 0b10101010, 0b11100110, 0b01000000, 0b10000000 });
        }
    }

    public static IEnumerable<TestCaseData> ShiftRightCases
    {
        get
        {
            yield return new TestCaseData(_emptyArray.Clone(), 0, _emptyArray);
            yield return new TestCaseData(_emptyArray.Clone(), 5, _emptyArray);

            yield return new TestCaseData(_singleArray.Clone(), 0, _singleArray);
            yield return new TestCaseData(_singleArray.Clone(), 4, new byte[] { 0b00001001 });
            yield return new TestCaseData(_singleArray.Clone(), 7, new byte[] { 0b00000001 });

            yield return new TestCaseData(_largeArray.Clone(), 0, _largeArray);
            yield return new TestCaseData(_largeArray.Clone(), 5, new byte[] { 0b00000101, 0b10011111, 0b11111010, 0b10101110, 0b01100100 });
            yield return new TestCaseData(_largeArray.Clone(), 7, new byte[] { 0b00000001, 0b01100111, 0b11111110, 0b10101011, 0b10011001 });
        }
    }
}
