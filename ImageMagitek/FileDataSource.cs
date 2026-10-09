using System;
using System.IO;

namespace ImageMagitek;

public sealed class FileDataSource : DataSource
{
    private const int ErrorWriteProtect = unchecked((int)0x80070013);

    public string FileLocation { get; private set; }
    protected override Lazy<Stream> Stream => _stream;

    private Lazy<Stream> _stream;
    private bool _isReadOnly;

    public bool IsMissing => !File.Exists(FileLocation);

    /// <summary>
    /// True when the file could only be opened for reading. Opens the file if needed; false while it is missing or its open failed.
    /// </summary>
    public override bool IsReadOnly
    {
        get
        {
            if (!_stream.IsValueCreated)
            {
                if (IsMissing)
                    return false;

                try
                {
                    _ = _stream.Value;
                }
                catch
                {
                    return false;
                }
            }

            return _isReadOnly;
        }
    }

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

        try
        {
            var stream = File.Open(FileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            _isReadOnly = false;
            return stream;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || (ex is IOException && ex.HResult == ErrorWriteProtect))
        {
            var stream = File.Open(FileLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
            _isReadOnly = true;
            return stream;
        }
    });
}
