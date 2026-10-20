using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Tilemaps;

namespace ImageMagitek;

/// <summary>
/// A read-only arranger whose elements are derived from the entries of a tilemap and the tile set they index
/// </summary>
public sealed class TilemapArranger : Arranger, IDisposable
{
    private readonly object _gridLock = new();
    private readonly IGraphicsCodec[] _slotCodecs;
    private readonly ICodecFactory _codecFactory;
    private ArrangerElement?[,] _grid;
    private volatile bool _isDirty = true;
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
    public DataSource TileSource { get; }
    public long TileOffset { get; }
    public string CodecName { get; }
    public int FirstTileIndex { get; }

    /// <summary>
    /// The palette for each value of the entry's palette field; an empty slot uses slot 0
    /// </summary>
    public IReadOnlyList<Palette?> PaletteSlots { get; }

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

    /// <exception cref="ArgumentException">The codec is unknown or not indexed, the size is not positive, or the width does not suit the screen layout</exception>
    public TilemapArranger(string name, DataSource mapSource, long mapOffset, MapFormat mapFormat, MapScreenLayout screenLayout,
        int width, int height, DataSource tileSource, long tileOffset, string codecName, int firstTileIndex,
        IReadOnlyList<Palette?> paletteSlots, ICodecFactory codecFactory)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Tilemap '{name}' must have a positive width and height, not {width}x{height}");

        if (screenLayout.FindWidthError(width) is { } widthError)
            throw new ArgumentException($"Tilemap '{name}': {widthError}");

        Name = name;
        Mode = ArrangerMode.Tilemap;
        Layout = ElementLayout.Tiled;
        ColorType = PixelColorType.Indexed;
        MapSource = mapSource;
        MapOffset = mapOffset;
        MapFormat = mapFormat;
        ScreenLayout = screenLayout;
        TileSource = tileSource;
        TileOffset = tileOffset;
        CodecName = codecName;
        FirstTileIndex = firstTileIndex;
        _codecFactory = codecFactory;

        var slots = new Palette?[mapFormat.PaletteSlotCount];
        for (int i = 0; i < Math.Min(slots.Length, paletteSlots.Count); i++)
            slots[i] = paletteSlots[i];
        PaletteSlots = slots;

        _slotCodecs = new IGraphicsCodec[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == 0 || slots[i] is not null)
                _slotCodecs[i] = CreateSlotCodec(slots[i]);
            else
                _slotCodecs[i] = _slotCodecs[0];
        }

        ElementPixelSize = new Size(_slotCodecs[0].Width, _slotCodecs[0].Height);
        ArrangerElementSize = new Size(width, height);
        _grid = new ArrangerElement?[height, width];

        MapSource.DataWritten += OnSourceWritten;
        if (!ReferenceEquals(TileSource, MapSource))
            TileSource.DataWritten += OnSourceWritten;
    }

    private IGraphicsCodec CreateSlotCodec(Palette? palette)
    {
        IGraphicsCodec? codec;
        try
        {
            codec = _codecFactory.CreateCodec(CodecName);
        }
        catch (KeyNotFoundException)
        {
            codec = null;
        }

        if (codec is null)
            throw new ArgumentException($"Tilemap '{Name}' uses unknown codec '{CodecName}'");

        if (codec is not IIndexedCodec indexedCodec)
            throw new ArgumentException($"Tilemap '{Name}' uses direct codec '{CodecName}'; tile codecs must be indexed");

        if (palette is not null)
            indexedCodec.Palette = palette;

        return codec;
    }

    private void OnSourceWritten(object? sender, EventArgs e) => _isDirty = true;

    private void EnsureGrid()
    {
        if (!_isDirty)
            return;

        lock (_gridLock)
        {
            if (_isDirty)
            {
                Rebuild();
                _isDirty = false;
            }
        }
    }

    private void Rebuild()
    {
        var width = ArrangerElementSize.Width;
        var height = ArrangerElementSize.Height;
        var grid = new ArrangerElement?[height, width];
        var entryBytes = MapFormat.EntryBytes;
        var entryCount = (long)ScreenLayout.EntryIndex(width - 1, height - 1, width) + 1;
        var neededBytes = entryCount * entryBytes;

        string? error = null;
        byte[] data = [];

        if (MapSource is CompressedDataSource { DecompressionError: { } decompressionError })
        {
            error = decompressionError;
        }
        else if (MapSource is FileDataSource { IsMissing: true })
        {
            error = $"Data file '{MapSource.Name}' is missing";
        }
        else
        {
            var available = Math.Max(0, MapSource.Length - MapOffset);
            var readBytes = (int)Math.Min(neededBytes, available);
            if (readBytes > 0)
                data = MapSource.Read(new BitAddress(MapOffset, 0), readBytes * 8);

            if (readBytes < neededBytes)
                error = $"The map data in '{MapSource.Name}' holds {readBytes / entryBytes} of the {entryCount} entries a {width}x{height} map needs";
        }

        var storageBits = (long)_slotCodecs[0].StorageSize;
        var tileBits = TileSource is FileDataSource { IsMissing: true } ? 0 : TileSource.Length * 8;
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

                var tile = (long)entry.TileIndex - FirstTileIndex;
                var address = TileOffset * 8 + tile * storageBits;
                if (tile < 0 || address + storageBits > tileBits)
                {
                    outOfRange++;
                    continue;
                }

                var codec = _slotCodecs[entry.Palette < _slotCodecs.Length ? entry.Palette : 0];
                grid[y, x] = new ArrangerElement(x * ElementPixelSize.Width, y * ElementPixelSize.Height, TileSource,
                    new BitAddress(address), codec, entry.Mirror, RotationOperation.None);
            }
        }

        _grid = grid;
        _mapError = error;
        _highestTileIndex = highest;
        _outOfRangeCount = outOfRange;
    }

    /// <exception cref="InvalidOperationException">Always; a tilemap's size comes from its resource definition</exception>
    public override void Resize(int arrangerWidth, int arrangerHeight) =>
        throw new InvalidOperationException($"Tilemap '{Name}' cannot be resized");

    /// <exception cref="InvalidOperationException">Always; a tilemap's elements come from its map entries</exception>
    public override void SetElement(in ArrangerElement? element, int elemX, int elemY) =>
        throw new InvalidOperationException($"Tilemap '{Name}' derives its elements from its map entries, which cannot be set yet");

    /// <exception cref="InvalidOperationException">Always; a tilemap's elements come from its map entries</exception>
    public override void ResetElement(int elemX, int elemY) =>
        throw new InvalidOperationException($"Tilemap '{Name}' derives its elements from its map entries, which cannot be reset yet");

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
        var clonedCodecs = new Dictionary<IGraphicsCodec, IGraphicsCodec>(ReferenceEqualityComparer.Instance);

        for (int y = 0; y < elemsHeight; y++)
        {
            for (int x = 0; x < elemsWidth; x++)
            {
                if (ElementGrid[y + elemY, x + elemX] is not ArrangerElement el)
                    continue;

                if (!clonedCodecs.TryGetValue(el.Codec, out var codec))
                {
                    codec = _codecFactory.CloneCodec(el.Codec);
                    ((IIndexedCodec)codec).Palette = ((IIndexedCodec)el.Codec).Palette;
                    clonedCodecs.Add(el.Codec, codec);
                }

                arranger.SetElement(el.WithCodec(codec, x * ElementPixelSize.Width, y * ElementPixelSize.Height), x, y);
            }
        }

        return arranger;
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
