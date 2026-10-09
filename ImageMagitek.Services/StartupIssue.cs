using System;

namespace ImageMagitek.Services;

/// <summary>
/// A resource file that bootstrapping logged and skipped
/// </summary>
public sealed record StartupIssue(string Path, string Message);

/// <summary>
/// Thrown when an essential resource cannot be loaded during bootstrapping
/// </summary>
public sealed class BootstrapException(string message) : Exception(message);
