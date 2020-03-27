using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;

namespace TileShop.Shared.Services
{
    public interface IPaletteService
    {
        Palette DefaultPalette { get; set; }
        List<Palette> Palettes { get; set; }

        void LoadJsonPalettes(string palettesPath);
    }

    public class PaletteService : IPaletteService
    {
        public Palette DefaultPalette { get; set; }
        public List<Palette> Palettes { get; set; } = new List<Palette>();

        public void LoadJsonPalettes(string palettesPath)
        {
            if (!Directory.Exists(palettesPath))
                throw new DirectoryNotFoundException($"{nameof(LoadJsonPalettes)}: Could not locate directory {palettesPath}");

            foreach (var paletteFileName in Directory.GetFiles(palettesPath).Where(x => x.EndsWith(".json")))
            {
                string json = File.ReadAllText(paletteFileName);
                var pal = PaletteJsonSerializer.ReadPalette(json);
                Palettes.Add(pal);
            }
        }
    }
}
