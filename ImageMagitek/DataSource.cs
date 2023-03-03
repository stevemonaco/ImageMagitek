using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Project;

namespace ImageMagitek;

public abstract class DataSource : IProjectResource, IDisposable
{
    public string Name { get; set; }
    public bool CanContainChildResources => false;
    public bool ShouldBeSerialized { get; set; } = true;
    public virtual long Length => Stream.Value.Length;

    protected abstract Lazy<Stream> Stream { get; }
    private bool _disposedValue;

    public DataSource(string name)
    {
        Name = name;
    }

    public virtual byte[] Read(BitAddress address, int readBits) =>
        Stream.Value.ReadUnshifted(address, readBits);

    public virtual void Read(BitAddress address, int readBits, Span<byte> buffer) =>
        Stream.Value.ReadUnshifted(address, readBits, buffer);

    public virtual async ValueTask<byte[]> ReadAsync(BitAddress address, int readBits) =>
        await Stream.Value.ReadUnshiftedAsync(address, readBits);

    public virtual async ValueTask ReadAsync(BitAddress address, int readBits, Memory<byte> buffer) =>
        await Stream.Value.ReadUnshiftedAsync(address, readBits, buffer);

    public virtual void Write(ReadOnlySpan<byte> buffer) =>
        Stream.Value.Write(buffer);

    public virtual void Write(BitAddress address, ReadOnlySpan<byte> buffer) =>
        Stream.Value.WriteUnshifted(address, buffer.Length * 8, buffer);

    public virtual void Write(BitAddress address, int writeBits, ReadOnlySpan<byte> buffer) =>
        Stream.Value.WriteUnshifted(address, writeBits, buffer);

    public virtual async Task WriteAsync(ReadOnlyMemory<byte> buffer) =>
        await Stream.Value.WriteAsync(buffer);

    public virtual async Task WriteAsync(BitAddress address, ReadOnlyMemory<byte> buffer) =>
        await Stream.Value.WriteUnshiftedAsync(address, buffer.Length * 8, buffer);

    public virtual async Task WriteAsync(BitAddress address, int writeBits, ReadOnlyMemory<byte> buffer) =>
        await Stream.Value.WriteUnshiftedAsync(address, writeBits, buffer);

    public virtual void Seek(long offset, SeekOrigin origin) => 
        Stream.Value.Seek(offset, origin);

    public virtual void Flush() => 
        Stream.Value.Flush();

    public virtual async Task FlushAsync() =>
        await Stream.Value.FlushAsync();

    public virtual IEnumerable<IProjectResource> LinkedResources => 
        Enumerable.Empty<IProjectResource>();

    public virtual bool UnlinkResource(IProjectResource resource) => false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (Stream.IsValueCreated && Stream.Value is not null)
                    Stream.Value.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
