using System.IO;
using System.Linq;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableDefinitionReaderTests : System.IDisposable
{
    private readonly TableTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private static string Definition(string body) => $"""
        <tabledefinition name="t">
        	<addressspace mapper="linear" />
        	<charmap name="test" file="test.charmap.xml" />
        	<record name="Item" size="4">
        		<field name="kind" offset="0" bits="8" />
        		<field name="data" offset="1" bits="16" kind="pointer" />
        	</record>
        {body}
        </tabledefinition>
        """;

    private const string Table = """<table name="items" address="0x10" record="Item" count="2" />""";

    private static string PaletteOutput(string attributes = "", string children = "") =>
        $"""<output id="pal" record="Item" type="palette" offset="data" entries="16" color="Bgr15" {attributes}>{children}</output>""";

    [Fact]
    public void SchemaError_NamesFileAndLine()
    {
        var result = _context.LoadXml("""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<record name="Item" size="4" unknownattribute="1">
            		<field name="kind" offset="0" bits="8" />
            	</record>
            </tabledefinition>
            """);

        Assert.True(result.HasFailed);
        var reason = Assert.Single(result.AsError.Reasons);
        Assert.Contains("test.table.xml", reason);
        Assert.Contains("line 3", reason);
    }

    [Fact]
    public void UnknownField_FailsAtLoadNamingElementAndAttribute()
    {
        var result = _context.LoadXml(Definition($"""
            	{Table}
            	{PaletteOutput("when=\"missing > 0\"")}
            	<output id="other" record="Item" type="compresseddata" offset="nosuchfield" compression="GBA LZ77" />
            """));

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("output 'pal' attribute 'when'") && x.Contains("unknown identifier 'missing'") && x.Contains("line 9"));
        Assert.Contains(result.AsError.Reasons, x => x.Contains("output 'other' attribute 'offset'") && x.Contains("'nosuchfield'"));
    }

    [Fact]
    public void UnknownRecordType_FailsAtLoad()
    {
        var result = _context.LoadXml(Definition("""
            	<table name="items" address="0x10" record="Missing" count="2" />
            	<table name="both" address="0x10" record="Item" pointer="Item" count="2" />
            	<output id="pal" record="Nope" type="palette" offset="0" entries="16" color="Bgr15" />
            """));

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("table 'items' attribute 'record'") && x.Contains("'Missing'"));
        Assert.Contains(result.AsError.Reasons, x => x.Contains("table 'both'") && x.Contains("exactly one of 'record' and 'pointer'"));
        Assert.Contains(result.AsError.Reasons, x => x.Contains("output 'pal' attribute 'record'") && x.Contains("'Nope'"));
    }

    [Fact]
    public void UnknownCharmap_FailsAtLoad()
    {
        var missingFile = _context.LoadXml("""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<charmap name="gone" file="gone.charmap.xml" />
            </tabledefinition>
            """);

        Assert.True(missingFile.HasFailed);
        Assert.Contains(missingFile.AsError.Reasons, x => x.Contains("charmap 'gone' attribute 'file'") && x.Contains("gone.charmap.xml"));

        var unknownName = _context.LoadXml(Definition("""
            	<nametable name="names" address="0x10" kind="pointer" count="2" charmap="nosuch" />
            """));

        Assert.True(unknownName.HasFailed);
        Assert.Contains(unknownName.AsError.Reasons, x => x.Contains("nametable 'names' attribute 'charmap'") && x.Contains("'nosuch'"));
    }

    [Theory]
    [InlineData("data +")]
    [InlineData("(data")]
    [InlineData("data $ 2")]
    [InlineData("0x")]
    public void BadExpression_FailsAtLoad(string expression)
    {
        var result = _context.LoadXml(Definition($"""
            	{Table}
            	<output id="pal" record="Item" type="palette" offset="{expression}" entries="16" color="Bgr15" />
            """));

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("output 'pal' attribute 'offset'") && x.Contains(expression));
    }

    [Fact]
    public void FieldOutsideRecord_FailsAtLoad()
    {
        var result = _context.LoadXml("""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<record name="Item" size="2">
            		<field name="wide" offset="1" bits="16" />
            		<field name="index" offset="0" bits="8" />
            	</record>
            </tabledefinition>
            """);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("field 'wide' attribute 'offset'") && x.Contains("past the record size 2"));
        Assert.Contains(result.AsError.Reasons, x => x.Contains("'index' is a reserved word"));
    }

    [Fact]
    public void PendingOutputType_FailsLoad()
    {
        var result = _context.LoadXml(Definition($"""
            	{Table}
            	<output id="sprite" record="Item" type="arranger" layout="rect" />
            """));

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("output 'sprite' attribute 'type'") && x.Contains("'arranger' is not implemented yet"));
    }

    [Theory]
    [InlineData("FireRed.table.xml")]
    public void ShippedDefinitions_Load(string fileName)
    {
        var result = new XmlTableDefinitionReader(TableTestContext.SchemaDirectory).LoadFromFile(Path.Combine(TestPaths.TablesPath, fileName));

        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        var definition = result.AsSuccess.Result;
        Assert.Equal("gba", definition.Mapper.Name);
        Assert.Equal(43, definition.Tables.Single(x => x.Name == "groupHeaders").Counts!.Count);
        Assert.Equal(425, definition.Tables.Single(x => x.Name == "groupHeaders").Counts!.Sum());
        Assert.Equal("Special Area", definition.NameTables["sections"].Overrides[0xC4]);
        Assert.Equal(["tilesetSet", "tilesetTiles", "tilesetPalettes", "layoutMap", "layoutBorder"], definition.Outputs.Select(x => x.Id));
    }
}
