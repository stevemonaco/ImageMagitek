using ImageMagitek.Colors;

namespace ImageMagitek;

public interface IImageFileAdapter
{
    void SaveImage(byte[] image, Arranger arranger, string imagePath);
    void SaveImage(ColorRgba32[] image, int width, int height, string imagePath);
    byte[] LoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy);
    MagitekResult TryLoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy, out byte[] image);
    ColorRgba32[] LoadImage(string imagePath);
}
