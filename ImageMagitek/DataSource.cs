using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    private readonly SemaphoreSlim _streamSemaphore = new(1, 1);
    private bool _disposedValue;

    public DataSource(string name)
    {
        Name = name;
    }

    public virtual byte[] Read(BitAddress address, int readBits)
    {
        _streamSemaphore.Wait();
        try
        {
            return Stream.Value.ReadUnshifted(address, readBits);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Read(BitAddress address, int readBits, Span<byte> buffer)
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.ReadUnshifted(address, readBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async ValueTask<byte[]> ReadAsync(BitAddress address, int readBits)
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            return await Stream.Value.ReadUnshiftedAsync(address, readBits);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async ValueTask ReadAsync(BitAddress address, int readBits, Memory<byte> buffer)
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.ReadUnshiftedAsync(address, readBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Write(ReadOnlySpan<byte> buffer)
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.Write(buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Write(BitAddress address, ReadOnlySpan<byte> buffer)
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.WriteUnshifted(address, buffer.Length * 8, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Write(BitAddress address, int writeBits, ReadOnlySpan<byte> buffer)
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.WriteUnshifted(address, writeBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task WriteAsync(ReadOnlyMemory<byte> buffer)
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.WriteAsync(buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task WriteAsync(BitAddress address, ReadOnlyMemory<byte> buffer)
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.WriteUnshiftedAsync(address, buffer.Length * 8, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task WriteAsync(BitAddress address, int writeBits, ReadOnlyMemory<byte> buffer)
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.WriteUnshiftedAsync(address, writeBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Seek(long offset, SeekOrigin origin)
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.Seek(offset, origin);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Flush()
    {
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.Flush();
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task FlushAsync()
    {
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.FlushAsync();
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

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

                _streamSemaphore.Dispose();
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
