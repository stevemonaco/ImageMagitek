using System.IO;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Serialization;

namespace ImageMagitek.Services;

public interface IPaletteService
{
    Palette? ReadJsonPalette(string paletteFileName);
}

public sealed class PaletteService : IPaletteService
{
    private readonly IColorFactory _colorFactory;

    public PaletteService(IColorFactory colorFactory)
    {
        _colorFactory = colorFactory;
    }

    /// <summary>
    /// Read a palette from a JSON file
    /// </summary>
    /// <param name="paletteFileName">Path to the JSON palette file</param>
    /// <param name="colorFactory">Factory to initialize colors with</param>
    public Palette? ReadJsonPalette(string paletteFileName)
    {
        if (!File.Exists(paletteFileName))
            throw new FileNotFoundException($"{nameof(ReadJsonPalette)}: Could not locate file {paletteFileName}");

        string json = File.ReadAllText(paletteFileName);
        var pal = PaletteJsonSerializer.DeserializePalette(json, _colorFactory);
        return pal;
    }
}
