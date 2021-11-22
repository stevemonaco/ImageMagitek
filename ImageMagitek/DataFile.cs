﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Project;

namespace ImageMagitek;

/// <summary>
/// DataFile manages access to user-modifiable files and memory
/// </summary>
public sealed class DataFile : IProjectResource, IDisposable
{
    public string Location { get; private set; }

    public Stream Stream { get => _stream.Value; }
    public string Name { get; set; }

    public bool CanContainChildResources => false;

    public bool ShouldBeSerialized { get; set; } = true;

    Lazy<Stream> _stream;
    private bool disposedValue;

    /// <summary>
    /// Creates a new DataFile and lazily opens the file at the specified location for ReadWrite access and Read sharing
    /// </summary>
    /// <param name="name">Name associated with DataFile resource</param>
    /// <param name="fileLocation">Location on disk</param>
    /// <exception cref="ArgumentException"></exception>
    public DataFile(string name, string fileLocation)
    {
        Name = name;
        Location = fileLocation;

        _stream = new Lazy<Stream>(() =>
        {
            if (string.IsNullOrWhiteSpace(Location))
                throw new ArgumentException($"{nameof(DataFile)} parameter {nameof(Location)} was null or empty");

            return File.Open(Location, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        });
    }

    /// <summary>
    /// Creates a new DataFile and takes ownership of the provided stream
    /// </summary>
    /// <param name="name">Name associated with DataFile resource</param>
    /// <param name="stream">Stream to take ownership of</param>
    public DataFile(string name, Stream stream)
    {
        Name = name;
        _stream = new Lazy<Stream>(() => stream);
    }

    public bool UnlinkResource(IProjectResource resource) => false;

    public IEnumerable<IProjectResource> LinkedResources
    {
        get
        {
            return Enumerable.Empty<IProjectResource>();
        }
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (Stream is not null)
                    Stream.Close();
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
