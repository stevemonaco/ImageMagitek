using System;
using System.IO;

namespace ImageMagitek;

public sealed class FileDataSource : DataSource
{
    public string FileLocation { get; private set; }
    protected override Lazy<Stream> Stream => _stream;

    private Lazy<Stream> _stream;

    public bool IsMissing => !File.Exists(FileLocation);

    public FileDataSource(string name, string fileLocation) : base(name)
    {
        FileLocation = fileLocation;
        _stream = CreateStream();
    }

    /// <summary>
    /// Discards the cached stream, including a failed open of a missing file, so the next access opens the file again
    /// </summary>
    public void Reopen()
    {
        if (_stream.IsValueCreated)
            _stream.Value.Dispose();

        _stream = CreateStream();
    }

    private Lazy<Stream> CreateStream() => new(() =>
    {
        if (string.IsNullOrWhiteSpace(FileLocation))
            throw new ArgumentException($"{nameof(DataSource)} parameter {nameof(FileLocation)} was null or empty");

        return File.Open(FileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    });
}
