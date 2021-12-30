﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Project;

namespace ImageMagitek;

public abstract class DataSource : IProjectResource, IDisposable
{
    public string Name { get; set; }
    public bool CanContainChildResources => false;
    public bool ShouldBeSerialized { get; set; } = true;

    public Stream Stream => _stream.Value;

    protected Lazy<Stream> _stream;
    private bool disposedValue;

    public DataSource(string name)
    {
        Name = name;
    }

    public virtual byte[] ReadUnshifted(BitAddress address, int readBits) =>
        _stream.Value.ReadUnshifted(address, readBits);

    public virtual void ReadUnshifted(BitAddress address, int readBits, Span<byte> buffer) =>
        _stream.Value.ReadUnshifted(address, readBits, buffer);

    public virtual void Write(BitAddress address, ReadOnlySpan<byte> buffer)
    {
        _stream.Value.Seek(address.ByteOffset, SeekOrigin.Begin);
        _stream.Value.Write(buffer);
    }

    public virtual void Write(ReadOnlySpan<byte> buffer) => 
        _stream.Value.Write(buffer);

    public virtual void Flush() => 
        _stream.Value.Flush();

    public virtual IEnumerable<IProjectResource> LinkedResources => 
        Enumerable.Empty<IProjectResource>();

    public virtual bool UnlinkResource(IProjectResource resource) => false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (!_stream.IsValueCreated && _stream.Value is not null)
                    _stream.Value.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}