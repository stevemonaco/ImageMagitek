using System.IO;
using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek
{
    public interface IImageFileAdapter
    {
        void SaveImage(byte[] image, Arranger arranger, string imagePath);
        void SaveImage(ColorRgba32[] image, int width, int height, string imagePath);
        byte[] LoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy);
        MagitekResult TryLoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy, out byte[] image);
        ColorRgba32[] LoadImage(string imagePath);
    }

    public class ImageFileAdapter : IImageFileAdapter
    {
        public void SaveImage(byte[] image, Arranger arranger, string imagePath)
        {
            var width = arranger.ArrangerPixelSize.Width;
            var height = arranger.ArrangerPixelSize.Height;
            using var outputImage = new Image<Rgba32>(width, height);

            var srcidx = 0;

            for (int y = 0; y < height; y++)
            {
                var span = outputImage.GetPixelRowSpan(y);

                for (int x = 0; x < width; x++, srcidx++)
                {
                    var pal = arranger.GetElementAtPixel(x, y).Palette;
                    var index = image[srcidx];
                    var color = pal[index];
                    span[x] = color.ToRgba32();
                }
            }

            using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            outputImage.SaveAsPng(outputStream);
        }

        public void SaveImage(ColorRgba32[] image, int width, int height, string imagePath)
        {
            using var outputImage = new Image<Rgba32>(width, height);
            var srcidx = 0;

            for (int y = 0; y < height; y++)
            {
                var span = outputImage.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++, srcidx++)
                {
                    span[x] = image[srcidx].ToRgba32();
                }
            }

            using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            outputImage.SaveAsPng(outputStream);
        }

        public byte[] LoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy)
        {
            using var inputImage = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);
            var width = inputImage.Width;
            var height = inputImage.Height;

            var outputImage = new byte[width * height];
            int destidx = 0;

            for (int y = 0; y < height; y++)
            {
                var span = inputImage.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++, destidx++)
                {
                    var pal = arranger.GetElementAtPixel(x, y).Palette;
                    var color = new ColorRgba32(span[x].PackedValue);
                    var palIndex = pal.GetIndexByNativeColor(color, matchStrategy);
                    outputImage[destidx] = palIndex;
                }
            }

            return outputImage;
        }

        public MagitekResult TryLoadImage(string imagePath, Arranger arranger, ColorMatchStrategy matchStrategy, out byte[] image)
        {
            using var inputImage = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);
            var width = inputImage.Width;
            var height = inputImage.Height;

            if (width != arranger.ArrangerPixelSize.Width || height != arranger.ArrangerPixelSize.Height)
            {
                image = default;
                return new MagitekResult.Failed($"Arranger dimensions ({arranger.ArrangerPixelSize.Width}, {arranger.ArrangerPixelSize.Height})" +
                    $" do not match image dimensions ({width}, {height})");
            }

            image = new byte[width * height];
            int destidx = 0;

            for (int y = 0; y < height; y++)
            {
                var span = inputImage.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++, destidx++)
                {
                    var pal = arranger.GetElementAtPixel(x, y).Palette;
                    var color = new ColorRgba32(span[x].PackedValue);

                    if (pal.TryGetIndexByNativeColor(color, matchStrategy, out var palIndex))
                    {
                        image[destidx] = palIndex;
                    }
                    else
                    {
                        return new MagitekResult.Failed($"Could not match image color (R: {color.R}, G: {color.G}, B: {color.B}, A: {color.A}) within palette '{pal.Name}'");
                    }
                }
            }

            return MagitekResult.SuccessResult;
        }

        public ColorRgba32[] LoadImage(string imagePath)
        {
            using var inputImage = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);
            var width = inputImage.Width;
            var height = inputImage.Height;

            var outputImage = new ColorRgba32[width * height];
            int destidx = 0;

            for (int y = 0; y < height; y++)
            {
                var span = inputImage.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var color = new ColorRgba32(span[x].PackedValue);
                    outputImage[destidx] = color;
                }
            }

            return outputImage;
        }


        //public void SaveImage(DirectImage image, string imagePath)
        //{
        //    using var outputImage = new Image<Rgba32>(image.Width, image.Height);

        //    var span = outputImage.GetPixelSpan();

        //    for (int i = 0; i < span.Length; i++)
        //    {
        //        var color = image.Image[i].ToRgba32();
        //        span[i] = color;
        //    }

        //    using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        //    outputImage.SaveAsPng(outputStream);
        //}

        //public void SaveImage(IndexedImage image, Palette pal, string imagePath)
        //{
        //    using var outputImage = new Image<Rgba32>(image.Width, image.Height);

        //    var span = outputImage.GetPixelSpan();

        //    for (int i = 0; i < span.Length; i++)
        //    {
        //        var color = pal.GetNativeColor(image.Image[i]);
        //        span[i] = color.ToRgba32();
        //    }

        //    using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        //    outputImage.SaveAsPng(outputStream);
        //}

        //public void SaveImage(IndexedImage image, Arranger arranger, string imagePath)
        //{
        //    using var outputImage = new Image<Rgba32>(image.Width, image.Height);

        //    for (int y = 0; y < image.Height; y++)
        //    {
        //        for (int x = 0; x < image.Width; x++)
        //        {
        //            var span = outputImage.GetPixelRowSpan(y);
        //            var pal = arranger.GetElement(x / image.Width, y / image.Height).Palette;
        //            var color = pal[image.GetPixel(x, y)];
        //            span[x] = color.ToRgba32();
        //        }
        //    }

        //    using var outputStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        //    outputImage.SaveAsPng(outputStream);
        //}

        //public DirectImage LoadDirectImage(string imagePath)
        //{
        //    throw new NotImplementedException();
        //    //using var image = Image.Load(imagePath);

        //    //var direct = new DirectImage(image.Width, image.Height);

        //    //var span = image.GetPixelSpan();

        //    //for (int i = 0; i < span.Length; i++)
        //    //{
        //    //    var color = new ColorRgba32(span[i].R, span[i].G, span[i].B, span[i].A);
        //    //    direct.Image[i] = color;
        //    //}

        //    //return direct;
        //}

        //public IndexedImage LoadIndexedImage(string imagePath, Palette pal)
        //{
        //    throw new NotImplementedException();
        //    //using var image = Image.Load(imagePath);

        //    //var indexed = new IndexedImage(image.Width, image.Height);

        //    //var span = image.GetPixelSpan();

        //    //for (int i = 0; i < span.Length; i++)
        //    //{
        //    //    var nc = new ColorRgba32(span[i].R, span[i].G, span[i].B, span[i].A);
        //    //    if (pal.ContainsNativeColor(nc))
        //    //        throw new Exception($"{nameof(LoadIndexedImage)}: Image '{imagePath}' contained a color not contained by the palette '{pal.Name}'");

        //    //    indexed.Image[i] = pal.GetIndexByNativeColor(nc, true);
        //    //}

        //    //return indexed;
        //}
    }
}
