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
    private List<ProjectTreeChange>? _heldChanges;

    public ImageProject? Project => Root.Item as ImageProject;
    public string Name => Root.Item.Name;

    /// <summary>
    /// True when the tree is a single data file opened without a project
    /// </summary>
    public bool IsStandaloneFile => Root is DataFileNode;

    /// <summary>
    /// Raised after a node in the tree is added, removed, moved, renamed or replaced
    /// </summary>
    public event EventHandler<ProjectTreeChange>? Changed;

    /// <summary>
    /// Raised when a palette in the tree changes or graphics data is written to a data source in the tree
    /// </summary>
    public event EventHandler<IProjectResource>? ResourceChanged;

    public ProjectTree(ResourceNode root) :
        base(root)
    {
        if (root is not (ProjectNode { Item: ImageProject } or DataFileNode))
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

        if (_heldChanges is not null)
            _heldChanges.Add(change);
        else
            Changed?.Invoke(this, change);
    }

    /// <summary>
    /// Runs <paramref name="swap"/> while holding tree changes, then raises each listed node detached and replaced by the node attached at its new key
    /// as one Replaced change (LIB-PROJECT-TREE-035)
    /// </summary>
    public T ReplaceNodes<T>(IReadOnlyList<(ResourceNode Old, string NewKey)> replacements, Func<T> swap)
    {
        if (_heldChanges is not null)
            throw new InvalidOperationException($"{nameof(ReplaceNodes)} cannot be nested");

        var held = _heldChanges = [];
        try
        {
            return swap();
        }
        finally
        {
            _heldChanges = null;
            foreach (var change in ResolveHeldChanges(held, replacements))
                Changed?.Invoke(this, change);
        }
    }

    /// <inheritdoc cref="ReplaceNodes{T}"/>
    public void ReplaceNodes(IReadOnlyList<(ResourceNode Old, string NewKey)> replacements, Action swap) =>
        ReplaceNodes(replacements, () =>
        {
            swap();
            return true;
        });

    private List<ProjectTreeChange> ResolveHeldChanges(List<ProjectTreeChange> held, IReadOnlyList<(ResourceNode Old, string NewKey)> replacements)
    {
        var dropped = new HashSet<ProjectTreeChange>(ReferenceEqualityComparer.Instance);
        var latest = new Dictionary<ResourceNode, ProjectTreeChange>(ReferenceEqualityComparer.Instance);

        foreach (var change in held.Where(x => x.Kind is ProjectTreeChangeKind.Added or ProjectTreeChangeKind.Removed))
        {
            // A node added then removed, or removed then put back under the same parent, was never visibly changed
            if (latest.Remove(change.Node, out var earlier) && earlier.Kind != change.Kind
                && (earlier.Kind == ProjectTreeChangeKind.Added || ReferenceEquals(earlier.Parent, change.Parent)))
            {
                dropped.Add(earlier);
                dropped.Add(change);
            }
            else
            {
                latest[change.Node] = change;
            }
        }

        var replacedAt = new Dictionary<ProjectTreeChange, ProjectTreeChange>(ReferenceEqualityComparer.Instance);

        foreach (var (old, newKey) in replacements)
        {
            if (!TryGetNode(newKey, out var node)
                || latest.GetValueOrDefault(old) is not { Kind: ProjectTreeChangeKind.Removed } removed
                || latest.GetValueOrDefault(node) is not { Kind: ProjectTreeChangeKind.Added } added)
                continue;

            dropped.Add(removed);
            replacedAt[added] = new ProjectTreeChange(ProjectTreeChangeKind.Replaced, node, added.Parent, removed.Parent, OldNode: old);
        }

        return held
            .Select(x => replacedAt.GetValueOrDefault(x) ?? x)
            .Where(x => !dropped.Contains(x))
            .ToList();
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

    /// <summary>
    /// Maps every global resource to its name and every tree resource to its path key, as written files reference them
    /// </summary>
    public Dictionary<IProjectResource, string> CreateResourceMap(IEnumerable<IProjectResource> globalResources)
    {
        var resourceMap = new Dictionary<IProjectResource, string>();

        foreach (var resource in globalResources)
            resourceMap.Add(resource, resource.Name);

        foreach (var node in this.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
            resourceMap.Add(node.Item, CreatePathKey(node));

        return resourceMap;
    }
}
