using System;
using System.Linq;
using ImageMagitek.Project.Serialization;
using Monaco.PathTree;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project;

public abstract class ResourceNode : PathNodeBase<ResourceNode, IProjectResource>
{
    private bool _suppressChildEvents;

    public string? DiskLocation { get; set; }
    public ResourceModel? Model { get; set; }

    internal event EventHandler<ProjectTreeChange>? TreeChanged;

    public ResourceNode(string nodeName, IProjectResource resource) :
        base(nodeName, resource)
    {
    }

    public override void AttachChildNode(ResourceNode node)
    {
        base.AttachChildNode(node);

        if (!_suppressChildEvents)
            Raise(new ProjectTreeChange(ProjectTreeChangeKind.Added, node, this));
    }

    public override ResourceNode DetachChildNode(string childName)
    {
        var child = base.DetachChildNode(childName);

        if (!_suppressChildEvents)
            Raise(new ProjectTreeChange(ProjectTreeChangeKind.Removed, child, this));

        return child;
    }

    public override void RemoveChildNode(string childName)
    {
        TryGetChildNode(childName, out var child);
        base.RemoveChildNode(childName);

        // The base leaves Parent set, which would make the removed node still look attached
        child!.Parent = null;

        if (!_suppressChildEvents)
            Raise(new ProjectTreeChange(ProjectTreeChangeKind.Removed, child, this));
    }

    public override void Rename(string name)
    {
        var oldName = Name;

        // The base renames through the parent's Detach/Attach, which must not surface as Removed+Added
        WithChildEventsSuppressed(Parent, null, () => base.Rename(name));

        Item.Name = name;
        Raise(new ProjectTreeChange(ProjectTreeChangeKind.Renamed, this, Parent, OldName: oldName));
    }

    /// <summary>
    /// Moves this node under <paramref name="newParent"/>, raising a single Moved change
    /// </summary>
    public void MoveTo(ResourceNode newParent)
    {
        var oldParent = Parent;

        WithChildEventsSuppressed(oldParent, newParent, () =>
        {
            oldParent?.DetachChildNode(Name);
            newParent.AttachChildNode(this);
        });

        Raise(new ProjectTreeChange(ProjectTreeChangeKind.Moved, this, newParent, oldParent));
    }

    private static void WithChildEventsSuppressed(ResourceNode? a, ResourceNode? b, Action action)
    {
        a?._suppressChildEvents = true;
        b?._suppressChildEvents = true;

        try
        {
            action();
        }
        finally
        {
            a?._suppressChildEvents = false;
            b?._suppressChildEvents = false;
        }
    }

    private void Raise(ProjectTreeChange change)
    {
        var root = this.SelfAndAncestors<ResourceNode, IProjectResource>().Last();
        root.TreeChanged?.Invoke(root, change);
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
    /// Representation of the Model that is currently persisted on disk
    /// Used to keep track of stale state
    /// </summary>
    public TModel? Model
    {
        get => (TModel?)base.Model;
        set => base.Model = value;
    }
}
