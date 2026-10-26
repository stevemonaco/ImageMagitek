using System;
using System.Collections.Generic;
using System.Linq;
using Monaco.PathTree;

namespace ImageMagitek.Project;

/// <summary>
/// What happens to a dependent when the resource it uses is removed
/// </summary>
[Flags]
public enum ResourceEdgeKind
{
    /// <summary>The dependent is removed, or rebuilt from its model when the target is replaced</summary>
    Cascade = 1,

    /// <summary>The dependent's elements on the target are dropped; it is removed when none are left</summary>
    DropElements = 2,

    /// <summary>The dependent falls back to a default palette in place of the target</summary>
    FallbackPalette = 4,

    /// <summary>The dependent tilemap's tile palette is cleared</summary>
    ClearTilePalette = 8
}

/// <summary>
/// A dependency on <paramref name="Target"/> and what happens to the dependent when the target goes
/// </summary>
public readonly record struct ResourceEdge(IProjectResource Target, ResourceEdgeKind Kind);

/// <summary>
/// The resources removed with a set of roots and the surviving dependents of those removals, with the kinds of edge that reach each
/// </summary>
public sealed record ResourceRemovalClosure(IReadOnlySet<IProjectResource> Removed, IReadOnlyDictionary<IProjectResource, ResourceEdgeKind> Affected)
{
    public bool Contains(IProjectResource resource) => Removed.Contains(resource) || Affected.ContainsKey(resource);
}

/// <summary>
/// The dependencies between the resources of a tree, built for one operation and not kept current with the tree
/// </summary>
public sealed class ResourceGraph
{
    private const ResourceEdgeKind RebuildKinds = ResourceEdgeKind.Cascade | ResourceEdgeKind.FallbackPalette;

    private readonly Dictionary<IProjectResource, (ResourceNode Node, int Index, IReadOnlyList<ResourceEdge> Edges)> _nodes =
        new(ReferenceEqualityComparer.Instance);

    // Targets include global palettes and detached replaced nodes, which are never dependents
    private readonly Dictionary<IProjectResource, List<(IProjectResource Dependent, ResourceEdgeKind Kind)>> _dependents =
        new(ReferenceEqualityComparer.Instance);

    public ResourceGraph(ProjectTree tree)
    {
        var index = 0;

        foreach (var node in tree.EnumerateDepthFirst())
        {
            var edges = node.Item.Dependencies.ToList();
            if (!_nodes.TryAdd(node.Item, (node, index++, edges)))
                continue;

            foreach (var edge in edges)
            {
                if (!_dependents.TryGetValue(edge.Target, out var dependents))
                {
                    dependents = [];
                    _dependents[edge.Target] = dependents;
                }

                dependents.Add((node.Item, edge.Kind));
            }
        }
    }

    /// <summary>
    /// The roots, every resource removed with them over reverse Cascade edges or by losing all of its DropElements targets,
    /// and the surviving dependents of those removals
    /// </summary>
    public ResourceRemovalClosure RemovalClosure(IEnumerable<IProjectResource> roots)
    {
        var removed = new HashSet<IProjectResource>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<IProjectResource>();

        foreach (var root in roots)
        {
            if (removed.Add(root))
                pending.Enqueue(root);
        }

        while (pending.TryDequeue(out var target))
        {
            foreach (var (dependent, kind) in DependentsOf(target))
            {
                if (removed.Contains(dependent))
                    continue;

                var isRemoved = kind == ResourceEdgeKind.Cascade ||
                    kind == ResourceEdgeKind.DropElements && _nodes[dependent].Edges
                        .Where(x => x.Kind == ResourceEdgeKind.DropElements)
                        .All(x => removed.Contains(x.Target));

                if (isRemoved)
                {
                    removed.Add(dependent);
                    pending.Enqueue(dependent);
                }
            }
        }

        var affected = new Dictionary<IProjectResource, ResourceEdgeKind>(ReferenceEqualityComparer.Instance);

        foreach (var target in removed)
        {
            foreach (var (dependent, kind) in DependentsOf(target))
            {
                if (!removed.Contains(dependent))
                    affected[dependent] = affected.GetValueOrDefault(dependent) | kind;
            }
        }

        return new ResourceRemovalClosure(removed, affected);
    }

    /// <summary>
    /// Every resource other than an element arranger that reaches one of <paramref name="replaced"/> over Cascade or FallbackPalette edges,
    /// each after every such resource it depends on, ties in tree order
    /// </summary>
    public IReadOnlyList<ResourceNode> RebuildOrder(IEnumerable<IProjectResource> replaced)
    {
        var roots = new HashSet<IProjectResource>(replaced, ReferenceEqualityComparer.Instance);
        var members = new HashSet<IProjectResource>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<IProjectResource>(roots);

        while (pending.TryDequeue(out var target))
        {
            foreach (var dependent in RebuildDependentsOf(target))
            {
                if (!roots.Contains(dependent) && members.Add(dependent))
                    pending.Enqueue(dependent);
            }
        }

        var waiting = new Dictionary<IProjectResource, int>(ReferenceEqualityComparer.Instance);
        foreach (var member in members)
        {
            waiting[member] = _nodes[member].Edges
                .Where(x => (x.Kind & RebuildKinds) != 0 && members.Contains(x.Target))
                .Select(x => x.Target)
                .Distinct<IProjectResource>(ReferenceEqualityComparer.Instance)
                .Count();
        }

        var ready = new PriorityQueue<IProjectResource, int>();
        foreach (var (member, _) in waiting.Where(x => x.Value == 0))
            ready.Enqueue(member, _nodes[member].Index);

        var order = new List<ResourceNode>();

        while (ready.TryDequeue(out var next, out _))
        {
            order.Add(_nodes[next].Node);

            foreach (var dependent in RebuildDependentsOf(next).Where(members.Contains))
            {
                if (--waiting[dependent] == 0)
                    ready.Enqueue(dependent, _nodes[dependent].Index);
            }
        }

        return order;
    }

    private IEnumerable<IProjectResource> RebuildDependentsOf(IProjectResource target) =>
        DependentsOf(target)
            .Where(x => (x.Kind & RebuildKinds) != 0 && x.Dependent is not Arranger)
            .Select(x => x.Dependent)
            .Distinct<IProjectResource>(ReferenceEqualityComparer.Instance);

    private IReadOnlyList<(IProjectResource Dependent, ResourceEdgeKind Kind)> DependentsOf(IProjectResource target) =>
        _dependents.TryGetValue(target, out var dependents) ? dependents : [];
}
