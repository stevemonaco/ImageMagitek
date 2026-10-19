using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Plugins;

namespace ImageMagitek.Compression;

public interface ICompressorFactory
{
    /// <summary>
    /// Registers a compression plugin type under its <see cref="CompressionInfo.Name"/> after validating it
    /// </summary>
    /// <returns>The registered name, or a failure naming the type and the reason it was refused</returns>
    MagitekResult<string> AddCompressionPlugin([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pluginType);

    /// <summary>
    /// Creates a new instance of the named compressor
    /// </summary>
    /// <returns>The compressor, or null when no compressor has that name</returns>
    ICompressionPlugin? CreateCompressor(string name);

    /// <summary>
    /// Describes each registered compressor, built-in ones first
    /// </summary>
    IReadOnlyList<CompressionInfo> GetRegisteredCompressors();
}
