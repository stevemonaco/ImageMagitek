using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImageMagitek.Colors.SerializationModels;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
