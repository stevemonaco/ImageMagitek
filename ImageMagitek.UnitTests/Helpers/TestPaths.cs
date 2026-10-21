using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ImageMagitek.UnitTests;

public static class TestPaths
{
    /// <summary>
    /// The shipped XML codec definitions in the source tree.
    /// </summary>
    public static string CodecsPath => Path.GetFullPath(Path.Combine(ThisDir(), "..", "..", "ImageMagitek", "_codecs"));

    /// <summary>
    /// The welcome screen's shipped demo files in the source tree.
    /// </summary>
    public static string DemoDirectory => Path.GetFullPath(Path.Combine(ThisDir(), "..", "..", "TileShop.UI", "_demo"));

    /// <summary>
    /// The shipped table definitions and charmaps in the source tree.
    /// </summary>
    public static string TablesPath => Path.GetFullPath(Path.Combine(ThisDir(), "..", "..", "Samples", "Tables"));

    public static string CreateTempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"imagemagitek-{Guid.NewGuid():N}{extension}");

    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
