using System;
using System.Linq;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class RecordFieldTests
{
    public static TheoryData<int, int, Endian> Alignments()
    {
        var data = new TheoryData<int, int, Endian>();
        foreach (var endian in new[] { Endian.Little, Endian.Big })
        {
            for (var lowBit = 0; lowBit < 8; lowBit++)
            {
                foreach (var bits in new[] { 1, 3, 8, 10, 16, 24, 31, 56 })
                    data.Add(lowBit, bits, endian);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Alignments))]
    public void BitFields_EveryAlignmentAndEndian(int lowBit, int bits, Endian endian)
    {
        var mask = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
        var value = 0xA5C3_9E71_5B2D_6F48UL & mask;
        var byteCount = (lowBit + bits + 7) / 8;

        // Surrounding bits set, so a wrong shift or mask shows
        var assembled = (value << lowBit) | ~(mask << lowBit);
        var bytes = Enumerable.Range(0, byteCount).Select(i => (byte)(assembled >> (8 * i))).ToArray();
        if (endian == Endian.Big)
            Array.Reverse(bytes);

        Assert.Equal(value, RecordInstance.ReadBits(bytes, lowBit, bits, endian));
    }

    [Fact]
    public void Pointer_EvaluatesToMappedOffset()
    {
        using var context = new TableTestContext();
        var definition = context.LoadValid("""
            <tabledefinition name="t">
            	<addressspace mapper="gba" />
            	<record name="Item" size="12">
            		<field name="target" offset="0" bits="32" kind="pointer" />
            		<field name="none" offset="4" bits="32" kind="pointer" />
            		<field name="bad" offset="8" bits="32" kind="pointer" />
            	</record>
            	<table name="items" address="0x08000010" record="Item" count="1" />
            </tabledefinition>
            """);

        var bytes = new byte[0x40];
        BitConverter.GetBytes(0x08000123u).CopyTo(bytes, 0x10);
        BitConverter.GetBytes(0x02000000u).CopyTo(bytes, 0x18);
        var evaluation = new TableEvaluator(definition, context.Files.Open("Rom", bytes)).Evaluate();

        var item = Assert.Single(evaluation.Instances);
        Assert.Equal(0x10, item.Offset);
        Assert.Equal(0x123, item.GetField("target"));
        Assert.Equal(0, item.GetField("none"));
        var error = Assert.Throws<TableEvaluationException>(() => item.GetField("bad"));
        Assert.Contains("unmapped pointer 0x2000000", error.Message);
    }
}
