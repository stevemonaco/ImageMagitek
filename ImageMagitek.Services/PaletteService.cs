using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Services
{
    public interface IPaletteService
    {
        Palette DefaultPalette { get; }
        List<Palette> GlobalPalettes { get; }
        Palette NesPalette { get; }

        void LoadGlobalPalette(string paletteFileName);
        void SetDefaultPalette(Palette pal);
        void LoadNesPalette(string nesPaletteFileName);
    }

    public class PaletteService : IPaletteService
    {
        public Palette DefaultPalette { get; private set; }
        public List<Palette> GlobalPalettes { get; } = new List<Palette>();
        public Palette NesPalette { get; private set; }

        /// <summary>
        /// Loads a palette from a JSON file and adds it to GlobalPalettes
        /// </summary>
        /// <param name="paletteFileName"></param>
        public void LoadGlobalPalette(string paletteFileName)
        {
            if (!File.Exists(paletteFileName))
                throw new FileNotFoundException($"{nameof(LoadGlobalPalette)}: Could not locate file {paletteFileName}");

            string json = File.ReadAllText(paletteFileName);
            var pal = PaletteJsonSerializer.ReadPalette(json);
            GlobalPalettes.Add(pal);

            if (GlobalPalettes.Count == 1)
                DefaultPalette = GlobalPalettes.First();
        }

        /// <summary>
        /// Loads a palette from a JSON file and sets it as the NesPalette
        /// </summary>
        /// <param name="nesPaletteFileName"></param>
        public void LoadNesPalette(string nesPaletteFileName)
        {
            if (!File.Exists(nesPaletteFileName))
                throw new FileNotFoundException($"{nameof(LoadNesPalette)}: Could not locate file {nesPaletteFileName}");

            NesPalette = LoadJsonPalette(nesPaletteFileName);
        }

        private Palette LoadJsonPalette(string paletteFileName)
        {
            string json = File.ReadAllText(paletteFileName);
            return PaletteJsonSerializer.ReadPalette(json);
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
    }
}
