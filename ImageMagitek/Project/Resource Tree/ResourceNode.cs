using ImageMagitek.Project.Serialization;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project;

public abstract class ResourceNode : PathNodeBase<ResourceNode, IProjectResource>
{
    public string? DiskLocation { get; set; }
    public required ResourceModel Model { get; init; }

    public ResourceNode(string nodeName, IProjectResource resource) :
        base(nodeName, resource)
    {
    }

    public override void Rename(string name)
    {
        base.Rename(name);
        Item.Name = name;
    }
}

#pragma warning disable CS0108
public abstract class ResourceNode<TModel> : ResourceNode
    where TModel : ResourceModel
{
    public ResourceNode(string nodeName, IProjectResource resource) : base(nodeName, resource)
    {
    }

    /// <summary>
    /// Representation of the Model that is currently persisted
    /// </summary>
    public TModel Model
    {
        get => (TModel)base.Model;
        set => base.Model = value;
    }
}
