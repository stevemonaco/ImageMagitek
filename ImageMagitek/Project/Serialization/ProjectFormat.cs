using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// The project format versions this build writes and reads.
/// </summary>
public static class ProjectFormat
{
    public static Version Current { get; } = new(1, 1);
    public static Version Oldest { get; } = new(0, 9);

    /// <summary>
    /// The version written for a project that uses no feature newer than 1.0
    /// </summary>
    public static Version Minimum { get; } = new(1, 0);

    /// <summary>
    /// The lowest version that can hold the project: the highest minimum version of any resource kind in it, and at least <see cref="Minimum"/>
    /// </summary>
    public static Version Required(ProjectTree tree, ResourceKindRegistry kinds) =>
        tree.EnumerateDepthFirst().Select(kinds.FindByNode).OfType<ResourceKind>().Select(x => x.MinimumVersion).Append(Minimum).Max()!;

    /// <summary>
    /// Parses a <c>major.minor</c> version, independently of the current culture.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out Version? version) =>
        Version.TryParse(text, out version) && version.Build == -1 && text.All(c => c is '.' or (>= '0' and <= '9'));
}
