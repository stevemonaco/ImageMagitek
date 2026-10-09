using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ImageMagitek.Services;

public interface IElementLayoutService
{
    MagitekResult<TileLayout> ReadLayout(string layoutFileName);
}

public sealed class ElementLayoutService : IElementLayoutService
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Loads a TileLayout from a JSON file
    /// </summary>
    /// <returns>The layout, or a failure naming the file when it cannot be read or describes an unusable layout</returns>
    public MagitekResult<TileLayout> ReadLayout(string layoutFileName)
    {
        if (!File.Exists(layoutFileName))
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} failed because '{layoutFileName}' does not exist");

        TileLayout? layout;
        try
        {
            var contents = File.ReadAllText(layoutFileName);
            layout = JsonSerializer.Deserialize<TileLayout>(contents, _options);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} failed to read '{layoutFileName}': {ex.Message}");
        }

        if (layout is null)
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} failed to serialize the contents of '{layoutFileName}'");

        if (Validate(layout) is { } reason)
            return new MagitekResult<TileLayout>.Failed($"{nameof(ReadLayout)} found an unusable layout in '{layoutFileName}': {reason}");

        return new MagitekResult<TileLayout>.Success(layout);
    }

    private static string? Validate(TileLayout layout)
    {
        if (layout.Width < 1 || layout.Height < 1)
            return $"width and height must be at least 1, but are {layout.Width} and {layout.Height}";

        var pattern = layout.Pattern.ToList();
        if (pattern.Count == 0)
            return "the pattern is empty";

        var outsideIndex = pattern.FindIndex(p => p.X < 0 || p.Y < 0 || p.X >= layout.Width || p.Y >= layout.Height);
        if (outsideIndex >= 0)
            return $"pattern cell ({pattern[outsideIndex].X}, {pattern[outsideIndex].Y}) is outside the {layout.Width}x{layout.Height} layout";

        if (layout.TilesPerPattern != pattern.Count)
            return $"tilesPerPattern is {layout.TilesPerPattern}, but the pattern has {pattern.Count} cells";

        return null;
    }
}
