using System.Text.Json;
using System.Collections.Generic;
using System.IO;

namespace ImageMagitek.Services
{
    public interface ITileLayoutService
    {
        Dictionary<string, ElementLayout> TileLayouts { get; }
        ElementLayout DefaultTileLayout { get; set; }

        MagitekResult LoadLayout(string layoutFileName);
    }

    public class TileLayoutService : ITileLayoutService
    {
        public ElementLayout DefaultTileLayout { get; set; }
        public Dictionary<string, ElementLayout> TileLayouts { get; } = new();

        /// <summary>
        /// Loads a TileLayout from a JSON file
        /// </summary>
        /// <param name="layoutFileName"></param>
        /// <returns></returns>
        public MagitekResult LoadLayout(string layoutFileName)
        {
            if (!File.Exists(layoutFileName))
                return new MagitekResult.Failed($"{nameof(LoadLayout)} failed because '{layoutFileName}' does not exist");

            var contents = File.ReadAllText(layoutFileName);
            var layout = JsonSerializer.Deserialize<ElementLayout>(contents, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            if (TileLayouts.ContainsKey(layout.Name))
                return new MagitekResult.Failed($"{nameof(LoadLayout)} failed because a layout with name '{layout.Name}' already exists");

            TileLayouts.Add(layout.Name, layout);
            return MagitekResult.SuccessResult;
        }
    }
}
