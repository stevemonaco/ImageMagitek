namespace ImageMagitek.Project;

public sealed class ResourceChange
{
    public IProjectResource Resource { get; }
    public ResourceNode ResourceNode { get; }

    public string ResourceName { get; set; }
    public string ResourcePath { get; set; }

    public bool Removed { get; set; }
    public bool LostPalette { get; set; }
    public bool LostElement { get; set; }
    public bool IsChanged { get; set; }

    public ResourceChange(ResourceNode resourceNode, string resourcePathKey, bool removed, bool lostPalette, bool lostElement)
    {
        ResourceNode = resourceNode;
        Resource = resourceNode.Item;
        ResourceName = Resource.Name;
        ResourcePath = resourcePathKey;
        Removed = removed;
        LostPalette = lostPalette;
        LostElement = lostElement;
        IsChanged = LostPalette || LostElement;
    }
}
