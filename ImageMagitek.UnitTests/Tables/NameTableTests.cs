using System;
using System.Linq;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class NameTableTests : IDisposable
{
    private readonly TableTestContext _context = new();
    private readonly TableEvaluation _evaluation;
    private readonly TableDefinition _definition;

    public NameTableTests()
    {
        _definition = _context.LoadValid("""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<charmap name="test" file="test.charmap.xml" />
            	<nametable name="sections" address="0x10" kind="pointer" count="3" charmap="test" indexbase="0x58" />
            	<nametable name="titled" address="0x10" kind="pointer" count="3" charmap="test" indexbase="0x58" case="title">
            		<override value="0x5A" text="CELADON special" />
            		<override value="0x99" text="Far Away" />
            	</nametable>
            	<nametable name="monsters" address="0x80" kind="fixed" width="6" count="2" charmap="test" />
            </tabledefinition>
            """);

        var data = new byte[0xC0];
        WritePointer(data, 0x10, 0x40);
        WritePointer(data, 0x14, 0x50);
        WritePointer(data, 0x18, 0x60);
        FireRedShapedData.Encode("PALLET TOWN").CopyTo(data, 0x40);
        new byte[] { 0xCD, 0xAD, 0xCD, 0xAD, 0x00, 0xBB, 0xC8, 0xC8, 0xBF, 0xFF }.CopyTo(data, 0x50);
        new byte[] { 0xC9, 0xBB, 0xC5, 0xB4, 0xCD, 0x00, 0xC6, 0xBB, 0xBC, 0xFF }.CopyTo(data, 0x60);
        FireRedShapedData.Encode("SLIME").CopyTo(data, 0x80);
        new byte[] { 0xC1, 0xC9, 0xBC, 0xC6, 0xC3, 0xC8 }.CopyTo(data, 0x86);

        _evaluation = new TableEvaluator(_definition, _context.Files.Open("Rom", data)).Evaluate();
    }

    public void Dispose() => _context.Dispose();

    private static void WritePointer(byte[] data, int offset, uint pointer) => BitConverter.GetBytes(pointer).CopyTo(data, offset);

    private string Lookup(string table, long value) => _evaluation.LookupName(_definition.NameTables[table], value);

    [Fact]
    public void PointerStrings()
    {
        Assert.Equal("PALLET TOWN", Lookup("sections", 0x58));
        Assert.Equal("S.S. ANNE", Lookup("sections", 0x59));
    }

    [Fact]
    public void FixedWidth()
    {
        Assert.Equal("SLIME", Lookup("monsters", 0));
        Assert.Equal("GOBLIN", Lookup("monsters", 1));
    }

    [Fact]
    public void IndexBase()
    {
        Assert.Equal("OAK'S LAB", Lookup("sections", 0x5A));
    }

    [Fact]
    public void Override_ReplacesName()
    {
        Assert.Equal("CELADON special", Lookup("titled", 0x5A));
        Assert.Equal("Far Away", Lookup("titled", 0x99));
        Assert.Empty(_evaluation.Warnings);
    }

    [Fact]
    public void TitleCase()
    {
        Assert.Equal("Pallet Town", Lookup("titled", 0x58));
        Assert.Equal("S.S. Anne", Lookup("titled", 0x59));
        Assert.Equal("Oak's Lab", NameTable.ToTitleCase("OAK'S LAB"));
        Assert.Equal("Mt.Moon 2F", NameTable.ToTitleCase("MT.MOON 2F"));
    }

    [Fact]
    public void IndexOutOfRange_FallsBack()
    {
        Assert.Equal("sections 0x57", Lookup("sections", 0x57));
        Assert.Equal("sections 0x5B", Lookup("sections", 0x5B));
        Assert.Equal("sections 0x5B", Lookup("sections", 0x5B));

        Assert.Equal(2, _evaluation.Warnings.Count(x => x.StartsWith("Name table 'sections' has no entry")));
    }
}
