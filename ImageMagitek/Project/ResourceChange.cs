using System.Collections.Generic;

namespace ImageMagitek.Project;

public sealed record ResourceChange(ResourceNode ResourceNode, string ResourcePath, bool Removed, bool LostPalette, bool LostElement)
{
    public IProjectResource Resource => ResourceNode.Item;
    public string ResourceName => Resource.Name;
    public bool IsChanged => LostPalette || LostElement;
}

/// <summary>
/// The resources in <paramref name="Tree"/> removed or changed by deleting a resource node
/// </summary>
public sealed record ResourceDeletionPlan(ProjectTree Tree, IReadOnlyList<ResourceChange> Changes);
