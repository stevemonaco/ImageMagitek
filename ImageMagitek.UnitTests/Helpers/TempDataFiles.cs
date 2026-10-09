using System;
using System.Collections.Generic;
using System.IO;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Creates temp data files and the sources opened on them, and deletes both on dispose, clearing any read-only attribute.
/// </summary>
public sealed class TempDataFiles : IDisposable
{
    private readonly List<string> _paths = [];
    private readonly List<FileDataSource> _sources = [];

    public string Create(byte[] bytes, bool isReadOnly = true)
    {
        var path = TestPaths.CreateTempPath(".bin");
        _paths.Add(path);
        File.WriteAllBytes(path, bytes);

        if (isReadOnly)
            File.SetAttributes(path, FileAttributes.ReadOnly);

        return path;
    }

    public FileDataSource Open(string name, string path)
    {
        var source = new FileDataSource(name, path);
        _sources.Add(source);
        return source;
    }

    public FileDataSource Open(string name, byte[] bytes, bool isReadOnly = true) => Open(name, Create(bytes, isReadOnly));

    public void Dispose()
    {
        foreach (var source in _sources)
            source.Dispose();

        foreach (var path in _paths)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
    }
}
