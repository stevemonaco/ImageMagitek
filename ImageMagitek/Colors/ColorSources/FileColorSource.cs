namespace ImageMagitek.Colors;

public class FileColorSource : IColorSource
{
    /// <summary>
    /// Byte offset of the color within the palette's data file
    /// </summary>
    public long Offset { get; set; }
    public Endian Endian { get; set; }

    public FileColorSource(long offset, Endian endian)
    {
        Offset = offset;
        Endian = endian;
    }
}
