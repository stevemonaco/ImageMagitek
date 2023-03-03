using System;
using System.IO;

namespace ImageMagitek;

public sealed class MemoryDataSource : DataSource
{
    protected override Lazy<Stream> Stream { get; }

    /// <summary>
    /// Creates an in-memory data source with unlimited capacity
    /// </summary>
    /// <param name="name"></param>
    public MemoryDataSource(string name) : base(name)
    {
        ShouldBeSerialized = false;
        Stream = new Lazy<Stream>(() =>
        {
            return new MemoryStream();
        });
    }

    /// <summary>
    /// Creates an in-memory data source with a maximum capacity
    /// </summary>
    /// <param name="name"></param>
    /// <param name="maxDataSize">Maximum size of data in bytes</param>
    public MemoryDataSource(string name, int maxDataSize) : base(name)
    {
        ShouldBeSerialized = false;
        Stream = new Lazy<Stream>(() =>
        {
            return new MemoryStream(new byte[maxDataSize]);
        });
    }
}
