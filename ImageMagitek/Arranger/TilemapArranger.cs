using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;

namespace ImageMagitek;

/// <summary>
/// A tilemap: a read-only view whose elements are derived from the map's entries and the tile set they index.
/// Edits are made on a working <see cref="TilemapGrid"/> of entries and written back with <see cref="WriteEntries"/>.
/// </summary>
public sealed class TilemapArranger : IProjectResource, IArrangerView, IDisposable, IUnavailableResource
{
    private readonly object _gridLock = new();
    private readonly IGraphicsCodec[] _slotCodecs;
    private readonly ICodecFactory _codecFactory;
    private readonly TileSet _tileSet;
    private ArrangerElement?[,] _elements;
    private TilemapGrid _entries;
    private int _version = 1;
    private int _builtVersion;
    private string? _mapError;
    private int _highestTileIndex;
    private int _outOfRangeCount;

    public string Name { get; set; }
    public bool ShouldBeSerialized { get; set; } = true;
    public bool CanContainChildResources => false;

    public Size ArrangerElementSize { get; }
    public Size ElementPixelSize { get; }

    public Size ArrangerPixelSize =>
        new(ArrangerElementSize.Width * ElementPixelSize.Width, ArrangerElementSize.Height * ElementPixelSize.Height);

    public ElementLayout Layout => ElementLayout.Tiled;
    public PixelColorType ColorType => PixelColorType.Indexed;

    public DataSource MapSource { get; }
    public long MapOffset { get; }
    public MapFormat MapFormat { get; }
    public MapScreenLayout ScreenLayout { get; }

    public DataSource TileSource => _tileSet.Source;
    public long TileOffset => _tileSet.Offset;
    public string CodecName => _tileSet.CodecName;
    public int FirstTileIndex => _tileSet.FirstTileIndex;

    /// <summary>
    /// The tile index written into the entry of a deleted cell
    /// </summary>
    public int EmptyTileIndex { get; }

    /// <summary>
    /// The number of tiles the tile set declares, or null when it runs to the end of the tile source
    /// </summary>
    public int? DeclaredTileCount => _tileSet.DeclaredCount;

    /// <summary>
    /// The palette for each value of the entry's palette field; an empty slot uses slot 0
    /// </summary>
    public IReadOnlyList<Palette?> PaletteSlots { get; }

    /// <summary>
    /// A curated layout of tiles to plot from, or null to generate one from the tile set; not a linked resource, so removing it never removes the tilemap
    /// </summary>
    public ScatteredArranger? TilePaletteArranger { get; set; }

    /// <summary>
    /// The number of tiles from <see cref="FirstTileIndex"/> that lie wholly in the tile source, fit the format's tile field
    /// and are within <see cref="DeclaredTileCount"/>
    /// </summary>
    public int TileCount => (int)Math.Max(0, Math.Min(_tileSet.TileCount, (long)MapFormat.MaxTileIndex - FirstTileIndex + 1));

    /// <summary>
    /// The assigned palette slot that the most read cells use, counting a cell naming an empty slot as slot 0; the lowest such slot on a tie
    /// </summary>
    public int MostUsedPaletteSlot
    {
        get
        {
            EnsureGrid();
            var counts = new int[PaletteSlots.Count];
            for (int y = 0; y < ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                {
                    if (_elements[y, x] is null || _entries[x, y] is not { } entry)
                        continue;

                    var slot = entry.Palette;
                    counts[slot < counts.Length && PaletteSlots[slot] is not null ? slot : 0]++;
                }
            }

            return Array.IndexOf(counts, counts.Max());
        }
    }

    /// <summary>
    /// Why the map entries cannot be written, completing "is read-only because it ...", or null when they can
    /// </summary>
    public string? MapReadOnlyReason => ArrangerExtensions.DescribeReadOnlySource(MapSource);

    /// <summary>
    /// Why some cells could not be read from the map data, or null when every cell's entry was read
    /// </summary>
    public string? MapError
    {
        get
        {
            EnsureGrid();
            return _mapError;
        }
    }

    FileDataSource? IUnavailableResource.FindMissingDataSource() => ArrangerExtensions.FindMissingDataSource(this);

    string? IUnavailableResource.UnavailableReason => MapError;

    /// <summary>
    /// The highest tile index among the entries read, or -1 when none was read
    /// </summary>
    public int HighestTileIndex
    {
        get
        {
            EnsureGrid();
            return _highestTileIndex;
        }
    }

    /// <summary>
    /// The number of entries whose tile index lies outside the tile set, which are shown as empty cells
    /// </summary>
    public int OutOfRangeCount
    {
        get
        {
            EnsureGrid();
            return _outOfRangeCount;
        }
    }

    /// <exception cref="ArgumentException">The codec is unknown or not indexed, the size is not positive, the width does not suit the screen layout,
    /// the empty tile index does not fit the format's tile field, or the declared tile count is not positive</exception>
    public TilemapArranger(string name, DataSource mapSource, long mapOffset, MapFormat mapFormat, MapScreenLayout screenLayout,
        int width, int height, DataSource tileSource, long tileOffset, string codecName, int firstTileIndex,
        IReadOnlyList<Palette?> paletteSlots, ICodecFactory codecFactory, int emptyTileIndex = 0, int? declaredTileCount = null)
    {
        try
        {
            _tileSet = new TileSet(tileSource, tileOffset, codecName, firstTileIndex, declaredTileCount, codecFactory);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Tilemap '{name}' {ex.Message}");
        }

        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Tilemap '{name}' must have a positive width and height, not {width}x{height}");

        if (screenLayout.FindWidthError(width) is { } widthError)
            throw new ArgumentException($"Tilemap '{name}': {widthError}");

        if (emptyTileIndex < 0 || emptyTileIndex > mapFormat.MaxTileIndex)
            throw new ArgumentException($"Tilemap '{name}' has empty tile index {emptyTileIndex}, which does not fit the {mapFormat.TileIndex.Bits}-bit tile field of map format '{mapFormat.Name}'");

        Name = name;
        MapSource = mapSource;
        MapOffset = mapOffset;
        MapFormat = mapFormat;
        ScreenLayout = screenLayout;
        EmptyTileIndex = emptyTileIndex;
        _codecFactory = codecFactory;

        var slots = new Palette?[mapFormat.PaletteSlotCount];
        for (int i = 0; i < Math.Min(slots.Length, paletteSlots.Count); i++)
            slots[i] = paletteSlots[i];
        PaletteSlots = slots;

        _slotCodecs = _tileSet.CreateSlotCodecs(slots);

        ElementPixelSize = _tileSet.TileSize;
        ArrangerElementSize = new Size(width, height);
        _elements = new ArrangerElement?[height, width];
        _entries = new TilemapGrid(new MapEntry?[height, width]);

        MapSource.DataWritten += OnSourceWritten;
        if (!ReferenceEquals(TileSource, MapSource))
            TileSource.DataWritten += OnSourceWritten;
    }

    private void OnSourceWritten(object? sender, EventArgs e) => Interlocked.Increment(ref _version);

    private void EnsureGrid()
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

    private (byte[] Data, string? Error) ReadMapData()
    {
        var width = ArrangerElementSize.Width;
        var height = ArrangerElementSize.Height;
        var entryBytes = MapFormat.EntryBytes;
        var entryCount = (long)ScreenLayout.EntryIndex(width - 1, height - 1, width) + 1;
        var (data, readEntries, sourceError) = MapDataReader.Read(MapSource, MapOffset, entryCount, entryBytes);

        if (sourceError is not null)
            return (data, sourceError);

        var error = readEntries < entryCount
            ? $"The map data in '{MapSource.Name}' holds {readEntries} of the {entryCount} entries a {width}x{height} map needs"
            : null;

        return (data, error);
    }

    private void Rebuild()
    {
        var grid = ReadGrid(out var error);
        var tileCount = TileCount;
        var highest = -1;
        var outOfRange = 0;

        foreach (var entry in grid.Entries)
        {
            if (entry is not { } e)
                continue;

            highest = Math.Max(highest, e.TileIndex);
            if (!IsInTileSet(e.TileIndex, tileCount))
                outOfRange++;
        }

        _elements = CreateElements(grid, _slotCodecs);
        _entries = grid;
        _mapError = error;
        _highestTileIndex = highest;
        _outOfRangeCount = outOfRange;
    }

    private TilemapGrid ReadGrid(out string? error)
    {
        var width = ArrangerElementSize.Width;
        var height = ArrangerElementSize.Height;
        var entries = new MapEntry?[height, width];
        var entryBytes = MapFormat.EntryBytes;
        (var data, error) = ReadMapData();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var offset = (long)ScreenLayout.EntryIndex(x, y, width) * entryBytes;
                if (offset + entryBytes <= data.Length)
                    entries[y, x] = MapFormat.Decode(data.AsSpan((int)offset, entryBytes));
            }
        }

        return new TilemapGrid(entries);
    }

    internal bool IsInTileSet(int tileIndex) => IsInTileSet(tileIndex, TileCount);

    private bool IsInTileSet(int tileIndex, int tileCount) => tileIndex >= FirstTileIndex && tileIndex - FirstTileIndex < tileCount;

    /// <summary>
    /// The element of every cell of <paramref name="grid"/> shown with <paramref name="codecs"/>, one per palette slot. A cell with no entry,
    /// or whose tile is outside the tile set, has none; an entry naming a slot past the codecs uses slot 0.
    /// </summary>
    internal ArrangerElement?[,] CreateElements(TilemapGrid grid, IGraphicsCodec[] codecs)
    {
        var elements = new ArrangerElement?[grid.Height, grid.Width];
        var tileCount = TileCount;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid[x, y] is not { } entry || !IsInTileSet(entry.TileIndex, tileCount))
                    continue;

                var slot = entry.Palette < codecs.Length ? entry.Palette : 0;
                elements[y, x] = _tileSet.CreateTileElement(entry.TileIndex, codecs[slot], entry.Mirror, x, y);
            }
        }

        return elements;
    }

    public ArrangerElement? GetElement(int elemX, int elemY)
    {
        EnsureGrid();
        return _elements[elemY, elemX];
    }

    /// <summary>
    /// A working copy of the entries the map decodes to now
    /// </summary>
    public TilemapGrid CreateGrid()
    {
        EnsureGrid();
        return _entries.Clone();
    }

    /// <summary>
    /// A view of a new working grid with its own slot codecs, for an editor to draw and edit
    /// </summary>
    public TilemapView CreateWorkingView() => new(this, CreateGrid(), CloneSlotCodecs());

    /// <summary>
    /// Bakes the whole tilemap into a <see cref="ScatteredArranger"/> whose elements have their own codec instances
    /// </summary>
    public ScatteredArranger Bake()
    {
        var size = ArrangerElementSize;
        var arranger = new ScatteredArranger(Name, ColorType, Layout, size.Width, size.Height, ElementPixelSize.Width, ElementPixelSize.Height);
        var clonedCodecs = new Dictionary<IGraphicsCodec, IGraphicsCodec>();

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (GetElement(x, y) is not ArrangerElement el)
                    continue;

                if (!clonedCodecs.TryGetValue(el.Codec, out var codec))
                {
                    codec = CloneCodec(el.Codec);
                    clonedCodecs.Add(el.Codec, codec);
                }

                arranger.SetElement(el.WithCodec(codec, x * ElementPixelSize.Width, y * ElementPixelSize.Height), x, y);
            }
        }

        return arranger;
    }

    /// <summary>
    /// A new codec instance showing the palette of <paramref name="slot"/>, shared by no other view
    /// </summary>
    internal IGraphicsCodec CloneSlotCodec(int slot) => CloneCodec(_slotCodecs[slot]);

    /// <summary>
    /// New codec instances for every slot; slots that share a codec here share one clone
    /// </summary>
    internal IGraphicsCodec[] CloneSlotCodecs() => _tileSet.CreateSlotCodecs(PaletteSlots);

    /// <summary>
    /// The element for cell (<paramref name="cellX"/>, <paramref name="cellY"/>) reading tile <paramref name="tileIndex"/> of the tile set
    /// </summary>
    internal ArrangerElement CreateTileElement(int tileIndex, IGraphicsCodec codec, MirrorOperation mirror, int cellX, int cellY) =>
        _tileSet.CreateTileElement(tileIndex, codec, mirror, cellX, cellY);

    private IGraphicsCodec CloneCodec(IGraphicsCodec codec)
    {
        var clone = _codecFactory.CloneCodec(codec);
        ((IIndexedCodec)clone).Palette = ((IIndexedCodec)codec).Palette;
        return clone;
    }

    private Palette EffectiveSlotPalette(int slot) => ((IIndexedCodec)_slotCodecs[slot]).Palette;

    /// <summary>
    /// The palette slot an entry should name to show <paramref name="palette"/>: <paramref name="preferredSlot"/> when it shows that palette,
    /// otherwise the lowest slot assigned that palette, otherwise slot 0 when it shows that palette; null when no slot shows it
    /// </summary>
    public int? FindPaletteSlot(Palette palette, int preferredSlot)
    {
        if (ReferenceEquals(EffectiveSlotPalette(preferredSlot), palette))
            return preferredSlot;

        for (int i = 0; i < PaletteSlots.Count; i++)
        {
            if (ReferenceEquals(PaletteSlots[i], palette))
                return i;
        }

        return ReferenceEquals(EffectiveSlotPalette(0), palette) ? 0 : null;
    }

    /// <summary>
    /// The tile index an entry should hold to show the tile <paramref name="element"/> reads, or why it is not a tile of this tile set
    /// </summary>
    /// <returns>On failure, a reason that completes "element at (x, y) ..."</returns>
    internal MagitekResult<int> FindTileIndex(ArrangerElement element) => _tileSet.FindTileIndex(element, MapFormat);

    /// <summary>
    /// The entry showing <paramref name="pasted"/> in a cell that holds <paramref name="destination"/>, or why it is not a tile of this tile set.
    /// Its slot shows the element's palette, preferring the destination's slot; when no slot does, it is the destination's slot (slot 0 for an empty cell).
    /// </summary>
    /// <returns>On failure, a reason that completes "element at (x, y) ..."</returns>
    internal MagitekResult<MapEntry> ToEntry(ArrangerElement pasted, MapEntry? destination)
    {
        var tileResult = FindTileIndex(pasted);
        if (tileResult.HasFailed)
            return new MagitekResult<MapEntry>.Failed(tileResult.AsError.Reason);

        var destinationSlot = destination?.Palette ?? 0;
        var slot = pasted.Codec is IIndexedCodec { Palette: { } palette } ? FindPaletteSlot(palette, destinationSlot) ?? destinationSlot : destinationSlot;

        return new MagitekResult<MapEntry>.Success(new MapEntry(tileResult.AsSuccess.Result, slot,
            pasted.Mirror.FlipsHorizontally(), pasted.Mirror.FlipsVertically(), destination?.Priority ?? false));
    }

    /// <summary>
    /// Writes the map entries that differ from <paramref name="grid"/>, a working grid of the same size, then flushes and notifies once.
    /// Nothing is written when any cell is refused.
    /// </summary>
    /// <remarks>
    /// An empty cell becomes the empty entry, unless this tilemap's own cell shows no tile. Priority and bits outside the format's fields are kept from the map.
    /// </remarks>
    public MagitekResult WriteEntries(TilemapGrid grid)
    {
        if (PlanWrites(grid, out var data, out var writes) is { } error)
            return new MagitekResult.Failed(error);

        if (writes.Count == 0)
            return MagitekResult.SuccessResult;

        var entryBytes = MapFormat.EntryBytes;
        foreach (var (offset, entry) in writes)
        {
            var buffer = data.AsSpan(offset, entryBytes);
            MapFormat.Encode(entry, buffer);
            MapSource.Write(new BitAddress(MapOffset + offset, 0), buffer);
        }

        MapSource.Flush();
        MapSource.NotifyDataWritten();
        return MagitekResult.SuccessResult;
    }

    /// <summary>
    /// Checks that <see cref="WriteEntries"/> would accept every cell of <paramref name="grid"/>, without writing anything
    /// </summary>
    public MagitekResult CanWriteEntries(TilemapGrid grid) =>
        PlanWrites(grid, out _, out _) is { } error ? new MagitekResult.Failed(error) : MagitekResult.SuccessResult;

    private string? PlanWrites(TilemapGrid grid, out byte[] data, out List<(int Offset, MapEntry Entry)> writes)
    {
        data = [];
        writes = [];

        if (grid.Width != ArrangerElementSize.Width || grid.Height != ArrangerElementSize.Height)
            return $"Tilemap '{Name}' is {ArrangerElementSize.Width}x{ArrangerElementSize.Height} cells, not {grid.Width}x{grid.Height}";

        if (MapReadOnlyReason is { } readOnlyReason)
            return $"The map of tilemap '{Name}' is read-only because it {readOnlyReason}";

        var width = ArrangerElementSize.Width;
        var entryBytes = MapFormat.EntryBytes;
        (data, _) = ReadMapData();

        for (int y = 0; y < ArrangerElementSize.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var offset = ScreenLayout.EntryIndex(x, y, width) * entryBytes;
                var cell = grid[x, y];

                if (offset + entryBytes > data.Length)
                {
                    if (cell is not null)
                        return $"Cell ({x}, {y}) of tilemap '{Name}' has no entry in the map data";
                    continue;
                }

                var current = MapFormat.Decode(data.AsSpan(offset, entryBytes));
                MapEntry target;

                if (cell is not { } entry)
                {
                    if (GetElement(x, y) is null)
                        continue;

                    target = new MapEntry(EmptyTileIndex, 0, false, false, false);
                }
                else
                {
                    target = entry with { Priority = current.Priority };
                }

                if (target != current)
                    writes.Add((offset, target));
            }
        }

        return null;
    }

    /// <summary>
    /// The map and tile sources (Cascade), every assigned slot palette (FallbackPalette) and the tile palette arranger when set (ClearTilePalette)
    /// </summary>
    public IEnumerable<ResourceEdge> Dependencies
    {
        get
        {
            var edges = new List<ResourceEdge> { new(MapSource, ResourceEdgeKind.Cascade), new(TileSource, ResourceEdgeKind.Cascade) };
            edges.AddRange(PaletteSlots.OfType<Palette>().Select(x => new ResourceEdge(x, ResourceEdgeKind.FallbackPalette)));

            if (TilePaletteArranger is not null)
                edges.Add(new(TilePaletteArranger, ResourceEdgeKind.ClearTilePalette));

            return edges.Distinct().ToList();
        }
    }

    public void Dispose()
    {
        MapSource.DataWritten -= OnSourceWritten;
        TileSource.DataWritten -= OnSourceWritten;
    }
}
