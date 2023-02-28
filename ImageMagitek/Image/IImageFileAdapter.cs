using ImageMagitek.Colors;
using System.Diagnostics.CodeAnalysis;

namespace ImageMagitek;

public interface IImageFileAdapter
{
    void SaveImage(byte[] image, Arranger arranger, string imagePath);
    void SaveImage(ColorRgba32[] image, int width, int height, string imagePath);
    byte[] LoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy);
    MagitekResult TryLoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy, [MaybeNullWhen(false)] out byte[] image);
    ColorRgba32[] LoadImage(string imagePath);
}
