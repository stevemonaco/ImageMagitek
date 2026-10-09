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

    /// <summary>
    /// True when the source cannot be written; every write overload then throws and <see cref="Flush"/> does nothing
    /// </summary>
    public virtual bool IsReadOnly => false;

    protected abstract Lazy<Stream> Stream { get; }
    private readonly SemaphoreSlim _streamSemaphore = new(1, 1);
    private bool _disposedValue;

    /// <summary>
    /// Raised after an image save writes graphics data to this source
    /// </summary>
    public event EventHandler? DataWritten;

    public DataSource(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Raises <see cref="DataWritten"/>. Not raised from <see cref="Flush"/>, because palette saves also flush and must not reload graphics editors.
    /// </summary>
    public void NotifyDataWritten() => DataWritten?.Invoke(this, EventArgs.Empty);

    public virtual byte[] Read(BitAddress address, int readBits)
    {
        _streamSemaphore.Wait();
        try
        {
            return Stream.Value.ReadShifted(address, readBits);
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
            Stream.Value.ReadShifted(address, readBits, buffer);
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
            return await Stream.Value.ReadShiftedAsync(address, readBits);
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
            await Stream.Value.ReadShiftedAsync(address, readBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfReadOnly();
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
        ThrowIfReadOnly();
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.WriteShifted(address, buffer.Length * 8, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual void Write(BitAddress address, int writeBits, ReadOnlySpan<byte> buffer)
    {
        ThrowIfReadOnly();
        _streamSemaphore.Wait();
        try
        {
            Stream.Value.WriteShifted(address, writeBits, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task WriteAsync(ReadOnlyMemory<byte> buffer)
    {
        ThrowIfReadOnly();
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
        ThrowIfReadOnly();
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.WriteShiftedAsync(address, buffer.Length * 8, buffer);
        }
        finally
        {
            _streamSemaphore.Release();
        }
    }

    public virtual async Task WriteAsync(BitAddress address, int writeBits, ReadOnlyMemory<byte> buffer)
    {
        ThrowIfReadOnly();
        await _streamSemaphore.WaitAsync();
        try
        {
            await Stream.Value.WriteShiftedAsync(address, writeBits, buffer);
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
        if (IsReadOnly)
            return;

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
        if (IsReadOnly)
            return;

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

    private void ThrowIfReadOnly()
    {
        if (IsReadOnly)
            throw new InvalidOperationException($"Data source '{Name}' is read-only");
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
