using System.Text.Json;
using ImageMagitek.Colors.SerializationModels;

namespace ImageMagitek.Colors
{
    public static class PaletteJsonSerializer
    {
        public static Palette ReadPalette(string json)
        {
            var options = new JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            var model = JsonSerializer.Deserialize<PaletteJsonModel>(json, options);
            return model.ToPalette();
        }
    }
}
