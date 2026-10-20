using System.Collections.Generic;

namespace ImageMagitek.Project;

/// <param name="LostTilePalette">A tilemap whose tile palette arranger is removed; it falls back to the generated tile palette</param>
public sealed record ResourceChange(ResourceNode ResourceNode, string ResourcePath, bool Removed, bool LostPalette, bool LostElement,
    bool LostTilePalette = false)
{
    public IProjectResource Resource => ResourceNode.Item;
    public string ResourceName => Resource.Name;
    public bool IsChanged => LostPalette || LostElement || LostTilePalette;
}

/// <summary>
/// The resources in <paramref name="Tree"/> removed or changed by deleting a resource node
/// </summary>
public sealed record ResourceDeletionPlan(ProjectTree Tree, IReadOnlyList<ResourceChange> Changes);
