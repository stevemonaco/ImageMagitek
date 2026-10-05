namespace ImageMagitek.Project;

public enum ProjectTreeChangeKind { Added, Removed, Moved, Renamed }

/// <summary>
/// Describes a structural change to a project tree, raised after the change is applied in memory
/// </summary>
/// <param name="Parent">Parent after the change, or the former parent for <see cref="ProjectTreeChangeKind.Removed"/></param>
/// <param name="OldParent">Parent before a <see cref="ProjectTreeChangeKind.Moved"/></param>
/// <param name="OldName">Name before a <see cref="ProjectTreeChangeKind.Renamed"/></param>
public sealed record ProjectTreeChange(ProjectTreeChangeKind Kind, ResourceNode Node, ResourceNode? Parent, ResourceNode? OldParent = null, string? OldName = null);
