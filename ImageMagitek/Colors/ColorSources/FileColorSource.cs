namespace ImageMagitek.Colors
{
    public class FileColorSource : IColorSource
    {
        public FileBitAddress Offset { get; set; }
        public Endian Endian { get; set; }

        public FileColorSource(FileBitAddress offset, Endian endian)
        {
            Offset = offset;
            Endian = endian;
        }
    }
}
