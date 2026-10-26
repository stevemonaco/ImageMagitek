using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using Monaco.PathTree;

namespace ImageMagitek.Services;

/// <summary>
/// The open trees, write lock and serializer factory that <see cref="ProjectService"/> and <see cref="ResourceGenerationService"/> share
/// </summary>
public sealed class ProjectWorkspace(IProjectSerializerFactory serializerFactory)
{
    // One lock for all trees: every transaction in a directory shares its journal and staging paths
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal IProjectSerializerFactory SerializerFactory { get; } = serializerFactory;

    internal ISet<ProjectTree> Projects { get; } = new HashSet<ProjectTree>();

    // No ConfigureAwait(false): tree events raised after the awaited write must stay on the caller's context
    internal async Task<T> WithWriteLockAsync<T>(Func<Task<T>> operation)
    {
        await _writeLock.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal static string StandaloneFileReason(ProjectTree tree) => $"'{tree.Name}' is a standalone file, not a project";

    /// <summary>
    /// Applies a deletion plan to the tree and disk; the caller holds the write lock
    /// </summary>
    internal async Task<MagitekResult> ApplyDeletionAsync(ResourceDeletionPlan plan, Palette defaultPalette)
    {
        var tree = plan.Tree;
        var removedItems = plan.Changes.Where(x => x.Removed).ToList();
        var removedResources = removedItems.Select(x => x.Resource).ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);
        var changedArrangers = plan.Changes
            .Where(x => !x.Removed && x.Resource is ScatteredArranger)
            .ToList();
        var changedTilemaps = plan.Changes
            .Where(x => x is { Removed: false, Resource: TilemapArranger, LostTilePalette: true, LostSlotPalette: false })
            .ToList();
        List<(ResourceNode Node, ResourceModel Model)> slotEdits;

        try
        {
            var resourceMap = tree.CreateResourceMap(SerializerFactory.GlobalResources);
            (var edits, slotEdits) = ResourceDeletionPlanner.EditModels(plan, resourceMap, resourceMap.GetValueOrDefault(defaultPalette));

            var writeResult = await SerializerFactory.CreateWriter(tree).WriteModelsAsync(edits);
            if (writeResult.HasFailed)
                return writeResult;
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Could not update the resources that use the removed resources: {ex.Message}");
        }

        foreach (var arranger in changedArrangers.Select(x => (ScatteredArranger)x.Resource))
        {
            arranger.ResetElementsReading(removedResources);

            foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
            {
                if (arranger.GetElement(x, y) is ArrangerElement { Codec: IIndexedCodec codec } el && removedResources.Contains(codec.Palette))
                {
                    codec.Palette = defaultPalette;
                    arranger.SetElement(el, x, y);
                }
            }
        }

        foreach (var tilemap in changedTilemaps.Select(x => (TilemapArranger)x.Resource))
            tilemap.TilePaletteArranger = null;

        var reasons = new List<string>();
        var replacements = new Dictionary<IProjectResource, IProjectResource>(ReferenceEqualityComparer.Instance);

        // Each rebuild reads its written model, so the removed slots are gone; maps over a rebuilt set are rebuilt in turn
        foreach (var (node, model) in slotEdits)
        {
            if (RebuildNode(tree, node, model, reasons) is { } rebuilt)
                replacements[node.Item] = rebuilt.Item;
        }

        RetargetReferences(tree, replacements, removedItems.Select(x => x.ResourceNode).ToHashSet(), reasons);

        var undeletableFiles = new List<string>();

        foreach (var item in removedItems.Where(x => x.Resource is not ResourceFolder))
        {
            var resourceParent = item.ResourceNode.Parent;
            resourceParent?.RemoveChildNode(item.Resource.Name);

            if (item.ResourceNode.DiskLocation is string location)
            {
                try
                {
                    File.Delete(location);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    undeletableFiles.Add(location);
                }
            }

            // Editors on the source close on Removed, so nothing reads it after this
            if (item.Resource is IDisposable disposable)
                disposable.Dispose();
        }

        var keptFolders = new List<string>();

        // Longest path first deletes children before their parents
        foreach (var item in removedItems.Where(x => x.Resource is ResourceFolder).OrderByDescending(x => x.ResourceNode.DiskLocation?.Length ?? 0))
        {
            var resourceParent = item.ResourceNode.Parent;
            resourceParent?.RemoveChildNode(item.Resource.Name);

            // Non-empty folders hold files the user placed there, such as ROMs, so they are left on disk
            if (item.ResourceNode.DiskLocation is string location && Directory.Exists(location))
            {
                if (Directory.EnumerateFileSystemEntries(location).Any())
                    keptFolders.Add(location);
                else
                    Directory.Delete(location);
            }
        }

        if (undeletableFiles.Count > 0)
            reasons.Add($"These resource files could not be deleted. They will reappear when the project is reopened until removed:\n{string.Join("\n", undeletableFiles)}");

        if (keptFolders.Count > 0)
            reasons.Add($"These folders contain other files and were left on disk. They will reappear when the project is reopened until removed:\n{string.Join("\n", keptFolders)}");

        return reasons.Count > 0
            ? new MagitekResult.Failed(string.Join("\n\n", reasons))
            : MagitekResult.SuccessResult;
    }

    /// <summary>
    /// Replaces the node with one built from the model, raising one Replaced, and keeps the old node's persisted model so its file is rewritten only when it differs
    /// </summary>
    private ResourceNode? RebuildNode(ProjectTree tree, ResourceNode node, ResourceModel model, List<string> reasons)
    {
        var key = tree.CreatePathKey(node);
        var parent = node.Parent!;

        return tree.ReplaceNodes<ResourceNode?>([(node, key)], () =>
        {
            parent.DetachChildNode(node.Name);
            var buildResult = SerializerFactory.BuildResources(tree,
                [(model, parent is ProjectNode ? "" : tree.CreatePathKey(parent).TrimStart('/'), node.DiskLocation!)]);

            if (buildResult.HasFailed || !tree.TryGetNode(key, out var rebuilt))
            {
                if (tree.TryGetNode(key, out var partial))
                    partial.Parent?.RemoveChildNode(partial.Name);

                parent.AttachChildNode(node);
                reasons.Add($"'{key}' could not be rebuilt: {(buildResult.HasFailed ? string.Join("; ", buildResult.AsError.Reasons) : "it was not rebuilt")}");
                return null;
            }

            rebuilt.Model = node.Model;
            return rebuilt;
        });
    }

    /// <summary>
    /// Rebuilds the dependents of the replaced resources and points every other user at the replacements in memory, then disposes the replaced resources
    /// when every user moved
    /// </summary>
    internal void RetargetReferences(ProjectTree tree, Dictionary<IProjectResource, IProjectResource> replacements,
        HashSet<ResourceNode> skipped, List<string> reasons)
    {
        if (replacements.Count == 0)
            return;

        var resourceMap = tree.CreateResourceMap(SerializerFactory.GlobalResources);
        foreach (var (old, replacement) in replacements)
            resourceMap[old] = resourceMap[replacement];

        var rebuildable = new ResourceGraph(tree).RebuildOrder(replacements.Keys).Where(x => !skipped.Contains(x)).ToList();
        var writer = SerializerFactory.CreateWriter(tree);

        foreach (var node in rebuildable)
        {
            // A dependent of a failed rebuild still reads the old resource, so it is not rebuilt either
            if (!node.Item.Dependencies.Any(x => x.Kind is ResourceEdgeKind.Cascade or ResourceEdgeKind.FallbackPalette && replacements.ContainsKey(x.Target)))
                continue;

            var key = tree.CreatePathKey(node);
            var mapResult = writer.MapCurrentModel(node, resourceMap);
            if (mapResult.HasFailed)
            {
                reasons.Add($"'{key}' still uses the replaced resources: {mapResult.AsError.Reason}");
                continue;
            }

            if (RebuildNode(tree, node, mapResult.AsSuccess.Result, reasons) is not { } rebuilt)
                continue;

            replacements[node.Item] = rebuilt.Item;
            resourceMap[node.Item] = key;
            resourceMap[rebuilt.Item] = key;
        }

        foreach (var node in tree.EnumerateDepthFirst().Where(x => !skipped.Contains(x)))
        {
            if (node.Item is TilemapArranger { TilePaletteArranger: { } tilePalette } tilemap && replacements.TryGetValue(tilePalette, out var newTilePalette))
                tilemap.TilePaletteArranger = (ScatteredArranger)newTilePalette;
            else if (node.Item is ScatteredArranger arranger)
                RetargetElements(arranger, replacements);
        }

        if (reasons.Count == 0)
        {
            foreach (var disposable in replacements.Keys.OfType<IDisposable>())
                disposable.Dispose();
        }
    }

    private static void RetargetElements(ScatteredArranger arranger, Dictionary<IProjectResource, IProjectResource> replacements)
    {
        foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
        {
            if (arranger.GetElement(x, y) is not ArrangerElement el)
                continue;

            var source = replacements.TryGetValue(el.Source, out var newSource) ? (DataSource)newSource : el.Source;
            var paletteReplaced = el.Codec is IIndexedCodec { Palette: { } palette } && replacements.ContainsKey(palette);
            if (ReferenceEquals(source, el.Source) && !paletteReplaced)
                continue;

            if (paletteReplaced)
            {
                var codec = (IIndexedCodec)el.Codec;
                codec.Palette = (Palette)replacements[codec.Palette];
            }

            arranger.SetElement(el.WithFile(source, el.SourceAddress), x, y);
        }
    }

    /// <summary>
    /// Deletes a renamed resource's file at its old location once the write to the new one committed
    /// </summary>
    internal static void RemoveRenamedFile(string oldLocation, string newLocation)
    {
        if (!string.Equals(oldLocation, newLocation, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(oldLocation);
            return;
        }

        var fileNames = Directory.EnumerateFiles(Path.GetDirectoryName(newLocation)!)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        bool hasOldFile = fileNames.Contains(Path.GetFileName(oldLocation));
        bool hasNewFile = fileNames.Contains(Path.GetFileName(newLocation));

        // On case-insensitive filesystems the write lands in the old file and keeps its casing
        if (hasOldFile && hasNewFile)
            File.Delete(oldLocation);
        else if (hasOldFile)
        {
            var tempLocation = $"{newLocation}.{Guid.NewGuid():N}.tmp";
            File.Move(oldLocation, tempLocation);
            File.Move(tempLocation, newLocation);
        }
    }
}
