using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class ResourceGraphTests
{
    private static readonly MapFormat _gba = MapFormatTests.LoadShippedFormats().Find("GBA Text BG")!;

    private readonly ProjectTree _tree;
    private readonly CompressorFactory _compressors = new();
    private readonly MemoryDataSource _rom = new("Rom", 0x400);
    private readonly MemoryDataSource _other = new("Other", 0x400);

    public ResourceGraphTests()
    {
        var project = new ImageProject("Project");
        _tree = new ProjectTree(new ProjectNode("", project.Name, project));
        Add(new DataFileNode(_rom.Name, _rom));
        Add(new DataFileNode(_other.Name, _other));
    }

    private void Add(ResourceNode node) => _tree.Root.AttachChildNode(node);

    private CompressedDataSource AddCompressed(string name, DataSource parent)
    {
        var compressed = new CompressedDataSource(name, parent, 0, "GBA LZ77", _compressors);
        Add(new CompressedDataNode(name, compressed));
        return compressed;
    }

    private Palette AddPalette(string name, DataSource source)
    {
        var palette = new Palette(name, new ColorFactory(), ColorModel.Rgba32, [new ProjectNativeColorSource(new ColorRgba32(0, 0, 0, 255))],
            true, PaletteStorageSource.ProjectXml, source);
        Add(new PaletteNode(name, palette));
        return palette;
    }

    private ScatteredArranger AddArranger(string name, DataSource source, Palette palette)
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) =>
        {
            var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
            codec.Palette = palette;
            return codec;
        }, source);
        arranger.Name = name;
        Add(new ArrangerNode(name, arranger));
        return arranger;
    }

    private TilemapArranger AddTilemap(Palette slotPalette, ScatteredArranger? tilePalette = null)
    {
        var tilemap = new TilemapArranger("Screen", _other, 0, _gba, MapScreenLayout.Linear, 1, 1, _other, 0x100, "GBA 4bpp", 0, [slotPalette],
            CodecFixture.Shared.CodecFactory) { TilePaletteArranger = tilePalette };
        Add(new TilemapNode(tilemap.Name, tilemap));
        return tilemap;
    }

    private MetatileSet AddSet(Palette slotPalette)
    {
        var set = new MetatileSet("Set", _other, 0, 4, new Size(2, 2), 2, _gba, MetatileOrder.Planar, _other, 0x100, "GBA 4bpp", 0, 8,
            [slotPalette], CodecFixture.Shared.CodecFactory);
        Add(new MetatileSetNode(set.Name, set));
        return set;
    }

    [Fact]
    public void CompressedDataAndPalette_CascadeToTheirSources()
    {
        var compressed = AddCompressed("Packed", _rom);
        var palette = AddPalette("Pal", compressed);

        Assert.Equal([new ResourceEdge(_rom, ResourceEdgeKind.Cascade)], compressed.Dependencies);
        Assert.Equal([new ResourceEdge(compressed, ResourceEdgeKind.Cascade)], palette.Dependencies);
    }

    [Fact]
    public void ScatteredArranger_DropsElementsOnSourcesAndFallsBackPalettes()
    {
        var palette = AddPalette("Pal", _rom);
        var arranger = AddArranger("Sprite", _other, palette);

        Assert.True(arranger.Dependencies.ToHashSet().SetEquals([
            new ResourceEdge(_other, ResourceEdgeKind.DropElements),
            new ResourceEdge(palette, ResourceEdgeKind.FallbackPalette)]));
    }

    [Fact]
    public void RemovingTilePalette_ClearsTilemapTilePalette()
    {
        var palette = AddPalette("Pal", _rom);
        var picker = AddArranger("Picker", _rom, palette);
        var tilemap = AddTilemap(palette, picker);

        var closure = new ResourceGraph(_tree).RemovalClosure([picker]);

        Assert.Contains(new ResourceEdge(picker, ResourceEdgeKind.ClearTilePalette), tilemap.Dependencies);
        Assert.Equal(ResourceEdgeKind.ClearTilePalette, closure.Affected[tilemap]);
        Assert.DoesNotContain(tilemap, closure.Removed);
    }

    [Fact]
    public void RemovingSlotPalette_FallsBackInsteadOfRemoving()
    {
        var palette = AddPalette("Pal", _rom);
        var tilemap = AddTilemap(palette);
        var set = AddSet(palette);

        var closure = new ResourceGraph(_tree).RemovalClosure([palette]);

        Assert.Contains(new ResourceEdge(palette, ResourceEdgeKind.FallbackPalette), set.Dependencies);
        Assert.Equal(ResourceEdgeKind.FallbackPalette, closure.Affected[tilemap]);
        Assert.Equal(ResourceEdgeKind.FallbackPalette, closure.Affected[set]);
        Assert.Equal([palette], closure.Removed);
    }

    [Fact]
    public void RemovalClosure_NestedCompressedData_RemovesEverythingOverIt()
    {
        var outer = AddCompressed("Outer", _rom);
        var inner = AddCompressed("Inner", outer);
        var palette = AddPalette("Pal", inner);
        var kept = AddPalette("Kept", _other);
        var picker = AddArranger("Picker", inner, kept);
        var tilemap = AddTilemap(kept, picker);

        var closure = new ResourceGraph(_tree).RemovalClosure([outer]);

        Assert.True(closure.Removed.ToHashSet().SetEquals([outer, inner, palette, picker]));
        Assert.Equal(ResourceEdgeKind.ClearTilePalette, closure.Affected[tilemap]);
        Assert.False(closure.Contains(_rom));
        Assert.False(closure.Contains(kept));
    }

    [Fact]
    public void RemovalClosure_ArrangerKeepingASource_IsAffectedNotRemoved()
    {
        var palette = AddPalette("Pal", _other);
        var arranger = AddArranger("Sprite", _other, palette);
        arranger.SetElement(arranger.GetElement(1, 0)!.Value.WithFile(_rom, BitAddress.Zero), 1, 0);

        var closure = new ResourceGraph(_tree).RemovalClosure([_other]);

        Assert.DoesNotContain(arranger, closure.Removed);
        Assert.Equal(ResourceEdgeKind.DropElements | ResourceEdgeKind.FallbackPalette, closure.Affected[arranger]);
    }

    [Fact]
    public void RebuildOrder_CompressedChainInReverseTreeOrder_ListsEachAfterItsParent()
    {
        var c1 = new CompressedDataSource("C1", _rom, 0, "GBA LZ77", _compressors);
        var c2 = new CompressedDataSource("C2", c1, 0, "GBA LZ77", _compressors);
        var c3 = new CompressedDataSource("C3", c2, 0, "GBA LZ77", _compressors);
        foreach (var compressed in new[] { c1, c2, c3 })
            Add(new CompressedDataNode(compressed.Name, compressed));

        var treeOrder = _tree.EnumerateDepthFirst().Select(x => x.Item).ToList();
        Assert.True(treeOrder.IndexOf(c3) < treeOrder.IndexOf(c2) && treeOrder.IndexOf(c2) < treeOrder.IndexOf(c1));

        var order = new ResourceGraph(_tree).RebuildOrder([_rom]);

        Assert.Equal<IProjectResource>([c1, c2, c3], order.Select(x => x.Item));
    }

    [Fact]
    public void RebuildOrder_ReplacedPalette_RebuildsSetButNotScatteredArranger()
    {
        var palette = AddPalette("Pal", _rom);
        var arranger = AddArranger("Sprite", _other, palette);
        var set = AddSet(palette);

        var order = new ResourceGraph(_tree).RebuildOrder([palette]).Select(x => x.Item).ToList();

        Assert.Equal<IProjectResource>([set], order);
        Assert.DoesNotContain(arranger, order);
    }

    [Fact]
    public void GlobalPalette_IsATargetButNeverADependent()
    {
        var global = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
        var arranger = AddArranger("Sprite", _other, global);
        var graph = new ResourceGraph(_tree);

        var closure = graph.RemovalClosure([global]);

        Assert.Equal([global], closure.Removed);
        Assert.Equal(ResourceEdgeKind.FallbackPalette, closure.Affected[arranger]);
        Assert.Empty(graph.RebuildOrder([global]));
    }
}
