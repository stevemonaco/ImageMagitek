using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class StreamReadExtensionTests
{
    private static readonly byte[] _singleArray = [0b10011101];
    private static readonly byte[] _largeArray = [0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001];

    public static TheoryData<byte[], BitAddress, int, byte[]> ReadUnshiftedCases => new()
    {
        { _largeArray, new BitAddress(0, 0), 1, new byte[] { 0b10000000 } },
        { _largeArray, new BitAddress(0, 2), 2, new byte[] { 0b00110000 } },
        { _largeArray, new BitAddress(0, 4), 4, new byte[] { 0b00000011 } },
        { _largeArray, new BitAddress(0, 0), 8, new byte[] { 0b10110011 } },

        { _largeArray, new BitAddress(0, 0), 9, new byte[] { 0b10110011, 0b10000000 } },
        { _largeArray, new BitAddress(1, 1), 9, new byte[] { 0b01111111, 0b01000000 } },

        { _largeArray, new BitAddress(4, 0), 8, new byte[] { 0b10000001 } },
        { _largeArray, new BitAddress(4, 1), 7, new byte[] { 0b00000001 } },
        { _largeArray, new BitAddress(4, 7), 1, new byte[] { 0b00000001 } },

        { _largeArray, new BitAddress(0, 1), 38, new byte[] { 0b00110011, 0b11111111, 0b01010101, 0b11001100, 0b10000000 } },
        { _largeArray, new BitAddress(0, 0), 40, _largeArray },
    };

    public static TheoryData<byte[], BitAddress, int, byte[]> ReadShiftedCases => new()
    {
        { _singleArray, new BitAddress(0, 0), 1, new byte[] { 0b10000000 } },
        { _singleArray, new BitAddress(0, 2), 2, new byte[] { 0b01000000 } },
        { _singleArray, new BitAddress(0, 4), 4, new byte[] { 0b11010000 } },
        { _singleArray, new BitAddress(0, 0), 8, new byte[] { 0b10011101 } },

        { _largeArray, new BitAddress(0, 0), 9, new byte[] { 0b10110011, 0b10000000 } },
        { _largeArray, new BitAddress(1, 1), 9, new byte[] { 0b11111110, 0b10000000 } },

        { _largeArray, new BitAddress(4, 0), 8, new byte[] { 0b10000001 } },
        { _largeArray, new BitAddress(4, 1), 7, new byte[] { 0b00000010 } },
        { _largeArray, new BitAddress(3, 1), 8, new byte[] { 0b10011001 } },
        { _largeArray, new BitAddress(4, 7), 1, new byte[] { 0b10000000 } },

        { _largeArray, new BitAddress(2, 1), 14, new byte[] { 0b10101011, 0b10011000 } },
        { _largeArray, new BitAddress(0, 1), 38, new byte[] { 0b01100111, 0b11111110, 0b10101011, 0b10011001, 0b00000010 } },
        { _largeArray, new BitAddress(0, 0), 40, _largeArray },
    };
}
