using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Colors.Serialization;
public sealed class ColorRgba32JsonConverter : JsonConverter<ColorRgba32>
{
    public override ColorRgba32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var colorString = reader.GetString();

        if (colorString is null)
            throw new JsonException($"{nameof(Read)}: Color string contained null content");

        if (ColorParser.TryParse(colorString, ColorModel.Rgba32, out var color))
            return (ColorRgba32)color;
        else
            throw new JsonException($"Unable to convert '{colorString}' to '{nameof(ColorRgba32)}'");
    }

    public override void Write(Utf8JsonWriter writer, ColorRgba32 value, JsonSerializerOptions options)
    {
        string content = $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}";
        writer.WriteStringValue(content);
    }
}
