using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Scenes;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Metatiles;

/// <summary>
/// A metatile set listed by a metatile map, with the metatile number its first metatile has
/// </summary>
public sealed record MetatileMapSet(MetatileSet Set, int Base);

/// <summary>
/// A block map drawn through an ordered list of metatile sets. It exposes one derived arranger per metatile layer and composites them;
/// it is not an arranger itself.
/// </summary>
public sealed class MetatileMap : IProjectResource, IDisposable
{
    private readonly object _gridLock = new();
    private readonly IGraphicsCodec[][] _setCodecs;
    private readonly bool[] _layerVisible;
    private readonly DataSource[] _watchedSources;
    private ArrangerElement?[][,] _layerGrids;
    private uint?[,] _blockValues;
    private int?[,] _blockNumbers;
    private int _version = 1;
    private int _builtVersion;
    private string? _mapError;

    public string Name { get; set; }
    public bool CanContainChildResources => false;
    public bool ShouldBeSerialized { get; set; } = true;

    public DataSource BlockSource { get; }
    public long BlockOffset { get; }
    public MapFormat BlockFormat { get; }
    public MapScreenLayout Layout { get; }

    /// <summary>
    /// The map's size in blocks
    /// </summary>
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// The block number an erased block gets
    /// </summary>
    public int EmptyBlock { get; }

    public IReadOnlyList<MetatileMapSet> Sets { get; }

    /// <summary>
    /// The palette for each value of an entry's palette field, from whichever set assigns it; an empty slot uses slot 0
    /// </summary>
    public IReadOnlyList<Palette?> PaletteSlots { get; }

    public int LayerCount { get; }

    /// <summary>
    /// The size of one block in tiles
    /// </summary>
    public Size Shape { get; }

    public Size TileSize { get; }
    public Size BlockPixelSize => new(Shape.Width * TileSize.Width, Shape.Height * TileSize.Height);
    public Size PixelSize => new(Width * BlockPixelSize.Width, Height * BlockPixelSize.Height);

    /// <summary>
    /// One derived arranger per metatile layer, bottom layer first
    /// </summary>
    public IReadOnlyList<MetatileLayerArranger> Layers { get; }

    /// <summary>
    /// The block map source and every source a listed set reads, each once
    /// </summary>
    public IReadOnlyList<DataSource> DataSources => _watchedSources;

    /// <summary>
    /// Why some blocks could not be read from the block map data, or null when every block was read
    /// </summary>
    public string? MapError
    {
        get
        {
            EnsureBuilt();
            return _mapError;
        }
    }

    /// <summary>
    /// Slot 0's color 0, made opaque, which shows where every layer is transparent
    /// </summary>
    public ColorRgba32 Backdrop => ((IIndexedCodec)_setCodecs[0][0]).Palette[0] with { A = 255 };

    /// <exception cref="ArgumentException">No set is listed, the size is not positive, the width does not suit the layout, the empty block
    /// does not fit the block format, or the sets' metatile ranges, tile ranges or palette slots overlap, or their layers or shapes differ</exception>
    public MetatileMap(string name, DataSource blockSource, long blockOffset, MapFormat blockFormat, MapScreenLayout layout,
        int width, int height, IReadOnlyList<MetatileMapSet> sets, int emptyBlock = 0)
    {
        if (FindError(blockFormat, layout, width, height, sets, emptyBlock) is { } error)
            throw new ArgumentException($"Metatile map '{name}' {error}");

        Name = name;
        BlockSource = blockSource;
        BlockOffset = blockOffset;
        BlockFormat = blockFormat;
        Layout = layout;
        Width = width;
        Height = height;
        EmptyBlock = emptyBlock;
        Sets = sets.ToList();

        var first = Sets[0].Set;
        LayerCount = first.Layers;
        Shape = first.Shape;
        TileSize = first.TileSet.TileSize;

        var slots = new Palette?[Sets.Max(x => x.Set.PaletteSlots.Count)];
        foreach (var set in Sets.Select(x => x.Set))
        {
            for (int i = 0; i < set.PaletteSlots.Count; i++)
                slots[i] ??= set.PaletteSlots[i];
        }
        PaletteSlots = slots;

        _setCodecs = Sets.Select(x => x.Set.TileSet.CreateSlotCodecs(slots)).ToArray();

        _layerVisible = Enumerable.Repeat(true, LayerCount).ToArray();
        _layerGrids = new ArrangerElement?[LayerCount][,];
        _blockValues = new uint?[height, width];
        _blockNumbers = new int?[height, width];
        Layers = Enumerable.Range(0, LayerCount).Select(i => new MetatileLayerArranger(this, i)).ToList();

        _watchedSources = Sets.SelectMany(x => new[] { x.Set.MetatileSource, x.Set.TileSet.Source, x.Set.AttributeSource })
            .Prepend(blockSource)
            .OfType<DataSource>()
            .Distinct()
            .ToArray();

        foreach (var source in _watchedSources)
            source.DataWritten += OnSourceWritten;
    }

    private static string? FindError(MapFormat blockFormat, MapScreenLayout layout, int width, int height, IReadOnlyList<MetatileMapSet> sets, int emptyBlock)
    {
        if (sets.Count == 0)
            return "must list at least one metatile set";

        if (width <= 0 || height <= 0)
            return $"must have a positive width and height, not {width}x{height}";

        if (layout.FindWidthError(width) is { } widthError)
            return $"has an unsuitable width: {widthError}";

        if (emptyBlock < 0 || emptyBlock > blockFormat.MaxTileIndex)
            return $"has empty block {emptyBlock}, which does not fit the {blockFormat.TileIndex.Bits}-bit index field of block format '{blockFormat.Name}'";

        if (sets.FirstOrDefault(x => x.Base < 0) is { } negative)
            return $"lists metatile set '{negative.Set.Name}' with negative base {negative.Base}";

        var first = sets[0].Set;
        if (sets.Skip(1).FirstOrDefault(x => x.Set.Layers != first.Layers) is { } layerMismatch)
            return $"lists metatile sets '{first.Name}' with {first.Layers} layers and '{layerMismatch.Set.Name}' with {layerMismatch.Set.Layers}; they must match";

        if (sets.Skip(1).FirstOrDefault(x => x.Set.Shape != first.Shape) is { } shapeMismatch)
            return $"lists metatile sets '{first.Name}' of {first.Shape.Width}x{first.Shape.Height} tiles and '{shapeMismatch.Set.Name}' of " +
                $"{shapeMismatch.Set.Shape.Width}x{shapeMismatch.Set.Shape.Height}; their shapes must match";

        if (sets.Skip(1).FirstOrDefault(x => x.Set.TileSet.TileSize != first.TileSet.TileSize) is { } tileMismatch)
            return $"lists metatile sets '{first.Name}' and '{tileMismatch.Set.Name}' whose tiles differ in size";

        for (int i = 0; i < sets.Count; i++)
        {
            for (int j = i + 1; j < sets.Count; j++)
            {
                var (a, b) = (sets[i], sets[j]);

                if (Overlaps(a.Base, a.Set.Count, b.Base, b.Set.Count))
                    return $"lists metatile sets '{a.Set.Name}' ({Range(a.Base, a.Set.Count)}) and '{b.Set.Name}' ({Range(b.Base, b.Set.Count)}), whose metatile numbers overlap";

                var (ta, tb) = (a.Set.TileSet, b.Set.TileSet);
                if (Overlaps(ta.FirstTileIndex, ta.TileCount, tb.FirstTileIndex, tb.TileCount))
                    return $"lists metatile sets '{a.Set.Name}' (tiles {Range(ta.FirstTileIndex, ta.TileCount)}) and '{b.Set.Name}' " +
                        $"(tiles {Range(tb.FirstTileIndex, tb.TileCount)}), whose tile ranges overlap";

                var sharedSlot = Enumerable.Range(0, Math.Min(a.Set.PaletteSlots.Count, b.Set.PaletteSlots.Count))
                    .FirstOrDefault(x => a.Set.PaletteSlots[x] is not null && b.Set.PaletteSlots[x] is not null, -1);
                if (sharedSlot >= 0)
                    return $"lists metatile sets '{a.Set.Name}' and '{b.Set.Name}', which both assign palette slot {sharedSlot}";
            }
        }

        return null;

        static bool Overlaps(long startA, long countA, long startB, long countB) =>
            countA > 0 && countB > 0 && startA < startB + countB && startB < startA + countA;

        static string Range(long start, long count) => $"{start}-{start + count - 1}";
    }

    public bool IsLayerVisible(int layer) => _layerVisible[layer];

    /// <summary>
    /// Shows or hides a layer in <see cref="Composite"/>; not stored in the project
    /// </summary>
    public void SetLayerVisible(int layer, bool isVisible) => _layerVisible[layer] = isVisible;

    private void OnSourceWritten(object? sender, EventArgs e) => Interlocked.Increment(ref _version);

    internal ArrangerElement?[,] GetLayerGrid(int layer)
    {
        EnsureBuilt();
        return _layerGrids[layer];
    }

    private void EnsureBuilt()
    {
        if (Volatile.Read(ref _builtVersion) == Volatile.Read(ref _version))
            return;

        lock (_gridLock)
        {
            // Read before rebuilding so a DataWritten raised during the rebuild schedules another
            var version = Volatile.Read(ref _version);
            if (_builtVersion != version)
            {
                Rebuild();
                Volatile.Write(ref _builtVersion, version);
            }
        }
    }

    private void Rebuild()
    {
        var entryBytes = BlockFormat.EntryBytes;
        var blockCount = BlockCount;
        var (data, readBlocks, sourceError) = MapDataReader.Read(BlockSource, BlockOffset, blockCount, entryBytes);

        _mapError = sourceError ?? (readBlocks < blockCount
            ? $"The block map in '{BlockSource.Name}' holds {readBlocks} of the {blockCount} blocks a {Width}x{Height} map needs"
            : null);

        var blockValues = new uint?[Height, Width];
        var numbers = new int?[Height, Width];

        for (int by = 0; by < Height; by++)
        {
            for (int bx = 0; bx < Width; bx++)
            {
                var offset = BlockDataOffset(bx, by);
                if (offset + entryBytes > data.Length)
                    continue;

                var value = BlockFormat.ReadRawValue(data.AsSpan((int)offset, entryBytes));
                blockValues[by, bx] = value;
                numbers[by, bx] = BlockFormat.TileIndex.Extract(value);
            }
        }

        _layerGrids = BuildLayerGrids(numbers);
        _blockValues = blockValues;
        _blockNumbers = numbers;
    }

    private long BlockCount => (long)Layout.EntryIndex(Width - 1, Height - 1, Width) + 1;

    private long BlockDataOffset(int blockX, int blockY) => (long)Layout.EntryIndex(blockX, blockY, Width) * BlockFormat.EntryBytes;

    /// <summary>
    /// One tile grid per layer for block numbers [y, x] of any size; a null number or one outside every set gives an empty block
    /// </summary>
    private ArrangerElement?[][,] BuildLayerGrids(int?[,] numbers)
    {
        var (blocksHigh, blocksWide) = (numbers.GetLength(0), numbers.GetLength(1));
        var tables = Sets.Select(x => x.Set.ReadTable()).ToArray();
        var tileRanges = Sets.Select(x => (First: x.Set.TileSet.FirstTileIndex, Count: x.Set.TileSet.TileCount)).ToArray();
        var (w, h) = (Shape.Width, Shape.Height);
        var grids = Enumerable.Range(0, LayerCount).Select(_ => new ArrangerElement?[blocksHigh * h, blocksWide * w]).ToArray();

        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                if (numbers[by, bx] is not { } number)
                    continue;

                var setIndex = FindSet(number);
                if (setIndex < 0)
                    continue;

                var set = Sets[setIndex].Set;
                var metatile = number - Sets[setIndex].Base;

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (!set.TryDecodeEntry(tables[setIndex], metatile, layer, x, y, out var entry))
                                continue;

                            var tileSetIndex = Array.FindIndex(tileRanges, r => entry.TileIndex >= r.First && (long)entry.TileIndex - r.First < r.Count);
                            if (tileSetIndex < 0)
                                continue;

                            var codecs = _setCodecs[tileSetIndex];
                            var slot = entry.Palette < codecs.Length ? entry.Palette : 0;
                            grids[layer][by * h + y, bx * w + x] = Sets[tileSetIndex].Set.TileSet
                                .CreateTileElement(entry.TileIndex, codecs[slot], entry.Mirror, bx * w + x, by * h + y);
                        }
                    }
                }
            }
        }

        return grids;
    }

    private int FindSet(int number)
    {
        for (int i = 0; i < Sets.Count; i++)
        {
            if (number >= Sets[i].Base && (long)number - Sets[i].Base < Sets[i].Set.Count)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// The set whose metatile range holds <paramref name="number"/>, or null when none does
    /// </summary>
    public MetatileMapSet? FindSetFor(int number) => FindSet(number) is var index and >= 0 ? Sets[index] : null;

    /// <summary>
    /// The block's value with the index field's bits removed and the higher bits shifted down, or null when it lies past the block map data
    /// </summary>
    public int? BlockBitsValue(int blockX, int blockY)
    {
        EnsureBuilt();
        return _blockValues[blockY, blockX] is { } value ? (int)BlockFormat.TileIndex.Remove(value) : null;
    }

    /// <summary>
    /// The visible layers drawn bottom layer first over <see cref="Backdrop"/>, reading the current data
    /// </summary>
    public LayerImage Composite() => CompositeLayers(Layers, PixelSize);

    /// <summary>
    /// The composite of <paramref name="grid"/>'s block numbers drawn through the current metatile tables and tiles
    /// </summary>
    public LayerImage Composite(BlockGrid grid) => RenderBlocks(grid.Numbers);

    /// <summary>
    /// Composites block numbers [y, x] of any size the way <see cref="Composite()"/> draws the map
    /// </summary>
    internal LayerImage RenderBlocks(int?[,] numbers)
    {
        var size = new Size(numbers.GetLength(1) * BlockPixelSize.Width, numbers.GetLength(0) * BlockPixelSize.Height);

        var arrangers = BuildLayerGrids(numbers).Select((grid, i) =>
        {
            var arranger = new ScatteredArranger($"{Name} Layer {i + 1}", PixelColorType.Indexed, ElementLayout.Tiled,
                grid.GetLength(1), grid.GetLength(0), TileSize.Width, TileSize.Height);

            for (int y = 0; y < grid.GetLength(0); y++)
            {
                for (int x = 0; x < grid.GetLength(1); x++)
                {
                    if (grid[y, x] is { } element)
                        arranger.SetElement(element, x, y);
                }
            }

            return arranger;
        }).ToList();

        return CompositeLayers(arrangers, size);
    }

    // Layers above the bottom treat index 0 as transparent whatever the palette's flag says, as GBA and SNES backgrounds do
    private LayerImage CompositeLayers(IReadOnlyList<Arranger> layers, Size size) =>
        LayerCompositor.Composite(size,
            layers.Select((layer, i) => new CompositorLayer(LayerImage.FromArranger(layer, forceZeroTransparent: i > 0), Point.Empty, IsLayerVisible(i))).ToList(),
            Backdrop);

    /// <summary>
    /// A working copy of the block numbers in the current data
    /// </summary>
    public BlockGrid CreateBlockGrid()
    {
        EnsureBuilt();
        return new BlockGrid((int?[,])_blockNumbers.Clone());
    }

    /// <summary>
    /// Why the block map cannot be written, completing "is read-only because it ...", or null when it can
    /// </summary>
    public string? BlockMapReadOnlyReason => ArrangerExtensions.DescribeReadOnlySource(BlockSource);

    /// <summary>
    /// Writes each number of <paramref name="grid"/> that differs from the block map into that block's index field, keeping its block bits,
    /// then flushes and raises <see cref="DataSource.DataWritten"/> once if any block changed
    /// </summary>
    /// <returns>A failure, with nothing written, when a block changed and the block map is read-only, or a changed number fits no set
    /// (other than the empty block) or the index field</returns>
    public MagitekResult WriteBlocks(BlockGrid grid)
    {
        if (grid.Width != Width || grid.Height != Height)
            return new MagitekResult.Failed($"Metatile map '{Name}' is {Width}x{Height} blocks, not {grid.Width}x{grid.Height}");

        var entryBytes = BlockFormat.EntryBytes;
        var data = MapDataReader.Read(BlockSource, BlockOffset, BlockCount, entryBytes).Data;
        var writes = new List<(int Offset, int Number)>();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var offset = BlockDataOffset(x, y);
                if (grid[x, y] is not { } number || offset + entryBytes > data.Length)
                    continue;

                if (BlockFormat.TileIndex.Extract(BlockFormat.ReadRawValue(data.AsSpan((int)offset, entryBytes))) == number)
                    continue;

                if (number < 0 || number > BlockFormat.MaxTileIndex)
                    return new MagitekResult.Failed($"Block ({x}, {y}) of metatile map '{Name}' holds {number}, which does not fit the " +
                        $"{BlockFormat.TileIndex.Bits}-bit index field of block format '{BlockFormat.Name}'");

                if (number != EmptyBlock && FindSet(number) < 0)
                    return new MagitekResult.Failed($"Block ({x}, {y}) of metatile map '{Name}' holds {number}, which is in no metatile set of the map");

                writes.Add(((int)offset, number));
            }
        }

        if (writes.Count == 0)
            return MagitekResult.SuccessResult;

        if (BlockMapReadOnlyReason is { } readOnlyReason)
            return new MagitekResult.Failed($"The block map of metatile map '{Name}' is read-only because it {readOnlyReason}");

        foreach (var (offset, number) in writes)
        {
            var buffer = data.AsSpan(offset, entryBytes);
            BlockFormat.WriteRawValue(BlockFormat.TileIndex.Insert(BlockFormat.ReadRawValue(buffer), number), buffer);
            BlockSource.Write(new BitAddress(BlockOffset + offset, 0), buffer);
        }

        BlockSource.Flush();
        BlockSource.NotifyDataWritten();
        return MagitekResult.SuccessResult;
    }

    /// <summary>
    /// The first missing file the map reads, directly, through compressed data or through a set, or null when none is missing
    /// </summary>
    public FileDataSource? FindMissingDataSource() =>
        MetatileSet.FindMissing(Sets.SelectMany(x => x.Set.ReadSources).Prepend(BlockSource));

    /// <summary>
    /// The block map source and every listed metatile set
    /// </summary>
    public IEnumerable<IProjectResource> LinkedResources =>
        Sets.Select(x => (IProjectResource)x.Set).Prepend(BlockSource).Distinct().ToList();

    public bool UnlinkResource(IProjectResource resource) => false;

    public void Dispose()
    {
        foreach (var source in _watchedSources)
            source.DataWritten -= OnSourceWritten;
    }
}
