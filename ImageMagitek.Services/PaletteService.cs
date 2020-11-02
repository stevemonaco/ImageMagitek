using System.Collections.Generic;
using System.IO;
using ImageMagitek.Colors;

namespace ImageMagitek.Services
{
    public interface IPaletteService
    {
        IColorFactory ColorFactory { get; }

        Palette DefaultPalette { get; }
        List<Palette> GlobalPalettes { get; }
        Palette NesPalette { get; }

        Palette ReadJsonPalette(string paletteFileName);
        void SetDefaultPalette(Palette pal);
    }

    public class PaletteService : IPaletteService
    {
        public IColorFactory ColorFactory { get; private set; }

        public Palette DefaultPalette { get; private set; }
        public List<Palette> GlobalPalettes { get; } = new List<Palette>();
        public Palette NesPalette { get; private set; }

        public PaletteService(IColorFactory colorFactory)
        {
            ColorFactory = colorFactory;
        }

        /// <summary>
        /// Read a palette from a JSON file
        /// </summary>
        /// <param name="paletteFileName">Path to the JSON palette file</param>
        public Palette ReadJsonPalette(string paletteFileName)
        {
            if (!File.Exists(paletteFileName))
                throw new FileNotFoundException($"{nameof(ReadJsonPalette)}: Could not locate file {paletteFileName}");

            string json = File.ReadAllText(paletteFileName);
            var pal = PaletteJsonSerializer.ReadPalette(json, ColorFactory);
            return pal;
        }

        /// <summary>
        /// Sets the provided palette to the DefaultPalette and adds it to DefaultPalettes if not already present
        /// </summary>
        /// <param name="pal"></param>
        public void SetDefaultPalette(Palette pal)
        {
            if (!GlobalPalettes.Contains(pal))
                GlobalPalettes.Add(pal);

            DefaultPalette = pal;
        }

        /// <summary>
        /// Sets the NES Palette
        /// </summary>
        /// <param name="nesPalette"></param>
        public void SetNesPalette(Palette nesPalette)
        {
            NesPalette = nesPalette;
        }
    }
}
