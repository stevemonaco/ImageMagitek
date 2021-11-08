namespace ImageMagitek.Codec;

public readonly struct PlaneCoordinate
{
    /// <summary>
    /// X-coordinate
    /// </summary>
    public short X { get; }

    /// <summary>
    /// Y-coordinate
    /// </summary>
    public short Y { get; }

    /// <summary>
    /// Plane-coordinate
    /// </summary>
    public short P { get; }

    public PlaneCoordinate(short x, short y, short plane)
    {
        X = x;
        Y = y;
        P = plane;
    }
}
