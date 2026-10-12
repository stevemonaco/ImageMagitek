using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// The project format versions this build writes and reads.
/// </summary>
public static class ProjectFormat
{
    public static Version Current { get; } = new(1, 0);
    public static Version Oldest { get; } = new(0, 9);

    /// <summary>
    /// Parses a <c>major.minor</c> version, independently of the current culture.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out Version? version) =>
        Version.TryParse(text, out version) && version.Build == -1 && text.All(c => c is '.' or (>= '0' and <= '9'));
}
