using System;
using System.IO;

namespace ImageMagitek.UnitTests;

/// <summary>
/// An in-memory source that, once armed, writes new bytes and raises <see cref="DataSource.DataWritten"/> during its next read, returning the bytes from before the write.
/// </summary>
public sealed class WriteDuringReadDataSource : DataSource
{
    private readonly Lazy<Stream> _stream = new(() => new MemoryStream());
    private byte[]? _pendingWrite;

    protected override Lazy<Stream> Stream => _stream;

    public WriteDuringReadDataSource(string name, byte[] bytes) : base(name)
    {
        Write(bytes);
    }

    public void WriteOnNextRead(byte[] bytes) => _pendingWrite = bytes;

    public override byte[] Read(BitAddress address, int readBits)
    {
        var data = base.Read(address, readBits);

        if (_pendingWrite is { } pending)
        {
            _pendingWrite = null;
            Write(BitAddress.Zero, pending);
            NotifyDataWritten();
        }

        return data;
    }
}
