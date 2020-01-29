using System;
using System.IO;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek
{
    public interface IImageFileAdapter
    {
        void SaveImage(DirectImage image, string imageFileName);
        void SaveImage(IndexedImage image, Palette pal, string imageFileName);
        DirectImage LoadDirectImage(string imageFileName);
        IndexedImage LoadIndexedImage(string imageFileName, Palette pal);
    }

    public class ImageFileAdapter : IImageFileAdapter
    {
        public void SaveImage(DirectImage image, string imageFileName)
        {
            using var outputImage = new Image<Rgba32>(image.Width, image.Height);

            var span = outputImage.GetPixelSpan();

            for (int i = 0; i < span.Length; i++)
            {
                var color = image.Image[i].ToRgba32();
                span[i] = color;
            }

            using var outputStream = new FileStream(imageFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
            outputImage.SaveAsPng(outputStream);
        }

        public void SaveImage(IndexedImage image, Palette pal, string imageFileName)
        {
            using var outputImage = new Image<Rgba32>(image.Width, image.Height);

            var span = outputImage.GetPixelSpan();

            for (int i = 0; i < span.Length; i++)
            {
                var color = pal.GetNativeColor(image.Image[i]);
                span[i] = color.ToRgba32();
            }

            using var outputStream = new FileStream(imageFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
            outputImage.SaveAsPng(outputStream);
        }

        public DirectImage LoadDirectImage(string imageFileName)
        {
            using var image = Image.Load(imageFileName);

            var direct = new DirectImage(image.Width, image.Height);

            var span = image.GetPixelSpan();

            for (int i = 0; i < span.Length; i++)
            {
                var color = new ColorRgba32(span[i].R, span[i].G, span[i].B, span[i].A);
                direct.Image[i] = color;
            }

            return direct;
        }

        public IndexedImage LoadIndexedImage(string imageFileName, Palette pal)
        {
            using var image = Image.Load(imageFileName);

            var indexed = new IndexedImage(image.Width, image.Height);

            var span = image.GetPixelSpan();

            for (int i = 0; i < span.Length; i++)
            {
                var nc = new ColorRgba32(span[i].R, span[i].G, span[i].B, span[i].A);
                if (pal.ContainsNativeColor(nc))
                    throw new Exception($"{nameof(LoadIndexedImage)}: Image '{imageFileName}' contained a color not contained by the palette '{pal.Name}'");

                indexed.Image[i] = pal.GetIndexByNativeColor(nc, true);
            }

            return indexed;
        }
    }
}
