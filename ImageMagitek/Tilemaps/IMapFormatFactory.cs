using System.Collections.Generic;

namespace ImageMagitek.Tilemaps;

public interface IMapFormatFactory
{
    /// <summary>
    /// Registers a map format under its name
    /// </summary>
    /// <returns>A failure, keeping the first registration, when the name is already registered (case-insensitive)</returns>
    MagitekResult AddFormat(MapFormat format);

    /// <summary>
    /// The registered format with this name (case-insensitive), or null
    /// </summary>
    MapFormat? Find(string name);

    /// <summary>
    /// Registered formats in registration order
    /// </summary>
    IReadOnlyList<MapFormat> Formats { get; }
}
