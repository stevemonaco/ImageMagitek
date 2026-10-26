using System;
using System.Collections.Generic;
using System.IO;
using ImageMagitek.Compression;
using ImageMagitek.Project;

namespace ImageMagitek;

/// <summary>
/// A read-only data source whose bytes are a compressed block of its parent, decompressed on first access
/// </summary>
public sealed class CompressedDataSource : DataSource
{
    public DataSource Parent { get; }
    public long ParentOffset { get; }
    public string CompressionName { get; }

    public override bool IsReadOnly => true;
    protected override Lazy<Stream> Stream => _stream;

    /// <summary>
    /// The number of parent bytes the block occupies, or 0 before the first access and after a failure
    /// </summary>
    public long CompressedLength
    {
        get
        {
            _ = _stream.Value;
            return _compressedLength;
        }
    }

    /// <inheritdoc/>
    public override FileDataSource? FindMissingDataSource() => Parent is FileDataSource { IsMissing: true } missing ? missing : null;

    /// <inheritdoc/>
    public override string? UnavailableReason => DecompressionError;

    /// <summary>
    /// Why the block could not be decompressed, or null when it was. Decompresses on first access.
    /// </summary>
    public string? DecompressionError
    {
        get
        {
            _ = _stream.Value;
            return _error;
        }
    }

    private readonly ICompressorFactory _compressorFactory;
    private Lazy<Stream> _stream;
    private long _compressedLength;
    private string? _error;

    public CompressedDataSource(string name, DataSource parent, long parentOffset, string compressionName, ICompressorFactory compressorFactory)
        : base(name)
    {
        Parent = parent;
        ParentOffset = parentOffset;
        CompressionName = compressionName;
        _compressorFactory = compressorFactory;
        _stream = CreateStream();

        Parent.DataWritten += OnParentDataWritten;
    }

    /// <summary>
    /// Discards the decompressed bytes and any error, so the next access decompresses again
    /// </summary>
    public void Invalidate() => ReplaceStream(() => _stream = CreateStream());

    public override IEnumerable<ResourceEdge> Dependencies => [new(Parent, ResourceEdgeKind.Cascade)];

    private void OnParentDataWritten(object? sender, EventArgs e)
    {
        Invalidate();
        NotifyDataWritten();
    }

    private Lazy<Stream> CreateStream() => new(() =>
    {
        _compressedLength = 0;
        _error = null;

        try
        {
            var compressor = _compressorFactory.CreateCompressor(CompressionName);
            if (compressor is null)
                return Fail($"Compression '{CompressionName}' is not available. It may come from a plugin that is not installed.");

            if (Parent is FileDataSource { IsMissing: true } missing)
                return Fail($"Data file '{missing.Name}' is missing");

            var available = Parent.Length - ParentOffset;
            if (available <= 0)
                return Fail($"Offset 0x{ParentOffset:X} is past the end of '{Parent.Name}'");

            var inputLength = (int)Math.Min(available, CompressorFactory.MaxInputLength);
            var input = Parent.Read(new BitAddress(ParentOffset, 0), inputLength * 8);
            var result = compressor.Decompress(input);

            _compressedLength = result.ConsumedLength;
            return new MemoryStream(result.Data, writable: false);
        }
        catch (Exception ex)
        {
            return Fail($"'{CompressionName}' could not decompress offset 0x{ParentOffset:X} of '{Parent.Name}': {ex.Message}");
        }
    });

    private MemoryStream Fail(string error)
    {
        _error = error;
        return new MemoryStream([], writable: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Parent.DataWritten -= OnParentDataWritten;

        base.Dispose(disposing);
    }
}
