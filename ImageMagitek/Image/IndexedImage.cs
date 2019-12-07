namespace ImageMagitek
{
    public class IndexedImage : ImageBase<byte>
    {
        public IndexedImage(int width, int height)
        {
            Width = width;
            Height = height;
            Image = new byte[Width * Height];
        }
    }
}