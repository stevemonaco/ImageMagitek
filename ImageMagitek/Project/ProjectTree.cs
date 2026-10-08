using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ImageMagitek.Colors;
using Monaco.PathTree;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project;

public sealed class ProjectTree : PathTreeBase<ResourceNode, IProjectResource>
{
    private readonly Dictionary<IProjectResource, ResourceNode> _index = new(ReferenceEqualityComparer.Instance);

    public ImageProject Project => (ImageProject) Root.Item;
    public string Name => Root.Item.Name;

    /// <summary>
    /// Raised after a node in the tree is added, removed, moved or renamed
    /// </summary>
    public event EventHandler<ProjectTreeChange>? Changed;

    /// <summary>
    /// Raised when a palette in the tree changes or graphics data is written to a data source in the tree
    /// </summary>
    public event EventHandler<IProjectResource>? ResourceChanged;

    public ProjectTree(ProjectNode root) :
        base(root)
    {
        if (root.Item is not ImageProject)
            throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid root type '{root.GetType()}'");

        ExcludeRootFromPath = true;

        Index(root);
        root.TreeChanged += OnTreeChanged;
    }

    private void OnTreeChanged(object? sender, ProjectTreeChange change)
    {
        if (change.Kind == ProjectTreeChangeKind.Added)
            Index(change.Node);
        else if (change.Kind == ProjectTreeChangeKind.Removed)
            Unindex(change.Node);

        Changed?.Invoke(this, change);
    }

    private void Index(ResourceNode node)
    {
        foreach (var child in node.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>())
        {
            if (_index.TryAdd(child.Item, child))
                SubscribeContent(child.Item);
            else
                _index[child.Item] = child;
        }
    }

    private void Unindex(ResourceNode node)
    {
        foreach (var child in node.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>())
        {
            if (_index.Remove(child.Item))
                UnsubscribeContent(child.Item);
        }
    }

    private void SubscribeContent(IProjectResource resource)
    {
        if (resource is Palette palette)
            palette.Changed += OnResourceChanged;
        else if (resource is DataSource source)
            source.DataWritten += OnResourceChanged;
    }

    private void UnsubscribeContent(IProjectResource resource)
    {
        if (resource is Palette palette)
            palette.Changed -= OnResourceChanged;
        else if (resource is DataSource source)
            source.DataWritten -= OnResourceChanged;
    }

    private void OnResourceChanged(object? sender, EventArgs e) => ResourceChanged?.Invoke(this, (IProjectResource)sender!);

    /// <summary>
    /// Determines if the specified resource is contained within the tree
    /// </summary>
    /// <param name="resource">Resource to search for</param>
    public bool ContainsResource(IProjectResource resource) => _index.ContainsKey(resource);

    /// <summary>
    /// Compares the node's root ancestor with the project root to determine if the tree contains the node
    /// </summary>
    /// <param name="node">Node to search</param>
    public bool ContainsNode(ResourceNode node) =>
        ReferenceEquals(node.SelfAndAncestors<ResourceNode, IProjectResource>().Last(), Root);

    /// <summary>
    /// Tries to find the resource node that contains the specified resource
    /// </summary>
    /// <param name="resource">Resource to locate</param>
    /// <param name="resourceNode">Result of search</param>
    /// <returns>True if found, false if not found</returns>
    public bool TryFindResourceNode(IProjectResource resource, [MaybeNullWhen(false)] out ResourceNode resourceNode) =>
        _index.TryGetValue(resource, out resourceNode);

    /// <summary>
    /// Gets the resource node that contains the specified resource
    /// </summary>
    /// <param name="resource">Resource to locate</param>
    public ResourceNode GetResourceNode(IProjectResource resource) =>
        _index.TryGetValue(resource, out var node) ? node :
            throw new KeyNotFoundException($"{nameof(GetResourceNode)}: resource '{resource.Name}' is not contained within project '{Name}'");

    /// <summary>
    /// Gets the resource node that contains the specified resource
    /// </summary>
    /// <param name="resource">Resource to locate</param>
    public T GetResourceNode<T>(IProjectResource resource) where T : ResourceNode =>
        _index.TryGetValue(resource, out var node) && node is T typed ? typed :
            throw new KeyNotFoundException($"{nameof(GetResourceNode)}: resource '{resource.Name}' of node type '{typeof(T).Name}' is not contained within project '{Name}'");
}
