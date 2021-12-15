namespace ImageMagitek.Colors;

public class FileColorSource : IColorSource
{
    public BitAddress Offset { get; set; }
    public Endian Endian { get; set; }

    public FileColorSource(BitAddress offset, Endian endian)
    {
        Offset = offset;
        Endian = endian;
    }
}
