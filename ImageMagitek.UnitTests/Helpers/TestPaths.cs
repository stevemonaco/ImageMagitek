using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

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

    internal static void AssertNoTransactionFiles(string directory)
    {
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.bak", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(directory, "_transaction.json", SearchOption.AllDirectories));
    }

    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
