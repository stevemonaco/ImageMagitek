using System.Collections.Generic;

namespace ImageMagitek.Project;

/// <param name="LostTilePalette">A tilemap whose tile palette arranger is removed; it falls back to the generated tile palette</param>
/// <param name="LostSlotPalette">A tilemap or metatile set whose slot palette is removed, or a metatile map listing such a set; the slot falls back as LIB-TILEMAPS-023</param>
public sealed record ResourceChange(ResourceNode ResourceNode, string ResourcePath, bool Removed, bool LostPalette, bool LostElement,
    bool LostTilePalette = false, bool LostSlotPalette = false)
{
    public IProjectResource Resource => ResourceNode.Item;
    public string ResourceName => Resource.Name;
    public bool IsChanged => LostPalette || LostElement || LostTilePalette || LostSlotPalette;
}

/// <summary>
/// The resources in <paramref name="Tree"/> removed or changed by deleting a resource node
/// </summary>
public sealed record ResourceDeletionPlan(ProjectTree Tree, IReadOnlyList<ResourceChange> Changes);
