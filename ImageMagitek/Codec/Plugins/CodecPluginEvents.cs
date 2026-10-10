using System;

namespace ImageMagitek.Codec;

/// <summary>
/// Events raised by plugin codec adapters, for hosts to log or show.
/// </summary>
public static class CodecPluginEvents
{
    /// <summary>
    /// Raised with the codec name and exception the first time a plugin adapter's decode throws. May be raised on any thread.
    /// </summary>
    public static event Action<string, Exception>? DecodeFailed;

    internal static void RaiseDecodeFailed(string codecName, Exception exception) =>
        DecodeFailed?.Invoke(codecName, exception);
}
