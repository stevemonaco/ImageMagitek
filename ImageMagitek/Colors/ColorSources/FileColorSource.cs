namespace ImageMagitek.Colors
{
    public class FileColorSource : IColorSource
    {
        public FileBitAddress Offset { get; set; }

        public FileColorSource(FileBitAddress offset)
        {
            Offset = offset;
        }
    }
}
