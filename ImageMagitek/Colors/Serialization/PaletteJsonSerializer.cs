using System.Text.Json;
using ImageMagitek.Colors.Serialization;

namespace ImageMagitek.Colors;

public static class PaletteJsonSerializer
{
    public static Palette ReadPalette(string json, IColorFactory colorFactory)
    {
        var options = new JsonSerializerOptions();
        options.PropertyNameCaseInsensitive = true;
        var model = JsonSerializer.Deserialize<PaletteJsonModel>(json, options);
        return model.ToPalette(colorFactory);
    }
}
