using System;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek
{
    public class DirectImage : ImageBase<ColorRgba32>
    {
        public DirectImage(Arranger arranger)
        {
            if (arranger is null)
                throw new ArgumentNullException($"{nameof(DirectImage)}.Ctor parameter '{nameof(arranger)}' was null");

            Arranger = arranger;
            Image = new ColorRgba32[Width * Height];
            Render();
        }

        public override void ExportImage(string imagePath, IImageFileAdapter adapter) =>
            adapter.SaveImage(Image, Width, Height, imagePath);

        public void ImportImage(string imagePath, IImageFileAdapter adapter)
        {
            var importImage = adapter.LoadImage(imagePath);
            importImage.CopyTo(Image, 0);
        }

        public override void Render()
        {
            if (Width <= 0 || Height <= 0)
                throw new InvalidOperationException($"{nameof(Render)}: arranger dimensions for '{Arranger.Name}' are too small to render " +
                    $"({Width}, {Height})");

            if (Width * Height != Image.Length)
                Image = new ColorRgba32[Width * Height];

            //var buffer = new ColorRgba32[Arranger.ElementPixelSize.Width, Arranger.ElementPixelSize.Height];

            foreach (var el in Arranger.EnumerateElements().Where(x => x.DataFile is object))
            {
                if (el.Codec is IDirectCodec codec)
                {
                    var encodedBuffer = codec.ReadElement(el);

                    // TODO: Detect reads past end of file gracefully
                    if (encodedBuffer.Length == 0)
                        continue;

                    var decodeResult = codec.DecodeElement(el, encodedBuffer);

                    for (int y = 0; y < Arranger.ElementPixelSize.Height; y++)
                    {
                        var destidx = y * Width;
                        for (int x = 0; x < Arranger.ElementPixelSize.Width; x++)
                        {
                            Image[destidx] = decodeResult[x, y];
                            destidx++;
                        }
                    }
                }
            }
        }

        public override void SaveImage()
        {
            var buffer = new ColorRgba32[Arranger.ElementPixelSize.Width, Arranger.ElementPixelSize.Height];
            foreach (var el in Arranger.EnumerateElements().Where(x => x.Codec is IDirectCodec))
            {
                Image.CopyToArray(buffer, el.X1, el.Y1, Width, el.Width, el.Height);
                var codec = el.Codec as IDirectCodec;

                var encodeResult = codec.EncodeElement(el, buffer);
                codec.WriteElement(el, encodeResult);

                //codec.Encode(el, buffer);
            }
            foreach (var fs in Arranger.EnumerateElements().Select(x => x.DataFile.Stream).Distinct())
                fs.Flush();
        }
    }
}
