using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ImageMagitek.UnitTests;

public static class TestPaths
{
    /// <summary>
    /// The shipped XML codec definitions in the source tree, which include codecs the ImageMagitek build does not copy to output.
    /// </summary>
    public static string CodecsPath => Path.GetFullPath(Path.Combine(ThisDir(), "..", "..", "ImageMagitek", "_codecs"));

    public static string CreateTempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"imagemagitek-{Guid.NewGuid():N}{extension}");

    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
