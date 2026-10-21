using System;
using System.IO;
using System.Linq;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tables;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableGeneratorTests : IDisposable
{
    private const string SetsFolder = "/Generated/Tilesets";

    private readonly TableTestContext _context = new();

    public TableGeneratorTests()
    {
        _context.CopyFixtures();
    }

    public void Dispose() => _context.Dispose();

    private string ProjectFile => Path.Combine(_context.Directory, "project.xml");

    private GenerationPlan PlanSynthetic(Func<string, string>? edit = null)
    {
        if (edit is not null)
            _context.Write("Synthetic.table.xml", edit(File.ReadAllText(Path.Combine(TableTestContext.FixturesDirectory, "Synthetic.table.xml"))));

        var result = _context.Load("Synthetic.table.xml");
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);

        var source = _context.Files.Open("Rom", FireRedShapedData.Create());
        return new TableGenerator(result.AsSuccess.Result, source, "/Rom", "/Generated", MapFormatTests.LoadShippedFormats()).Plan(ProjectFile);
    }

    private static byte[] MiniData(params byte[][] items)
    {
        var data = new byte[0x80];
        for (var i = 0; i < items.Length; i++)
            items[i].CopyTo(data, 0x10 + 4 * i);

        new byte[] { 0xBB, 0xBC, 0xFF, 0x00, 0xBD, 0xBE, 0xFF, 0x00 }.CopyTo(data, 0x40);
        return data;
    }

    private GenerationPlan PlanMini(string outputs, byte[]? data = null)
    {
        var definition = _context.LoadValid($"""
            <tabledefinition name="t">
            	<addressspace mapper="linear" />
            	<charmap name="test" file="test.charmap.xml" />
            	<record name="Item" size="4">
            		<field name="kind" offset="0" bits="8" />
            		<field name="value" offset="1" bits="8" />
            		<variant name="boss" when="kind &amp; 0x80">
            			<field name="layout" offset="1" bits="4" />
            		</variant>
            	</record>
            	<nametable name="names" address="0x40" kind="fixed" width="4" count="2" charmap="test" />
            	<table name="items" address="0x10" record="Item" count="3" />
            {outputs}
            </tabledefinition>
            """);

        data ??= MiniData([0, 1, 0, 0], [1, 2, 0, 0], [1, 3, 0, 0]);
        return new TableGenerator(definition, _context.Files.Open("Mini", data), "/Rom", "/Gen", MapFormatTests.LoadShippedFormats()).Plan(ProjectFile);
    }

    private static T Model<T>(GenerationPlan plan, string key) where T : ResourceModel =>
        Assert.IsType<T>(Assert.Single(plan.Resources, x => x.Key == key).Model);

    [Fact]
    public void SharedTileset_OneSetReferencedByBothMaps()
    {
        var plan = PlanSynthetic();

        Assert.Equal([("compresseddata", 1), ("palette", 13), ("metatileset", 3), ("metatilemap", 3)], plan.Report.Counts);
        Assert.Equal(["compresseddata", "palette", "metatileset", "metatilemap"], plan.Resources.Select(x => x.Type).Distinct());

        var pallet = Model<MetatileMapModel>(plan, "/Generated/Maps/Pallet Town/Pallet Town");
        var viridian = Model<MetatileMapModel>(plan, "/Generated/Maps/Viridian City/Viridian City 1");
        Assert.Equal([($"{SetsFolder}/General", 0), ($"{SetsFolder}/Pallet Town", 640)], pallet.Sets);
        Assert.Equal([($"{SetsFolder}/General", 0), ($"{SetsFolder}/Viridian City", 640)], viridian.Sets);
        Assert.Equal((0x300L, 3, 2, "/Rom"), (pallet.MapOffset, pallet.Width, pallet.Height, pallet.MapDataKey));
        Assert.Equal("3x2 blocks", plan.Find("/Generated/Maps/Pallet Town/Pallet Town")!.Summary);

        var border = Model<MetatileMapModel>(plan, "/Generated/Borders/Pallet Town/Pallet Town Border");
        Assert.Equal((0x400L, 2, 2), (border.MapOffset, border.Width, border.Height));

        Assert.Single(plan.Resources, x => x.Type == "metatileset" && x.Offset == FireRedShapedData.Primary);
        Assert.Equal(["/Generated", "/Generated/Tilesets", "/Generated/Tilesets/Compressed", "/Generated/Tilesets/Palettes",
            "/Generated/Maps", "/Generated/Maps/Pallet Town", "/Generated/Maps/Viridian City", "/Generated/Borders", "/Generated/Borders/Pallet Town"],
            plan.FolderKeys);
        Assert.Empty(plan.Report.Skipped);
    }

    [Fact]
    public void PalettesDedupedByAddressAndEntries()
    {
        var plan = PlanSynthetic();

        var palettes = plan.Resources.Where(x => x.Type == "palette").ToList();
        Assert.Equal(13, palettes.Count);
        Assert.All(palettes, x => Assert.Equal(("palette", 16, "16 colors"), (x.Record, x.Entries!.Value, x.Summary)));

        var palletSet = Model<MetatileSetModel>(plan, $"{SetsFolder}/Pallet Town");
        var viridianSet = Model<MetatileSetModel>(plan, $"{SetsFolder}/Viridian City");
        Assert.Equal(Enumerable.Range(7, 6), palletSet.PaletteSlots.Keys.Order());
        Assert.Equal(palletSet.PaletteSlots.OrderBy(x => x.Key), viridianSet.PaletteSlots.OrderBy(x => x.Key));
        Assert.Equal("/Generated/Tilesets/Palettes/Pallet Town 7", palletSet.PaletteSlots[7]);

        var slot12 = Assert.IsType<PaletteModel>(plan.Find("/Generated/Tilesets/Palettes/Pallet Town 12")!.Model);
        var source = Assert.IsType<FileColorSourceModel>(Assert.Single(slot12.ColorSources));
        Assert.Equal((FireRedShapedData.SecondaryPalettes + 0x20 * 12, 16), (source.FileAddress, source.Entries));
        Assert.Equal((Colors.ColorModel.Bgr15, true, "/Rom"), (slot12.ColorModel, slot12.ZeroIndexTransparent, slot12.DataFileKey));
        Assert.Equal(5, plan.Find("/Generated/Tilesets/Palettes/Pallet Town 12")!.Index);
    }

    [Fact]
    public void CompressedAndUncompressedTiles()
    {
        var plan = PlanSynthetic();

        var tiles = Model<CompressedDataModel>(plan, $"{SetsFolder}/Compressed/General Tiles");
        Assert.Equal(("/Rom", FireRedShapedData.PrimaryTiles, "GBA LZ77"), (tiles.ParentKey, tiles.FileOffset, tiles.Compression));

        var general = Model<MetatileSetModel>(plan, $"{SetsFolder}/General");
        Assert.Equal(($"{SetsFolder}/Compressed/General Tiles", 0L, (int?)null, 0, 640), (general.TileDataKey, general.TileOffset, general.TileCount, general.FirstTile, general.MetatileCount));
        Assert.Equal(("/Rom", FireRedShapedData.PrimaryMetatiles, 2, "GBA Text BG", "GBA 4bpp"), (general.MetatileDataKey, general.MetatileOffset, general.Layers, general.FormatName, general.CodecName));
        Assert.Equal(("/Rom", 0x7000L, (int?)4), (general.AttributeDataKey, general.AttributeOffset, general.AttributeSize));

        var pallet = Model<MetatileSetModel>(plan, $"{SetsFolder}/Pallet Town");
        Assert.Equal(("/Rom", FireRedShapedData.SecondaryTiles, (int?)136, 640, 4), (pallet.TileDataKey, pallet.TileOffset, pallet.TileCount, pallet.FirstTile, pallet.MetatileCount));
        Assert.DoesNotContain(plan.Resources, x => x.Type == "compresseddata" && x.Offset != FireRedShapedData.Primary);
    }

    [Fact]
    public void ContextMode_PicksMostCommonSection()
    {
        var plan = PlanSynthetic();

        var viridian = Assert.Single(plan.Resources, x => x.Type == "metatileset" && x.Offset == FireRedShapedData.SecondaryB);
        Assert.Equal($"{SetsFolder}/Viridian City", viridian.Key);
        Assert.Equal("/Generated/Maps/Viridian City", Assert.Single(plan.Resources, x => x.Type == "metatilemap" && x.Offset == FireRedShapedData.LayoutB).ParentKey);
    }

    [Fact]
    public void UndercountedSet_RaisedAndReported()
    {
        var plan = PlanSynthetic();

        Assert.Equal(4, Model<MetatileSetModel>(plan, $"{SetsFolder}/Viridian City").MetatileCount);
        Assert.Equal("4 metatiles", plan.Find($"{SetsFolder}/Viridian City")!.Summary);
        Assert.Contains($"Metatile set '{SetsFolder}/Viridian City' declared 1 metatiles, maps use up to 4, raised to 4", plan.Report.Warnings);
        Assert.Equal(4, Model<MetatileSetModel>(plan, $"{SetsFolder}/Pallet Town").MetatileCount);
        Assert.Single(plan.Report.Warnings);
    }

    [Fact]
    public void OutputReferencingSkippedResource_Skipped()
    {
        var plan = PlanSynthetic(xml => xml.Replace("""type="compresseddata" when="isCompressed" """, """type="compresseddata" when="0" """));

        Assert.DoesNotContain(plan.Resources, x => x.Type == "compresseddata");
        Assert.Null(plan.Find($"{SetsFolder}/General"));
        Assert.DoesNotContain(plan.Resources, x => x.Type == "metatilemap");
        Assert.Contains(plan.Report.Skipped, x => x.ToString() ==
            $"Skipped Tileset at 0x200: output 'tilesetSet': tiles output 'tilesetTiles' generated no resource for Tileset at 0x200");
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Layout" && x.Offset == FireRedShapedData.LayoutA && x.Reason.StartsWith("output 'layoutMap': set output 'tilesetSet'"));
        Assert.Equal(13, plan.Resources.Count(x => x.Type == "palette"));
    }

    [Fact]
    public void EvaluationError_SkipsOutputAndReports()
    {
        var plan = PlanSynthetic(xml => xml
            .Replace("""offset="blocks" width="width" """, """offset="blocks" width="width / (width - 2)" """)
            .Replace("</tabledefinition>", """<output id="orphan" record="MapHeader" type="compresseddata" context="Layout" offset="context.width" compression="GBA LZ77" /></tabledefinition>"""));

        Assert.NotNull(plan.Find("/Generated/Maps/Pallet Town/Pallet Town"));
        Assert.DoesNotContain(plan.Resources, x => x.Type == "metatilemap" && x.Offset == FireRedShapedData.LayoutB);
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Layout" && x.Offset == FireRedShapedData.LayoutB &&
            x.Reason.Contains("output 'layoutMap'") && x.Reason.Contains("division by zero in 'width / (width - 2)'"));
        Assert.Equal(4, plan.Report.Skipped.Count(x => x.Record == "MapHeader" && x.Reason.Contains("has no Layout ancestor")));
        Assert.DoesNotContain(plan.Resources, x => x.OutputId == "orphan");
    }

    [Fact]
    public void OutputVariant_OnlyMatchingRecords()
    {
        var plan = PlanMini("""<output id="boss" record="Item" type="compresseddata" variant="boss" offset="address + layout" compression="GBA LZ77" />""",
            MiniData([0x81, 0x23, 0, 0], [1, 2, 0, 0], [0x80, 0x05, 0, 0]));

        Assert.Equal([0x10L + 3, 0x18L + 5], plan.Resources.Select(x => ((CompressedDataModel)x.Model).FileOffset));
    }

    [Fact]
    public void NameTemplates_RenderSegments()
    {
        var plan = PlanMini("""
            	<output id="a" record="Item" type="compresseddata" offset="address" compression="GBA LZ77">
            		<name>{{{names[kind]}}} {address:X6} {value:D3}</name>
            		<folder>Group {kind}</folder>
            	</output>
            	<output id="b" record="Item" type="compresseddata" offset="address" compression="GBA LZ77">
            		<name when="kind == 0">{name:a}</name>
            		<name>Copy {n}</name>
            		<folder>Copies/{kind}</folder>
            	</output>
            	<output id="c" record="Item" type="compresseddata" offset="address" compression="GBA LZ77" />
            """);

        Assert.Equal([
            "/Gen/Group 0/{AB} 000010 001", "/Gen/Group 1/{CD} 000014 002", "/Gen/Group 1/{CD} 000018 003",
            "/Gen/Copies/0/{AB} 000010 001", "/Gen/Copies/1/Copy 1", "/Gen/Copies/1/Copy 2",
            "/Gen/c 000010", "/Gen/c 000014", "/Gen/c 000018"], plan.Resources.Select(x => x.Key));
    }

    [Fact]
    public void NamesSanitizedAndUnique()
    {
        var plan = PlanMini("""
            	<output id="clean" record="Item" type="compresseddata" offset="address" compression="GBA LZ77">
            		<name>  ..A&lt;b&gt;:c?d.  </name>
            	</output>
            	<output id="empty" record="Item" type="compresseddata" offset="address" compression="GBA LZ77">
            		<name when="kind == 0">...</name>
            		<name when="value == 2">CON</name>
            		<name>A/B</name>
            		<folder>Sub./CON/ok</folder>
            	</output>
            	<output id="upper" record="Item" type="compresseddata" when="kind == 0" offset="address" compression="GBA LZ77">
            		<name>ABCD</name>
            	</output>
            """);

        Assert.Equal([
            "/Gen/Abcd", "/Gen/Abcd 2", "/Gen/Abcd 3",
            "/Gen/Sub/ok/empty 000010", "/Gen/Sub/ok/empty 000014", "/Gen/Sub/ok/AB",
            "/Gen/ABCD 4"], plan.Resources.Select(x => x.Key));
        Assert.Contains(plan.Report.Warnings, x => x.Contains("folder segment 'CON'"));
    }

    [Fact]
    public void Duplicates_NumberAndAddress()
    {
        var plan = PlanMini("""
            	<output id="number" record="Item" type="compresseddata" offset="address" compression="GBA LZ77">
            		<name>Same</name>
            	</output>
            	<output id="address" record="Item" type="compresseddata" offset="address" compression="GBA LZ77" duplicates="address">
            		<name>Same</name>
            		<folder>A</folder>
            	</output>
            """);

        Assert.Equal(["/Gen/Same", "/Gen/Same 2", "/Gen/Same 3", "/Gen/A/Same", "/Gen/A/Same 000014", "/Gen/A/Same 000018"],
            plan.Resources.Select(x => x.Key));
    }

    [Fact]
    public void TilemapOutput_BuildsModel()
    {
        var plan = PlanMini("""
            	<output id="screen" record="Item" type="tilemap" when="kind == 0" offset="0x20" width="value + 1" height="2" format="GBA Text BG"
            		codec="GBA 4bpp" firsttile="16" emptytile="3" layout="snes32">
            		<name>Screen</name>
            		<tiles offset="0x60" count="8" />
            		<paletteslots output="screenPalette" />
            	</output>
            	<output id="screenPalette" record="Item" type="palette" when="kind == 0" repeat="2" slot="i + 1" offset="0x40 + 2 * slot" entries="1" color="Bgr15">
            		<name>Pal {slot}</name>
            	</output>
            """);

        var tilemap = Model<TilemapModel>(plan, "/Gen/Screen");
        Assert.Equal(("/Rom", 0x20L, 2, 2, ImageMagitek.Tilemaps.MapScreenLayout.Snes32, "GBA Text BG"), (tilemap.MapDataKey, tilemap.MapOffset, tilemap.Width, tilemap.Height, tilemap.Layout, tilemap.FormatName));
        Assert.Equal(("/Rom", 0x60L, (int?)8, 16, 3, "GBA 4bpp"), (tilemap.TileDataKey, tilemap.TileOffset, tilemap.TileCount, tilemap.FirstTile, tilemap.EmptyTile, tilemap.CodecName));
        Assert.Equal([(1, "/Gen/Pal 1"), (2, "/Gen/Pal 2")], tilemap.PaletteSlots.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)));
        Assert.Equal("2x2 tiles", plan.Find("/Gen/Screen")!.Summary);
        Assert.Equal(["palette", "palette", "tilemap"], plan.Resources.Select(x => x.Type));
    }

    private GenerationPlan PlanFf4(bool header = false, Action<byte[]>? edit = null, Func<string, string>? editXml = null)
    {
        if (editXml is not null)
            _context.Write("Ff4Synthetic.table.xml", editXml(File.ReadAllText(Path.Combine(TableTestContext.FixturesDirectory, "Ff4Synthetic.table.xml"))));

        var result = _context.Load("Ff4Synthetic.table.xml");
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);

        var data = Ff4ShapedData.Create(header);
        edit?.Invoke(data);
        return new TableGenerator(result.AsSuccess.Result, _context.Files.Open("Ff4", data), "/Rom", "/Monsters", MapFormatTests.LoadShippedFormats()).Plan(ProjectFile);
    }

    private static ArrangerElementModel?[,] Grid(GenerationPlan plan, string key) => Model<ScatteredArrangerModel>(plan, key).ElementGrid;

    [Fact]
    public void RectArranger_SequentialElements()
    {
        var plan = PlanFf4();

        var imp = Model<ScatteredArrangerModel>(plan, "/Monsters/00 Imp");
        Assert.Equal((new System.Drawing.Size(2, 2), new System.Drawing.Size(8, 8), ElementLayout.Tiled, PixelColorType.Indexed),
            (imp.ArrangerElementSize, imp.ElementPixelSize, imp.Layout, imp.ColorType));
        Assert.Equal("2x2 elements", plan.Find("/Monsters/00 Imp")!.Summary);

        var bomb = Model<ScatteredArrangerModel>(plan, "/Monsters/01 Bomb");
        for (var n = 0; n < 6; n++)
        {
            var element = bomb.ElementGrid[n % 3, n / 3];
            Assert.Equal((Ff4ShapedData.MonsterTiles + 24 * n, n % 3, n / 3), (element.FileAddress.ByteOffset, element.PositionX, element.PositionY));
            Assert.Equal(("/Rom", "SNES 3bpp", "/Monsters/Monster Palettes/MonsterPal1"), (element.DataFileKey, element.CodecName, element.PaletteKey));
        }

        Assert.NotNull(plan.Find("/Monsters/05 Mt.Dr.Ab"));
    }

    [Fact]
    public void IndexmapArranger_SkipCodes()
    {
        var plan = PlanFf4();

        var golbez = Grid(plan, "/Monsters/02 Golbez");
        int?[] expected = [0, null, 1, null, null, 3];
        for (var n = 0; n < expected.Length; n++)
        {
            var element = golbez[n % 3, n / 3];
            if (expected[n] is { } tile)
                Assert.Equal(Ff4ShapedData.BossTiles + 24 * tile, element!.FileAddress.ByteOffset);
            else
                Assert.Null(element);
        }

        var zeromus = Grid(plan, "/Monsters/03 Zeromus");
        Assert.Equal(Ff4ShapedData.Boss4bppTiles + 32 * 2, zeromus[0, 0]!.FileAddress.ByteOffset);
        Assert.Single(zeromus.Cast<ArrangerElementModel?>(), x => x is not null);
    }

    [Fact]
    public void IndexmapStreamPastEnd_SkipsAndReports()
    {
        var plan = PlanFf4(edit: data => (data[Ff4ShapedData.BossMaps], data[Ff4ShapedData.BossMaps + 1]) = (0xFE, 0xFF));

        Assert.Null(plan.Find("/Monsters/02 Golbez"));
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Monster" && x.Offset == Ff4ShapedData.Monsters + 8 &&
            x.Reason.Contains("output 'boss'") && x.Reason.Contains("cell stream at 0xFFFE ends after 2 of 6 cells"));
        Assert.NotNull(plan.Find("/Monsters/03 Zeromus"));
    }

    [Fact]
    public void ArrangerOverElementLimit_SkipsAndReports()
    {
        var plan = PlanFf4(editXml: xml => xml.Replace("width=\"sizes[size].w\" height=\"sizes[size].h\"", "width=\"300\" height=\"300\""));

        Assert.Null(plan.Find("/Monsters/00 Imp"));
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Monster" && x.Offset == Ff4ShapedData.Monsters &&
            x.Reason.Contains("output 'monster'") && x.Reason.Contains("arranger of 300x300 elements has more than 65536"));
        Assert.NotNull(plan.Find("/Monsters/02 Golbez"));
    }

    [Fact]
    public void ArrangerTilePastEnd_SkipsAndReports()
    {
        var plan = PlanFf4(edit: data => (data[Ff4ShapedData.Monsters + 22], data[Ff4ShapedData.Monsters + 23]) = (0xFD, 0x1F));

        Assert.Null(plan.Find("/Monsters/05 Mt.Dr.Ab"));
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Monster" && x.Offset == Ff4ShapedData.Monsters + 20 &&
            x.Reason.Contains("output 'monster'") && x.Reason.Contains("tile 1 at 0x10000 is past the end of the data"));
        Assert.NotNull(plan.Find("/Monsters/00 Imp"));
    }

    [Fact]
    public void ArrangerPaletteNotGenerated_SkipsAndReports()
    {
        var plan = PlanFf4(editXml: xml => xml.Replace("""type="palette" when="index != 4 """, """type="palette" when="index != 4 &amp; index != 5 """));

        Assert.Null(plan.Find("/Monsters/05 Mt.Dr.Ab"));
        Assert.Null(plan.Find("/Monsters/Monster Palettes/MonsterPal3"));
        Assert.Contains(plan.Report.Skipped, x => x.Record == "Monster" && x.Offset == Ff4ShapedData.Monsters + 20 &&
            x.Reason.Contains("output 'monster': palette output 'monsterPalette' generated no resource"));
        Assert.NotNull(plan.Find("/Monsters/00 Imp"));
    }

    [Fact]
    public void BossVariant_SelectsCodecByPointerBit()
    {
        var plan = PlanFf4();

        var golbez = Grid(plan, "/Monsters/02 Golbez")[0, 0]!;
        var zeromus = Grid(plan, "/Monsters/03 Zeromus")[0, 0]!;
        Assert.Equal(("SNES 3bpp", "/Monsters/Monster Palettes/MonsterPal2"), (golbez.CodecName, golbez.PaletteKey));
        Assert.Equal(("SNES 4bpp", "/Monsters/Monster Palettes/MonsterPal2x16"), (zeromus.CodecName, zeromus.PaletteKey));
        Assert.Equal(("boss", "boss4bpp"), (plan.Find("/Monsters/02 Golbez")!.OutputId, plan.Find("/Monsters/03 Zeromus")!.OutputId));
        Assert.Equal((8, 16), (plan.Find("/Monsters/Monster Palettes/MonsterPal2")!.Entries, plan.Find("/Monsters/Monster Palettes/MonsterPal2x16")!.Entries));
    }

    [Fact]
    public void MonsterPalettes_DedupedAcrossOutputs()
    {
        var plan = PlanFf4();

        Assert.Equal(["MonsterPal1", "MonsterPal3", "MonsterPal2", "MonsterPal2x16"], plan.Resources.Where(x => x.Type == "palette").Select(x => x.Name));
        Assert.All(plan.Resources.Where(x => x.Type == "palette"), x => Assert.Equal("/Monsters/Monster Palettes", x.ParentKey));
        Assert.Equal(Grid(plan, "/Monsters/00 Imp")[0, 0]!.PaletteKey, Grid(plan, "/Monsters/01 Bomb")[0, 0]!.PaletteKey);

        var eight = Assert.IsType<FileColorSourceModel>(Assert.Single(Model<PaletteModel>(plan, "/Monsters/Monster Palettes/MonsterPal2").ColorSources));
        var sixteen = Assert.IsType<FileColorSourceModel>(Assert.Single(Model<PaletteModel>(plan, "/Monsters/Monster Palettes/MonsterPal2x16").ColorSources));
        Assert.Equal((Ff4ShapedData.Palettes + 32, 8), (eight.FileAddress, eight.Entries));
        Assert.Equal((Ff4ShapedData.Palettes + 32, 16), (sixteen.FileAddress, sixteen.Entries));
    }

    [Fact]
    public void BankImpliedPointer_MapsThroughLoRom()
    {
        var plain = PlanFf4();
        var headered = PlanFf4(header: true);

        Assert.Equal(plain.Resources.Select(x => x.Key), headered.Resources.Select(x => x.Key));
        Assert.Equal(plain.Resources.Select(x => x.Offset + SnesLoRomAddressMapper.CopierHeaderSize), headered.Resources.Select(x => x.Offset));

        var golbez = Grid(headered, "/Monsters/02 Golbez");
        Assert.Equal(Ff4ShapedData.BossTiles + SnesLoRomAddressMapper.CopierHeaderSize + 24 * 3, golbez[2, 1]!.FileAddress.ByteOffset);
        Assert.Null(golbez[1, 0]);
        Assert.Empty(headered.Report.Skipped);
    }

    [Fact]
    public void SkipPredicate_LeavesNoResources()
    {
        var plan = PlanFf4();

        Assert.DoesNotContain(plan.Resources, x => x.Offset == Ff4ShapedData.Monsters + 16 || x.Name.Contains("Dummy") || x.Name == "MonsterPal5");
        Assert.Equal(5, plan.Resources.Count(x => x.Type == "arranger"));
        Assert.Empty(plan.Report.Skipped);
        Assert.Empty(plan.Report.Warnings);
    }

    [Fact]
    public void BuildOrder_PalettesBeforeArrangers()
    {
        var plan = PlanFf4();

        Assert.Equal(["palette", "arranger"], plan.Resources.Select(x => x.Type).Distinct());
        Assert.Equal([("palette", 4), ("arranger", 5)], plan.Report.Counts);
    }
}
