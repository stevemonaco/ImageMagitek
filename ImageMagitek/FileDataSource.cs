using System;
using System.IO;

namespace ImageMagitek;

public sealed class FileDataSource : DataSource
{
    public string FileLocation { get; private set; }

    public FileDataSource(string name, string fileLocation) : base(name)
    {
        FileLocation = fileLocation;

        _stream = new Lazy<Stream>(() =>
        {
            if (string.IsNullOrWhiteSpace(FileLocation))
                throw new ArgumentException($"{nameof(DataSource)} parameter {nameof(FileLocation)} was null or empty");

            return File.Open(FileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        });
    }
}
