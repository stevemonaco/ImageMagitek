using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageMagitek.Codec;

/// <summary>
/// Manages the creation of codec types
/// </summary>
public interface ICodecFactory
{
    MagitekResult<string> AddCodecPlugin(Type pluginType);
    MagitekResult AddFormat(IGraphicsFormat format);
    IGraphicsCodec? CreateCodec(string codecName, Size? elementSize = default);
    IGraphicsCodec CloneCodec(IGraphicsCodec codec);
    IEnumerable<string> GetRegisteredCodecNames();
}
