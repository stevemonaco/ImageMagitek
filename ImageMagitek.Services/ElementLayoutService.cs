using System.Text.Json;
using System.Collections.Generic;
using System.IO;

namespace ImageMagitek.Services
{
    public interface IElementLayoutService
    {
        Dictionary<string, ElementLayout> ElementLayouts { get; }
        ElementLayout DefaultElementLayout { get; set; }

        MagitekResult LoadLayout(string layoutFileName);
    }

    public class ElementLayoutService : IElementLayoutService
    {
        public ElementLayout DefaultElementLayout { get; set; } = ElementLayout.Default;
        public Dictionary<string, ElementLayout> ElementLayouts { get; } = new() { { "Default", ElementLayout.Default } };

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

            if (ElementLayouts.ContainsKey(layout.Name))
                return new MagitekResult.Failed($"{nameof(LoadLayout)} failed because a layout with name '{layout.Name}' already exists");

            ElementLayouts.Add(layout.Name, layout);
            return MagitekResult.SuccessResult;
        }
    }
}
