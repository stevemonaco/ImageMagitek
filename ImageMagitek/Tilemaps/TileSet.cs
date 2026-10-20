using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// A range of same-sized indexed tiles in a data source, numbered from <see cref="FirstTileIndex"/>, that map entries index into.
/// A tilemap has one; a metatile map resolves an index across several by their ranges.
/// </summary>
public sealed class TileSet
{
    private readonly ICodecFactory _codecFactory;
    private readonly long _tileBits;

    public DataSource Source { get; }
    public long Offset { get; }
    public string CodecName { get; }
    public int FirstTileIndex { get; }

    /// <summary>
    /// The number of tiles the set declares, or null when it runs to the end of <see cref="Source"/>
    /// </summary>
    public int? DeclaredCount { get; }

    public Size TileSize { get; }

    /// <summary>
    /// The number of tiles from <see cref="Offset"/> that lie wholly in the source and are within <see cref="DeclaredCount"/>
    /// </summary>
    public int TileCount
    {
        get
        {
            var tileBits = Source is FileDataSource { IsMissing: true } ? 0 : Source.Length * 8;
            var inSource = Math.Max(0, tileBits - Offset * 8) / _tileBits;
            return (int)Math.Min(inSource, DeclaredCount ?? int.MaxValue);
        }
    }

    /// <exception cref="ArgumentException">The declared count is not positive, or the codec is unknown or not indexed; the message completes the owner's name</exception>
    public TileSet(DataSource source, long offset, string codecName, int firstTileIndex, int? declaredCount, ICodecFactory codecFactory)
    {
        if (declaredCount <= 0)
            throw new ArgumentException($"must declare a positive tile count, not {declaredCount}");

        Source = source;
        Offset = offset;
        CodecName = codecName;
        FirstTileIndex = firstTileIndex;
        DeclaredCount = declaredCount;
        _codecFactory = codecFactory;

        var codec = CreateCodec(null);
        TileSize = new Size(codec.Width, codec.Height);
        _tileBits = codec.StorageSize;
    }

    /// <summary>
    /// Whether <paramref name="tileIndex"/> lies in [<see cref="FirstTileIndex"/>, <see cref="FirstTileIndex"/> + <see cref="TileCount"/>)
    /// </summary>
    public bool Contains(int tileIndex) => tileIndex >= FirstTileIndex && (long)tileIndex - FirstTileIndex < TileCount;

    public BitAddress TileAddress(int tileIndex) => new(Offset * 8 + ((long)tileIndex - FirstTileIndex) * _tileBits);

    /// <summary>
    /// One codec per palette slot; an unassigned slot shares slot 0's codec
    /// </summary>
    public IGraphicsCodec[] CreateSlotCodecs(IReadOnlyList<Palette?> slots)
    {
        var codecs = new IGraphicsCodec[slots.Count];
        for (int i = 0; i < codecs.Length; i++)
            codecs[i] = i == 0 || slots[i] is not null ? CreateCodec(slots[i]) : codecs[0];

        return codecs;
    }

    /// <summary>
    /// A new instance of the tile codec, showing <paramref name="palette"/> or the codec's default palette when null
    /// </summary>
    /// <exception cref="ArgumentException">The codec is unknown or not indexed; the message completes the owner's name</exception>
    public IGraphicsCodec CreateCodec(Palette? palette)
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
            throw new ArgumentException($"uses unknown codec '{CodecName}'");

        if (codec is not IIndexedCodec indexedCodec)
            throw new ArgumentException($"uses direct codec '{CodecName}'; tile codecs must be indexed");

        if (palette is not null)
            indexedCodec.Palette = palette;

        return codec;
    }

    /// <summary>
    /// The element for cell (<paramref name="cellX"/>, <paramref name="cellY"/>) of a grid of tiles, reading tile <paramref name="tileIndex"/>
    /// </summary>
    public ArrangerElement CreateTileElement(int tileIndex, IGraphicsCodec codec, MirrorOperation mirror, int cellX, int cellY) =>
        new(cellX * TileSize.Width, cellY * TileSize.Height, Source, TileAddress(tileIndex), codec, mirror, RotationOperation.None);

    /// <summary>
    /// The tile index an entry of <paramref name="format"/> should hold to show the tile <paramref name="element"/> reads, or why it cannot
    /// </summary>
    /// <returns>On failure, a reason that completes "element at (x, y) ..."</returns>
    public MagitekResult<int> FindTileIndex(ArrangerElement element, MapFormat format)
    {
        if (!ReferenceEquals(element.Source, Source))
            return new MagitekResult<int>.Failed($"reads data file '{element.Source.Name}', not the tile set's data file '{Source.Name}'");

        if (element.Codec.Name != CodecName)
            return new MagitekResult<int>.Failed($"uses codec '{element.Codec.Name}', not the tile set's codec '{CodecName}'");

        if (element.Rotation != RotationOperation.None)
            return new MagitekResult<int>.Failed("is rotated; tilemap entries cannot be rotated");

        if (element.Mirror.FlipsHorizontally() && format.HorizontalFlip is null)
            return new MagitekResult<int>.Failed($"is mirrored horizontally; map format '{format.Name}' has no horizontal flip field");

        if (element.Mirror.FlipsVertically() && format.VerticalFlip is null)
            return new MagitekResult<int>.Failed($"is mirrored vertically; map format '{format.Name}' has no vertical flip field");

        var bit = element.SourceAddress.Offset - Offset * 8;
        if (bit >= 0 && bit % _tileBits != 0)
            return new MagitekResult<int>.Failed($"reads 0x{element.SourceAddress.ByteOffset:X}, which is not on a tile boundary of the tile set");

        if (bit < 0 || element.SourceAddress.Offset + _tileBits > Source.Length * 8)
            return new MagitekResult<int>.Failed($"reads 0x{element.SourceAddress.ByteOffset:X}, which is outside the tile set");

        var index = bit / _tileBits + FirstTileIndex;
        if (index > format.MaxTileIndex)
            return new MagitekResult<int>.Failed($"is tile {index}, which does not fit the {format.TileIndex.Bits}-bit tile field of map format '{format.Name}'");

        return new MagitekResult<int>.Success((int)index);
    }
}
