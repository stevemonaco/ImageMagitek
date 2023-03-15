using System.Text.Json;
using ImageMagitek.Colors.Serialization;

namespace ImageMagitek.Colors.Serialization;

public static class PaletteJsonSerializer
{
    public static Palette? DeserializePalette(string json, IColorFactory colorFactory)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var model = JsonSerializer.Deserialize<PaletteJsonModel>(json, options);
        return model?.ToPalette(colorFactory);
    }
}
