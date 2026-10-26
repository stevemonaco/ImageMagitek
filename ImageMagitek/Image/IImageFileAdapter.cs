using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek;

public interface IImageFileAdapter
{
    void SaveImage(byte[] image, IArrangerView arranger, string imagePath);
    void SaveImage(byte[] image, IArrangerView arranger, Rectangle subsection, string imagePath);
    void SaveImage(ColorRgba32[] image, int width, int height, string imagePath);
    MagitekResult<DecodedImage> LoadImage(string imagePath);
}
