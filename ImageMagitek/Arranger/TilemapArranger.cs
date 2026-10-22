using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;

namespace ImageMagitek;

/// <summary>
/// An arranger whose elements are derived from the entries of a tilemap and the tile set they index.
/// Its grid is always what the map decodes to; edits are made on a clone and written back with <see cref="WriteEntries"/>.
/// </summary>
public sealed class TilemapArranger : Arranger, IDisposable
{
    private readonly object _gridLock = new();
    private readonly IGraphicsCodec[] _slotCodecs;
    private readonly ICodecFactory _codecFactory;
    private readonly TileSet _tileSet;
    private readonly ConditionalWeakTable<IGraphicsCodec, StrongBox<int>> _codecSlots = new();
    private ArrangerElement?[,] _grid;
    private int[,] _gridSlots;
    private int _version = 1;
    private int _builtVersion;
    private string? _mapError;
    private int _highestTileIndex;
    private int _outOfRangeCount;

    /// <inheritdoc/>
    protected override ArrangerElement?[,] ElementGrid
    {
        get
        {
            EnsureGrid();
            return _grid;
        }
        set => _grid = value;
    }

    /// <inheritdoc/>
    public override string Name { get; set; }

    /// <inheritdoc/>
    public override bool ShouldBeSerialized { get; set; } = true;

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
                    if (_grid[y, x] is null)
                        continue;

                    var slot = _gridSlots[y, x];
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
        Mode = ArrangerMode.Tilemap;
        Layout = ElementLayout.Tiled;
        ColorType = PixelColorType.Indexed;
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
        _grid = new ArrangerElement?[height, width];
        _gridSlots = new int[height, width];

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
        var width = ArrangerElementSize.Width;
        var height = ArrangerElementSize.Height;
        var grid = new ArrangerElement?[height, width];
        var gridSlots = new int[height, width];
        var entryBytes = MapFormat.EntryBytes;
        var (data, error) = ReadMapData();

        var tileCount = TileCount;
        var highest = -1;
        var outOfRange = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var offset = (long)ScreenLayout.EntryIndex(x, y, width) * entryBytes;
                if (offset + entryBytes > data.Length)
                    continue;

                var entry = MapFormat.Decode(data.AsSpan((int)offset, entryBytes));
                highest = Math.Max(highest, entry.TileIndex);

                if (entry.TileIndex < FirstTileIndex || entry.TileIndex - FirstTileIndex >= tileCount)
                {
                    outOfRange++;
                    continue;
                }

                var slot = entry.Palette < _slotCodecs.Length ? entry.Palette : 0;
                gridSlots[y, x] = slot;
                var codec = _slotCodecs[slot];
                grid[y, x] = _tileSet.CreateTileElement(entry.TileIndex, codec, entry.Mirror, x, y);
            }
        }

        _grid = grid;
        _gridSlots = gridSlots;
        _mapError = error;
        _highestTileIndex = highest;
        _outOfRangeCount = outOfRange;
    }

    /// <exception cref="InvalidOperationException">Always; a tilemap's size comes from its resource definition</exception>
    public override void Resize(int arrangerWidth, int arrangerHeight) =>
        throw new InvalidOperationException($"Tilemap '{Name}' cannot be resized");

    /// <exception cref="InvalidOperationException">Always; edit a clone and write it back with <see cref="WriteEntries"/></exception>
    public override void SetElement(in ArrangerElement? element, int elemX, int elemY) =>
        throw new InvalidOperationException($"Tilemap '{Name}' derives its elements from its map entries; edit a clone and write its entries");

    /// <exception cref="InvalidOperationException">Always; edit a clone and write it back with <see cref="WriteEntries"/></exception>
    public override void ResetElement(int elemX, int elemY) =>
        throw new InvalidOperationException($"Tilemap '{Name}' derives its elements from its map entries; edit a clone and write its entries");

    /// <summary>
    /// Bakes the region into a <see cref="ScatteredArranger"/> whose elements have their own codec instances
    /// </summary>
    protected override Arranger CloneArrangerCore(int posX, int posY, int width, int height)
    {
        var elemX = posX / ElementPixelSize.Width;
        var elemY = posY / ElementPixelSize.Height;
        var elemsWidth = (posX + width + ElementPixelSize.Width - 1) / ElementPixelSize.Width - elemX;
        var elemsHeight = (posY + height + ElementPixelSize.Height - 1) / ElementPixelSize.Height - elemY;

        var arranger = new ScatteredArranger(Name, ColorType, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);
        var clonedCodecs = new Dictionary<int, IGraphicsCodec>();
        var grid = ElementGrid;
        var gridSlots = _gridSlots;

        for (int y = 0; y < elemsHeight; y++)
        {
            for (int x = 0; x < elemsWidth; x++)
            {
                if (grid[y + elemY, x + elemX] is not ArrangerElement el)
                    continue;

                var slot = gridSlots[y + elemY, x + elemX];
                if (!clonedCodecs.TryGetValue(slot, out var codec))
                {
                    codec = CloneCodec(el.Codec, slot);
                    clonedCodecs.Add(slot, codec);
                }

                arranger.SetElement(el.WithCodec(codec, x * ElementPixelSize.Width, y * ElementPixelSize.Height), x, y);
            }
        }

        return arranger;
    }

    /// <summary>
    /// A new codec instance showing the palette of <paramref name="slot"/>, shared by no other arranger
    /// </summary>
    public IGraphicsCodec CloneSlotCodec(int slot) => CloneCodec(_slotCodecs[slot], slot);

    /// <summary>
    /// The element for cell (<paramref name="cellX"/>, <paramref name="cellY"/>) reading tile <paramref name="tileIndex"/> of the tile set
    /// </summary>
    public ArrangerElement CreateTileElement(int tileIndex, IGraphicsCodec codec, MirrorOperation mirror, int cellX, int cellY) =>
        _tileSet.CreateTileElement(tileIndex, codec, mirror, cellX, cellY);

    private IGraphicsCodec CloneCodec(IGraphicsCodec codec, int slot)
    {
        var clone = _codecFactory.CloneCodec(codec);
        ((IIndexedCodec)clone).Palette = ((IIndexedCodec)codec).Palette;
        _codecSlots.AddOrUpdate(clone, new StrongBox<int>(slot));
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
    /// The palette slot an entry should name for a cell shown with <paramref name="codec"/>: LIB-TILEMAPS-042 preferring the slot the codec
    /// was cloned for by this tilemap, otherwise <paramref name="fallbackSlot"/>; null when the codec's palette is in no slot
    /// </summary>
    public int? FindCodecSlot(IGraphicsCodec codec, int fallbackSlot)
    {
        if (codec is not IIndexedCodec { Palette: { } palette })
            return null;

        var preferred = _codecSlots.TryGetValue(codec, out var slot) ? slot.Value : fallbackSlot;
        return FindPaletteSlot(palette, preferred);
    }

    /// <summary>
    /// The tile index an entry should hold to show the tile <paramref name="element"/> reads, or why it is not a tile of this tile set
    /// </summary>
    /// <returns>On failure, a reason that completes "element at (x, y) ..."</returns>
    public MagitekResult<int> FindTileIndex(ArrangerElement element) => _tileSet.FindTileIndex(element, MapFormat);

    /// <summary>
    /// Checks that <paramref name="pasted"/> is a tile of this tile set and gives it a palette a slot holds:
    /// its own when a slot holds it, otherwise the palette of the slot <paramref name="destination"/> uses (slot 0 when the cell is empty)
    /// </summary>
    public MagitekResult<ArrangerElement> ToCellElement(ArrangerElement pasted, ArrangerElement? destination)
    {
        var tileResult = FindTileIndex(pasted);
        if (tileResult.HasFailed)
            return new MagitekResult<ArrangerElement>.Failed(tileResult.AsError.Reason);

        if (pasted.Codec is IIndexedCodec { Palette: { } palette } && FindPaletteSlot(palette, 0) is not null)
            return new MagitekResult<ArrangerElement>.Success(pasted);

        var slot = destination?.Codec is IIndexedCodec { Palette: { } destinationPalette } ? FindPaletteSlot(destinationPalette, 0) ?? 0 : 0;
        var codec = CloneCodec(_slotCodecs[slot], slot);
        return new MagitekResult<ArrangerElement>.Success(pasted.WithCodec(codec));
    }

    /// <summary>
    /// Writes the map entries that differ from <paramref name="working"/>, an edited arranger of the same size such as a clone, then flushes and notifies once.
    /// Nothing is written when any cell is refused.
    /// </summary>
    /// <remarks>
    /// An empty working cell becomes the empty entry, unless this tilemap's own cell is empty too. Priority and bits outside the format's fields are kept.
    /// </remarks>
    public MagitekResult WriteEntries(Arranger working)
    {
        if (PlanWrites(working, out var data, out var writes) is { } error)
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
    /// Checks that <see cref="WriteEntries"/> would accept every cell of <paramref name="working"/>, without writing anything
    /// </summary>
    public MagitekResult CanWriteEntries(Arranger working) =>
        PlanWrites(working, out _, out _) is { } error ? new MagitekResult.Failed(error) : MagitekResult.SuccessResult;

    private string? PlanWrites(Arranger working, out byte[] data, out List<(int Offset, MapEntry Entry)> writes)
    {
        data = [];
        writes = [];

        if (working.ArrangerElementSize != ArrangerElementSize)
            return $"Tilemap '{Name}' is {ArrangerElementSize.Width}x{ArrangerElementSize.Height} cells, not {working.ArrangerElementSize.Width}x{working.ArrangerElementSize.Height}";

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
                var cell = working.GetElement(x, y);

                if (offset + entryBytes > data.Length)
                {
                    if (cell is not null)
                        return $"Cell ({x}, {y}) of tilemap '{Name}' has no entry in the map data";
                    continue;
                }

                var current = MapFormat.Decode(data.AsSpan(offset, entryBytes));
                MapEntry target;

                if (cell is not ArrangerElement el)
                {
                    if (GetElement(x, y) is null)
                        continue;

                    target = new MapEntry(EmptyTileIndex, 0, false, false, false);
                }
                else
                {
                    var tileResult = FindTileIndex(el);
                    if (tileResult.HasFailed)
                        return $"Cell ({x}, {y}) of tilemap '{Name}' {tileResult.AsError.Reason}";

                    if (FindCodecSlot(el.Codec, current.Palette) is not int slot)
                        return $"Cell ({x}, {y}) of tilemap '{Name}' uses a palette that is in no palette slot";

                    target = new MapEntry(tileResult.AsSuccess.Result, slot, el.Mirror.FlipsHorizontally(), el.Mirror.FlipsVertically(), current.Priority);
                }

                if (target != current)
                    writes.Add((offset, target));
            }
        }

        return null;
    }

    /// <summary>
    /// The map source, the tile source and every assigned palette slot
    /// </summary>
    public override IEnumerable<IProjectResource> LinkedResources =>
        new IProjectResource[] { MapSource, TileSource }.Concat(PaletteSlots.OfType<Palette>()).Distinct().ToList();

    public void Dispose()
    {
        MapSource.DataWritten -= OnSourceWritten;
        TileSource.DataWritten -= OnSourceWritten;
    }
}
