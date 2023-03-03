using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageMagitek.Codec;

public interface ICodecFactory
{
    void AddOrUpdateCodec(Type codecType);
    IGraphicsCodec? GetCodec(string codecName, Size? elementSize = default);
    IGraphicsCodec? CloneCodec(IGraphicsCodec codec);
    IEnumerable<string> GetSupportedCodecNames();
}
