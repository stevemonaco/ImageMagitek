namespace ImageMagitek.Project;

public enum ProjectTreeChangeKind { Added, Removed, Moved, Renamed, Replaced }

/// <summary>
/// Describes a structural change to a project tree, raised after the change is applied in memory
/// </summary>
/// <param name="Node">The changed node, or the new node for <see cref="ProjectTreeChangeKind.Replaced"/></param>
/// <param name="Parent">Parent after the change, or the former parent for <see cref="ProjectTreeChangeKind.Removed"/></param>
/// <param name="OldParent">Parent before a <see cref="ProjectTreeChangeKind.Moved"/>, or the old node's former parent for <see cref="ProjectTreeChangeKind.Replaced"/></param>
/// <param name="OldName">Name before a <see cref="ProjectTreeChangeKind.Renamed"/></param>
/// <param name="OldNode">The detached node a <see cref="ProjectTreeChangeKind.Replaced"/> swapped out</param>
public sealed record ProjectTreeChange(ProjectTreeChangeKind Kind, ResourceNode Node, ResourceNode? Parent, ResourceNode? OldParent = null, string? OldName = null,
    ResourceNode? OldNode = null);
