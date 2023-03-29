using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek;

public enum MirrorOperation { None, Horizontal, Vertical, Both }
public enum RotationOperation { None, Left, Right, Turn }

/// <summary>
/// Contains all necessary data to encode/decode a single element in the arranger
/// </summary>
public readonly struct ArrangerElement
{
    /// <summary>
    /// DataSource which contains the Element's pixel data
    /// </summary>
    public DataSource Source { get; }

    /// <summary>
    /// BitAddress into Source where the Element's pixel data is located
    /// </summary>
    public BitAddress SourceAddress { get; }

    /// <summary>
    /// Codec used for encoding and decoding
    /// </summary>
    public IGraphicsCodec Codec { get; }

    /// <summary>
    /// Width of Element in pixels
    /// </summary>
    public int Width => Codec.Width;

    /// <summary>
    /// Height of Element in pixels
    /// </summary>
    public int Height => Codec.Height;

    /// <summary>
    /// Left edge of the Element within the Arranger in pixel coordinates, inclusive
    /// </summary>
    public int X1 { get; }

    /// <summary>
    /// Top edge of the Element within the Arranger in pixel coordinates, inclusive
    /// </summary>
    public int Y1 { get; }

    /// <summary>
    /// Right edge of the Element within the Arranger in pixel coordinates, inclusive
    /// </summary>
    public int X2 => X1 + Width - 1;

    /// <summary>
    /// Bottom edge of the Element within the Arranger in pixel coordinates, inclusive
    /// </summary>
    public int Y2 => Y1 + Height - 1;

    public MirrorOperation Mirror { get; }

    public RotationOperation Rotation { get; }

    public ArrangerElement(int x1, int y1, DataSource dataFile, BitAddress address, IGraphicsCodec codec)
    {
        X1 = x1;
        Y1 = y1;
        Source = dataFile;
        SourceAddress = address;
        Codec = codec;
        Mirror = MirrorOperation.None;
        Rotation = RotationOperation.None;
    }

    public ArrangerElement(int x1, int y1, DataSource dataFile, BitAddress address, IGraphicsCodec codec,
        MirrorOperation mirror, RotationOperation rotation)
    {
        X1 = x1;
        Y1 = y1;
        Source = dataFile;
        SourceAddress = address;
        Codec = codec;
        Mirror = mirror;
        Rotation = rotation;
    }

    public ArrangerElement WithLocation(int x1, int y1) =>
        new(x1, y1, Source, SourceAddress, Codec, Mirror, Rotation);

    public ArrangerElement WithFile(DataSource dataFile, BitAddress fileAddress) =>
        new(X1, Y1, dataFile, fileAddress, Codec, Mirror, Rotation);

    public ArrangerElement WithAddress(BitAddress address) =>
        new(X1, Y1, Source, address, Codec, Mirror, Rotation);

    public ArrangerElement WithCodec(IGraphicsCodec codec) =>
        new(X1, Y1, Source, SourceAddress, codec, Mirror, Rotation);

    public ArrangerElement WithCodec(IGraphicsCodec codec, int x1, int y1) =>
        new(x1, y1, Source, SourceAddress, codec, Mirror, Rotation);

    public ArrangerElement WithTarget(DataSource dataFile, BitAddress fileAddress, IGraphicsCodec codec) =>
        new(X1, Y1, dataFile, fileAddress, codec, Mirror, Rotation);

    public ArrangerElement WithMirror(MirrorOperation mirror) =>
        new(X1, Y1, Source, SourceAddress, Codec, mirror, Rotation);

    public ArrangerElement WithRotation(RotationOperation rotation) =>
        new(X1, Y1, Source, SourceAddress, Codec, Mirror, rotation);
}
