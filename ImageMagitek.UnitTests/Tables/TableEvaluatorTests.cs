using System;
using System.Linq;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableEvaluatorTests : IDisposable
{
    private readonly TableTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private TableEvaluation Evaluate(string body, byte[] data, string records = "")
    {
        var definition = _context.LoadValid($"""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<record name="Parent" size="4">
            		<field name="kind" offset="0" bits="8" />
            		<field name="count" offset="1" bits="8" />
            		<field name="child" offset="2" bits="16" kind="pointer" to="Child" />
            	</record>
            	<record name="Child" size="2">
            		<field name="value" offset="0" bits="16" />
            	</record>
            {records}
            {body}
            </tabledefinition>
            """);

        return new TableEvaluator(definition, _context.Files.Open("Rom", data)).Evaluate();
    }

    private static byte[] Data(int length, params (int Offset, byte[] Bytes)[] parts)
    {
        var data = new byte[length];
        foreach (var (offset, bytes) in parts)
            bytes.CopyTo(data, offset);
        return data;
    }

    [Fact]
    public void SharedChild_YieldsOneInstance()
    {
        // Two parents at 0x10 and 0x14 point at one child at 0x20; a third points at another child at 0x22
        var data = Data(0x40, (0x10, [1, 0, 0x20, 0, 2, 0, 0x20, 0, 3, 0, 0x22, 0]), (0x20, [0x34, 0x12, 0x78, 0x56]));
        var evaluation = Evaluate("""<table name="parents" address="0x10" record="Parent" count="3" />""", data);

        Assert.Equal(["Parent 10", "Child 20", "Parent 14", "Parent 18", "Child 22"],
            evaluation.Instances.Select(x => $"{x.Type.Name} {x.Offset:X}"));

        var shared = evaluation.Instances[1];
        Assert.Equal(0x1234, shared.GetField("value"));
        Assert.Equal([0x10L, 0x14L], shared.References.Select(x => x.Parent!.Offset));
        Assert.All(shared.References, x => Assert.Equal("child", x.Via));
        Assert.Equal(0, shared.Index);
        Assert.Same(shared, evaluation.Instances[0].Children["child"]);
        Assert.Equal(2, evaluation.Instances[3].Index);
    }

    [Fact]
    public void SentinelTerminatedTable()
    {
        var data = Data(0x40, (0x10, [1, 0, 0, 0, 2, 0, 0, 0, 0xFF, 0, 0, 0, 4, 0, 0, 0]));
        var evaluation = Evaluate("""<table name="parents" address="0x10" record="Parent" until="kind == 0xFF" />""", data);

        Assert.Equal([1L, 2L], evaluation.Instances.Select(x => x.GetField("kind")));
        Assert.Empty(evaluation.Warnings);
    }

    [Fact]
    public void SentinelHitsEndOfData_Warns()
    {
        var data = Data(0x18, (0x10, [1, 0, 0, 0, 2, 0, 0, 0]));
        var evaluation = Evaluate("""<table name="parents" address="0x10" record="Parent" until="kind == 0xFF" />""", data);

        Assert.Equal(2, evaluation.Instances.Count);
        Assert.Contains(evaluation.Warnings, x => x.Contains("'parents'") && x.Contains("end of the data before its terminator"));
    }

    [Fact]
    public void ExpressionCount()
    {
        var data = Data(0x40, (0x10, [0, 3, 0x20, 0]), (0x20, [1, 0, 2, 0, 3, 0, 4, 0]));
        var evaluation = Evaluate("""<table name="owners" address="0x10" record="Owner" count="1" /><table name="values" record="Child" count="count - 1 + index" />""", data,
            """
            	<record name="Owner" size="4">
            		<field name="count" offset="1" bits="8" />
            		<field name="list" offset="2" bits="16" kind="pointer" to="values" />
            	</record>
            """);

        Assert.Equal([1L, 2L], evaluation.InstancesOf(evaluation.Instances[1].Type).Select(x => x.GetField("value")));
    }

    [Fact]
    public void ListLiteralCounts_IndexedByParent()
    {
        var data = Data(0x40, (0x10, [0x20, 0, 0x30, 0]), (0x20, [0x24, 0]), (0x24, [1, 0]), (0x30, [0x38, 0, 0x3A, 0]), (0x38, [2, 0, 3, 0]));
        var evaluation = Evaluate("""<table name="groups" address="0x10" pointer="members" pointerbits="16" count="2" /><table name="members" pointer="Child" pointerbits="16" counts="1, 2" />""", data);

        Assert.Equal([1L, 2L, 3L], evaluation.Instances.Select(x => x.GetField("value")));
        Assert.Equal([0, 0, 1], evaluation.Instances.Select(x => x.Index));
        Assert.All(evaluation.Instances, x => Assert.Null(Assert.Single(x.References).Parent));
    }

    [Fact]
    public void ListLiteralTooShort_Reported()
    {
        var data = Data(0x40, (0x10, [0x20, 0, 0x30, 0]), (0x20, [0x24, 0]), (0x24, [1, 0]), (0x30, [0x38, 0]), (0x38, [2, 0]));
        var evaluation = Evaluate("""<table name="groups" address="0x10" pointer="members" pointerbits="16" count="2" /><table name="members" pointer="Child" pointerbits="16" counts="1" />""", data);

        Assert.Equal([1L], evaluation.Instances.Select(x => x.GetField("value")));
        Assert.Contains(evaluation.Warnings, x => x.Contains("'members'") && x.Contains("has 1 counts, but row 1 points at it"));
    }

    [Fact]
    public void PointerPastEnd_ReportedPerRecord()
    {
        var data = Data(0x20, (0x10, [1, 0, 0xFF, 0x00, 2, 0, 0x1F, 0, 3, 0, 0x18, 0]));
        var evaluation = Evaluate("""<table name="parents" address="0x10" record="Parent" count="3" />""", data);

        Assert.Equal(["Parent 10", "Parent 14", "Parent 18", "Child 18"], evaluation.Instances.Select(x => $"{x.Type.Name} {x.Offset:X}"));
        Assert.Contains(evaluation.Skipped, x => x.Record == "Child" && x.Offset == 0xFF && x.Reason.Contains("Parent at 0x10 field 'child'"));
        Assert.Contains(evaluation.Skipped, x => x.Record == "Child" && x.Offset == 0x1F && x.ToString().StartsWith("Skipped Child at 0x1F: extends past the end of the data"));
    }

    [Fact]
    public void NullPointer_NoChild()
    {
        var data = Data(0x20, (0x10, [1, 0, 0, 0]));
        var evaluation = Evaluate("""<table name="parents" address="0x10" record="Parent" count="1" />""", data);

        var parent = Assert.Single(evaluation.Instances);
        Assert.Empty(parent.Children);
        Assert.Empty(evaluation.Skipped);
    }

    [Fact]
    public void Variant_SelectedByPredicate()
    {
        var data = Data(0x20, (0x10, [0x01, 0x22, 0x81, 0x33]));
        var evaluation = Evaluate("""<table name="sprites" address="0x10" record="Sprite" count="2" />""", data, """
            	<record name="Sprite" size="2">
            		<field name="size" offset="0" bits="8" />
            		<field name="palette" offset="1" bits="8" />
            		<variant name="boss" when="size &amp; 0x80">
            			<field name="palette" offset="1" bits="4" />
            			<field name="layout" offset="1" lowbit="4" bits="4" />
            		</variant>
            	</record>
            """);

        Assert.Null(evaluation.Instances[0].Variant);
        Assert.Equal(0x22, evaluation.Instances[0].GetField("palette"));
        Assert.Equal("boss", evaluation.Instances[1].Variant?.Name);
        Assert.Equal(3, evaluation.Instances[1].GetField("palette"));
        Assert.Equal(3, evaluation.Instances[1].GetField("layout"));
    }

    [Fact]
    public void Lookup_ReadsIndexedRecordField()
    {
        var data = Data(0x40, (0x10, [5, 0, 7, 0]), (0x20, [1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0]));
        var evaluation = Evaluate("""<lookup name="sizes" address="0x10" record="Child" count="2" /><table name="values" address="0x20" record="Child" count="sizes[1].value - sizes[0].value" />""", data);

        Assert.Equal(2, evaluation.Instances.Count(x => x.Offset >= 0x20));

        var outOfRange = Evaluate("""<lookup name="sizes" address="0x10" record="Child" count="2" /><table name="values" address="0x20" record="Child" count="sizes[2].value" />""", data);
        Assert.DoesNotContain(outOfRange.Instances, x => x.Offset >= 0x20);
        Assert.Contains(outOfRange.Warnings, x => x.Contains("index 2 is outside lookup 'sizes' of 2 entries"));
    }

    [Fact]
    public void UnmappedTableAddress_Warns()
    {
        var definition = _context.LoadValid("""
            <tabledefinition name="t">
            	<addressspace mapper="gba" />
            	<record name="R" size="1"><field name="a" offset="0" bits="8" /></record>
            	<table name="items" address="0x02000000" record="R" count="1" />
            </tabledefinition>
            """);

        var evaluation = new TableEvaluator(definition, _context.Files.Open("Rom", new byte[16])).Evaluate();

        Assert.Empty(evaluation.Instances);
        Assert.Contains(evaluation.Warnings, x => x.Contains("Table 'items' address 0x2000000 is unmapped"));
    }
}
