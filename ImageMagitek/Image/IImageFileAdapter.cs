using ImageMagitek.Colors;

namespace ImageMagitek;

public interface IImageFileAdapter
{
    void SaveImage(byte[] image, Arranger arranger, string imagePath);
    void SaveImage(ColorRgba32[] image, int width, int height, string imagePath);
    MagitekResult<DecodedImage> LoadImage(string imagePath);
}
