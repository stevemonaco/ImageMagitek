using System;
using System.IO;

namespace ImageMagitek;

public sealed class FileDataSource : DataSource
{
    public string FileLocation { get; private set; }
    protected override Lazy<Stream> Stream { get; }

    public FileDataSource(string name, string fileLocation) : base(name)
    {
        FileLocation = fileLocation;

        Stream = new Lazy<Stream>(() =>
        {
            if (string.IsNullOrWhiteSpace(FileLocation))
                throw new ArgumentException($"{nameof(DataSource)} parameter {nameof(FileLocation)} was null or empty");

            return File.Open(FileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        });
    }
}
