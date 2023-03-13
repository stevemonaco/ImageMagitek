using System.IO;
using System.Text.Json;

namespace ImageMagitek.Services;

public interface IElementLayoutService
{
    MagitekResult<TileLayout> ReadLayout(string layoutFileName);
}

public sealed class ElementLayoutService : IElementLayoutService
{
    /// <summary>
    /// Loads a TileLayout from a JSON file
    /// </summary>
    /// <param name="layoutFileName"></param>
    /// <returns></returns>
    public MagitekResult<TileLayout> ReadLayout(string layoutFileName)
    {
        if (!File.Exists(layoutFileName))
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} failed because '{layoutFileName}' does not exist");

        var contents = File.ReadAllText(layoutFileName);
        var layout = JsonSerializer.Deserialize<TileLayout>(contents, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        if (layout is null)
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} failed to serialize the contents of '{layoutFileName}'");

        return new MagitekResult<TileLayout>.Success(layout);
    }
}
