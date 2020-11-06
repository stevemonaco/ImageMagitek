using System;
using System.Collections.Generic;
using System.IO;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SixLabors.ImageSharp;
using System.Linq;

namespace ImageMagitek.Benchmarks
{
    public class Snes3bppDecodeToImage
    {
        private const string nativeFileName = "Snes3bppDecodeToImageNative.bin";
        private const string genericFileName = "Snes3bppDecodeToImageGeneric.bin";
        private const string genericCodecFileName = "SNES3bpp.xml";
        private const string paletteFileName = "DefaultRgba32.json";
        private const string outputDirectory = @"F:\Projects\ImageMagitek\Benchmark\";

        private IGraphicsCodec Codec;

        private DataFile df;
        private Palette pal;
        private ScatteredArranger arranger;

        [GlobalSetup(Target = nameof(DecodeNative))]
        public void GlobalSetupNative()
        {
            var palFileName = Path.Combine(Directory.GetCurrentDirectory(), "Resources", paletteFileName);
            pal = PaletteJsonSerializer.ReadPalette(palFileName);

            Codec = new Snes3bppCodec(8, 8);
            Setup(nativeFileName, "native");
        }

        [GlobalSetup(Target = nameof(DecodeGeneric))]
        public void GlobalSetupGeneric()
        {
            var palFileName = Path.Combine(Directory.GetCurrentDirectory(), "Resources", paletteFileName);
            pal = PaletteJsonSerializer.ReadPalette(palFileName);

            var codecFileName = Path.Combine(Directory.GetCurrentDirectory(), "Resources", genericCodecFileName);
            var serializer = new XmlGraphicsFormatReader();
            var format = serializer.LoadFromFile(codecFileName);
            Codec = new GeneralGraphicsCodec(format, pal);

            Setup(genericFileName, "generic");
        }

        public void Setup(string dataFileName, string arrangerName)
        {
            using (var fs = File.Create(dataFileName))
            {
                Random rng = new Random();
                var data = new byte[3 * 16 * 32];
                rng.NextBytes(data);
                fs.Write(data);
            }

            df = new DataFile("df", Path.GetFullPath(dataFileName));

            arranger = new ScatteredArranger(arrangerName, ArrangerLayout.Tiled, 16, 32, 8, 8);

            for (int y = 0; y < arranger.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arranger.ArrangerElementSize.Width; x++)
                {
                    var el = new ArrangerElement
                    {
                        Codec = Codec,
                        DataFile = df,
                        Palette = pal,
                        FileAddress = 24 * x + 24 * x * y,
                        Height = 8,
                        Width = 8,
                        X1 = x * 8,
                        Y1 = y * 8
                    };
                    arranger.ElementGrid[x, y] = el;
                }
            }
        }

        [GlobalCleanup(Target = nameof(DecodeNative))]
        public void GlobalCleanupNative()
        {
            df.Close();
            File.Delete(nativeFileName);
        }

        [GlobalCleanup(Target = nameof(DecodeGeneric))]
        public void GlobalCleanupGeneric()
        {
            df.Close();
            File.Delete(genericFileName);
        }

        [Benchmark(Baseline = true)]
        public void DecodeNative()
        {
            for (int i = 0; i < 100; i++)
            {
                var outputFileName = Path.Combine(outputDirectory, $"Native.{i}.bmp");

                var image = new IndexedImage(arranger);
                image.ExportImage(outputFileName, new ImageSharpFileAdapter());
            }
        }

        [Benchmark]
        public void DecodeGeneric()
        {
            for (int i = 0; i < 100; i++)
            {
                var outputFileName = Path.Combine(outputDirectory, $"Generic.{i}.bmp");

                var image = new IndexedImage(arranger);
                image.ExportImage(outputFileName, new ImageSharpFileAdapter());
            }
        }
    }
}
