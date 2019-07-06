namespace ImageMagitek
{
    public interface IArrangerImage<TPixel>
    {
        bool Render(Arranger arranger);
        bool LoadImage(string imageFileName);
        bool SaveImage(Arranger arranger);
        TPixel GetPixel(int x, int y);
        void SetPixel(int x, int y, TPixel color);
    }
}
