using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Metatiles;
using ImageMagitek.Project.Serialization;
using Monaco.PathTree;

namespace ImageMagitek.Project;

/// <summary>
/// Plans a deletion over the resource graph's removal closure and the model edits that apply it (LIB-PROJECT-SERVICE-045 to -047, -066 and -080)
/// </summary>
public static class ResourceDeletionPlanner
{
    /// <summary>
    /// The resources removed or changed by deleting <paramref name="deleteNode"/> and its descendants, writing nothing
    /// </summary>
    public static ResourceDeletionPlan Plan(ProjectTree tree, ResourceNode deleteNode)
    {
        var graph = new ResourceGraph(tree);
        var roots = deleteNode.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>().ToList();
        var closure = graph.RemovalClosure(roots.Select(x => x.Item));
        var removed = closure.Removed;

        var changes = roots
            .Concat(tree.EnumerateDepthFirst().Where(x => removed.Contains(x.Item)).Except(roots))
            .Select(x => new ResourceChange(x, tree.CreatePathKey(x), true, false, false))
            .ToList();

        var changed = new Dictionary<ResourceNode, ResourceChange>();
        var slotChanged = new List<IProjectResource>();

        foreach (var node in tree.EnumerateDepthFirst())
        {
            if (!closure.Affected.TryGetValue(node.Item, out var kind))
                continue;

            var lostElements = kind.HasFlag(ResourceEdgeKind.DropElements);
            var lostPalette = node.Item is Arranger arranger &&
                arranger.EnumerateElements().OfType<ArrangerElement>().Any(x => x.Source is not null && !removed.Contains(x.Source) &&
                    x.Codec is IIndexedCodec { Palette: { } palette } && removed.Contains(palette));
            var lostTilePalette = kind.HasFlag(ResourceEdgeKind.ClearTilePalette);
            var lostSlotPalette = node.Item is TilemapArranger or MetatileSet && kind.HasFlag(ResourceEdgeKind.FallbackPalette);

            if (lostSlotPalette)
                slotChanged.Add(node.Item);

            if (lostElements || lostPalette || lostTilePalette || lostSlotPalette)
                changed[node] = new ResourceChange(node, tree.CreatePathKey(node), false, lostPalette, lostElements, lostTilePalette, lostSlotPalette);
        }

        foreach (var node in graph.RebuildOrder(slotChanged).Where(x => !removed.Contains(x.Item)))
        {
            changed[node] = changed.TryGetValue(node, out var change)
                ? change with { LostSlotPalette = true }
                : new ResourceChange(node, tree.CreatePathKey(node), false, false, false, LostSlotPalette: true);
        }

        changes.AddRange(changed.Values);
        return new ResourceDeletionPlan(tree, changes);
    }

    /// <summary>
    /// The models the plan's changed arrangers, tilemaps and metatile sets are written with, and those of them that must be rebuilt because they lose slot palettes
    /// </summary>
    /// <param name="resourceMap">The key of every resource the models may reference</param>
    /// <param name="fallbackPaletteKey">The key elements on a removed palette move to; required only when such an element exists</param>
    public static (List<(ResourceNode Node, ResourceModel Model)> Writes, List<(ResourceNode Node, ResourceModel Model)> SlotRebuilds) EditModels(
        ResourceDeletionPlan plan, Dictionary<IProjectResource, string> resourceMap, string? fallbackPaletteKey)
    {
        var removedKeys = plan.Changes
            .Where(x => x.Removed && resourceMap.ContainsKey(x.Resource))
            .Select(x => resourceMap[x.Resource])
            .ToHashSet();
        var writes = new List<(ResourceNode Node, ResourceModel Model)>();
        var slotRebuilds = new List<(ResourceNode Node, ResourceModel Model)>();

        foreach (var change in plan.Changes.Where(x => !x.Removed && x.Resource is ScatteredArranger))
            writes.Add((change.ResourceNode, WithoutRemovedKeys(((ScatteredArranger)change.Resource).MapToModel(resourceMap), removedKeys, fallbackPaletteKey)));

        foreach (var change in plan.Changes.Where(x => x is { Removed: false, Resource: TilemapArranger } && (x.LostTilePalette || x.LostSlotPalette)))
        {
            var model = ((TilemapArranger)change.Resource).MapToModel(resourceMap);
            if (change.LostTilePalette)
                model = model with { TilePaletteKey = null };

            if (change.LostSlotPalette)
            {
                model = model with { PaletteSlots = WithoutRemovedSlots(model.PaletteSlots, removedKeys) };
                slotRebuilds.Add((change.ResourceNode, model));
            }

            writes.Add((change.ResourceNode, model));
        }

        foreach (var change in plan.Changes.Where(x => x is { Removed: false, LostSlotPalette: true, Resource: MetatileSet }))
        {
            var model = ((MetatileSet)change.Resource).MapToModel(resourceMap);
            model = model with { PaletteSlots = WithoutRemovedSlots(model.PaletteSlots, removedKeys) };
            slotRebuilds.Add((change.ResourceNode, model));
            writes.Add((change.ResourceNode, model));
        }

        return (writes, slotRebuilds);
    }

    private static EquatableDictionary<int, string> WithoutRemovedSlots(IReadOnlyDictionary<int, string> slots, HashSet<string> removedKeys) =>
        EquatableDictionary<int, string>.From(slots.Where(x => !removedKeys.Contains(x.Value)).ToDictionary());

    private static ScatteredArrangerModel WithoutRemovedKeys(ScatteredArrangerModel model, HashSet<string> removedKeys, string? fallbackPaletteKey)
    {
        var grid = (ArrangerElementModel[,])model.ElementGrid.Clone();

        for (int y = 0; y < grid.GetLength(1); y++)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                var el = grid[x, y];

                if (el is null)
                    continue;

                if (removedKeys.Contains(el.DataFileKey))
                {
                    grid[x, y] = null!;
                }
                else if (el.PaletteKey is string paletteKey && removedKeys.Contains(paletteKey))
                {
                    grid[x, y] = el with
                    {
                        PaletteKey = fallbackPaletteKey ?? throw new InvalidOperationException("The fallback palette is not available to the project")
                    };
                }
            }
        }

        return model with { ElementGrid = grid };
    }
}
