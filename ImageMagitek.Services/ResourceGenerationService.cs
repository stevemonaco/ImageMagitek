using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tables;
using Monaco.PathTree;

namespace ImageMagitek.Services;

/// <summary>
/// Generates and regenerates resources from table definitions into the workspace's open projects, under the workspace's write lock
/// </summary>
public class ResourceGenerationService(ProjectWorkspace workspace) : IResourceGenerationService
{
    private readonly IProjectSerializerFactory _serializerFactory = workspace.SerializerFactory;

    /// <inheritdoc/>
    public virtual MagitekResult<GenerationPlan> PlanGeneration(ProjectTree tree, string dataFileKey, TableDefinition definition, string targetFolderKey,
        bool overwriteEdited = false)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(definition);

        if (tree.IsStandaloneFile)
            return new MagitekResult<GenerationPlan>.Failed(ProjectWorkspace.StandaloneFileReason(tree));

        if (!tree.TryGetNode(dataFileKey, out var dataNode) || dataNode.Item is not DataSource source)
            return new MagitekResult<GenerationPlan>.Failed($"'{dataFileKey}' is not a data file in project '{tree.Name}'");

        var targetResult = ResolveTargetFolder(tree, targetFolderKey);
        if (targetResult.HasFailed)
            return new MagitekResult<GenerationPlan>.Failed(targetResult.AsError.Reason);

        GenerationPlan plan;
        try
        {
            var generator = new TableGenerator(definition, source, tree.CreatePathKey(dataNode), targetResult.AsSuccess.Result, _serializerFactory.MapFormatFactory);
            plan = generator.Plan(tree.Root.DiskLocation!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MagitekResult<GenerationPlan>.Failed($"Could not read '{dataFileKey}': {ex.Message}");
        }

        var manifestResult = ReadManifest(plan);
        if (manifestResult.HasFailed)
            return new MagitekResult<GenerationPlan>.Failed(manifestResult.AsError.Reason);

        if (manifestResult.AsSuccess.Result.FindGeneration(plan.ManifestGeneration.Project, plan.ManifestGeneration.Folder) is not { } generation)
            return new MagitekResult<GenerationPlan>.Success(plan);

        if (!string.Equals(generation.DataFile, plan.DataFileKey, StringComparison.OrdinalIgnoreCase))
            return new MagitekResult<GenerationPlan>.Failed(
                $"'{generation.Folder}' was generated from data file '{generation.DataFile}', not '{plan.DataFileKey}'");

        var entries = FindGeneratedResources(tree, generation);
        var regeneration = KeepReferencedRemovals(tree, RegenerationPlan.Match(plan, entries, overwriteEdited));
        return new MagitekResult<GenerationPlan>.Success(plan with { Regeneration = regeneration.WithAffected(FindAffected(tree, regeneration)) });
    }

    // The changed and removed resources, and the dependents the retarget rebuilds over them (LIB-PROJECT-SERVICE-081)
    private static HashSet<IProjectResource> FindAffected(ProjectTree tree, RegenerationPlan regeneration)
    {
        var affected = ReplacedResources(tree, regeneration);
        affected.UnionWith(FindNodes(tree, regeneration.Removed.Select(x => x.CurrentKey)).Select(x => x.Item));
        affected.UnionWith(new ResourceGraph(tree).RebuildOrder(affected).Select(x => x.Item));
        return affected;
    }

    private static HashSet<IProjectResource> ReplacedResources(ProjectTree tree, RegenerationPlan regeneration) =>
        FindNodes(tree, regeneration.Written.Where(x => x.Kind == RegenerationKind.Changed).Select(x => x.Entry!.CurrentKey))
            .Select(x => x.Item)
            .ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);

    private static MagitekResult<TableManifest> ReadManifest(GenerationPlan plan)
    {
        if (!File.Exists(plan.ManifestFileName))
            return new MagitekResult<TableManifest>.Success(TableManifest.Empty);

        var readResult = new XmlTableManifestReader(plan.ManifestSchemaFileName).LoadFromFile(plan.ManifestFileName);
        return readResult.HasFailed
            ? new MagitekResult<TableManifest>.Failed($"Manifest '{plan.ManifestFileName}' could not be read:\n{string.Join("\n", readResult.AsError.Reasons)}")
            : new MagitekResult<TableManifest>.Success(readResult.AsSuccess.Result);
    }

    // The manifest schema never names a data file, so a data file node matches no entry
    private string? GeneratedTypeName(ResourceNode node) => _serializerFactory.Kinds.FindByNode(node)?.ElementName;

    // Every entry is looked up by key before any by hash, so a resource found by hash is never one another entry names
    private List<RegenerationEntry> FindGeneratedResources(ProjectTree tree, TableManifestGeneration generation)
    {
        var fileHashes = new Dictionary<ResourceNode, string?>();
        var byHash = new Dictionary<string, ILookup<string?, ResourceNode>>();
        var found = new ResourceNode?[generation.Resources.Count];
        var taken = new HashSet<ResourceNode>();

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (tree.TryGetNode(entry.Key, out var node) && GeneratedTypeName(node) == entry.Type && taken.Add(node))
                found[i] = node;
        }

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (found[i] is not null || entry.Hash is not { } hash)
                continue;

            if (!byHash.TryGetValue(entry.Type, out var candidates))
            {
                candidates = tree.EnumerateDepthFirst().Where(x => GeneratedTypeName(x) == entry.Type).ToLookup(HashOf);
                byHash[entry.Type] = candidates;
            }

            if (candidates[hash].Where(x => !taken.Contains(x)).ToList() is [var moved])
            {
                found[i] = moved;
                taken.Add(moved);
            }
        }

        var writer = _serializerFactory.CreateWriter(tree);
        var entries = new List<RegenerationEntry>();
        var entryKeys = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < found.Length; i++)
        {
            if (found[i] is { } node)
                entryKeys[tree.CreatePathKey(node)] = generation.Resources[i].Key;
        }

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (found[i] is not { } node)
            {
                entries.Add(new RegenerationEntry(entry, null, null, false));
                continue;
            }

            var mapResult = writer.MapCurrentModel(node);
            var current = mapResult.HasSucceeded ? mapResult.AsSuccess.Result : null;
            var isEdited = entry.Hash is null || current is null || current != node.Model
                || HashOf(node) is var fileHash && fileHash != entry.Hash && !IsRewrittenByMoves(current, fileHash, entry.Hash);
            entries.Add(new RegenerationEntry(entry, tree.CreatePathKey(node), current, isEdited));
        }

        return entries;

        // The file is exactly what TileShop writes for the model, and the model with its references put back is the one generated
        bool IsRewrittenByMoves(ResourceModel current, string? fileHash, string entryHash)
        {
            if (TableManifest.HashContents(writer.SerializeModel(current)) != fileHash)
                return false;

            var original = RegenerationPlan.WithKeys(current, current.Name, key => entryKeys.GetValueOrDefault(key, key));
            return TableManifest.HashContents(writer.SerializeModel(original)) == entryHash;
        }

        string? HashOf(ResourceNode node)
        {
            if (fileHashes.TryGetValue(node, out var hash))
                return hash;

            try
            {
                hash = node.DiskLocation is { } location && File.Exists(location) ? TableManifest.HashFile(location) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                hash = null;
            }

            fileHashes[node] = hash;
            return hash;
        }
    }

    // Keeping one removal can block another, such as a palette a kept set uses, so this repeats until nothing more is kept
    private RegenerationPlan KeepReferencedRemovals(ProjectTree tree, RegenerationPlan regeneration)
    {
        var replaced = ReplacedResources(tree, regeneration);
        var written = regeneration.Written.Select(x => (x.TargetKey, Keys: RegenerationPlan.ReferencedKeys(x.Model))).ToList();
        var graph = new ResourceGraph(tree);
        var treeOrder = tree.EnumerateDepthFirst().ToList();

        var removals = regeneration.Removed
            .Select(x => (Removal: x, Node: x.CurrentKey is not null && tree.TryGetNode(x.CurrentKey, out var node) ? node : null))
            .Where(x => x.Node is not null)
            .Select(x => (x.Removal, Resource: x.Node!.Item, Closure: graph.RemovalClosure([x.Node.Item])))
            .ToList();
        var removedResources = removals.Select(x => x.Resource).ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);

        for (var kept = true; kept;)
        {
            kept = false;

            foreach (var (removal, resource, closure) in removals.Where(x => removedResources.Contains(x.Resource)))
            {
                // A change only cascading through a replaced resource, such as a map whose replaced set loses a palette, does not count
                var gone = closure.Removed.Where(x => !replaced.Contains(x)).ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);
                var usedBy = treeOrder
                    .Where(x => closure.Contains(x.Item) && !removedResources.Contains(x.Item) && !replaced.Contains(x.Item) &&
                        x.Item.Dependencies.Any(edge => gone.Contains(edge.Target)))
                    .Select(tree.CreatePathKey)
                    .ToList();

                usedBy.AddRange(written.Where(x => x.Keys.Contains(removal.CurrentKey!)).Select(x => x.TargetKey));

                if (usedBy.Count == 0)
                    continue;

                regeneration = regeneration.KeepReferenced(removal, usedBy.Distinct().ToList());
                removedResources.Remove(resource);
                kept = true;
                break;
            }
        }

        return regeneration;
    }

    private static IEnumerable<ResourceNode> FindNodes(ProjectTree tree, IEnumerable<string?> keys) =>
        keys.Select(x => x is not null && tree.TryGetNode(x, out var node) ? node : null).OfType<ResourceNode>();

    // Spells the key as the existing folders spell it, so a case variant does not plan a second folder
    private static MagitekResult<string> ResolveTargetFolder(ProjectTree tree, string targetFolderKey)
    {
        ResourceNode? node = tree.Root;
        var key = "";

        foreach (var segment in targetFolderKey.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            var child = node?.ChildNodes.FirstOrDefault(x => ResourceName.AreSame(x.Name, segment));
            if (child is not null and not ResourceFolderNode)
                return new MagitekResult<string>.Failed($"'{tree.CreatePathKey(child)}' is not a folder");

            if (child is null && ResourceName.Validate(segment, true) is { HasFailed: true } invalid)
                return new MagitekResult<string>.Failed($"Target folder segment '{segment}' is not a valid folder name: {invalid.AsError.Reason}");

            key += "/" + (child?.Name ?? segment);
            node = child;
        }

        return new MagitekResult<string>.Success(key.Length == 0 ? "/" : key);
    }

    /// <inheritdoc/>
    public virtual Task<MagitekResult> GenerateResourcesAsync(ProjectTree tree, GenerationPlan plan, bool saveProject = false, bool confirmRemovals = false)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(plan);

        return workspace.WithWriteLockAsync(() => GenerateResourcesCoreAsync(tree, plan, saveProject, confirmRemovals));
    }

    private async Task<MagitekResult> GenerateResourcesCoreAsync(ProjectTree tree, GenerationPlan plan, bool saveProject, bool confirmRemovals)
    {
        if (tree.IsStandaloneFile)
            return new MagitekResult.Failed(ProjectWorkspace.StandaloneFileReason(tree));

        if (!workspace.Projects.Contains(tree))
            return new MagitekResult.Failed($"Project '{tree.Name}' is not open");

        if (!tree.TryGetItem<DataSource>(plan.DataFileKey, out _))
            return new MagitekResult.Failed($"'{plan.DataFileKey}' is not a data file in project '{tree.Name}'");

        var conflicts = FindGenerationConflicts(tree, plan);
        if (conflicts.Count > 0)
            return new MagitekResult.Failed($"Generation would clash with existing resources:\n{string.Join("\n", conflicts)}");

        var manifestResult = ReadManifest(plan);
        if (manifestResult.HasFailed)
            return new MagitekResult.Failed(manifestResult.AsError.Reason);

        if (plan.Regeneration is { } regeneration)
            return await RegenerateCoreAsync(tree, plan, regeneration, manifestResult.AsSuccess.Result, saveProject, confirmRemovals);

        var createdFolders = new List<ResourceNode>();
        var createdDirectories = new List<string>();
        MagitekResult failure;

        try
        {
            foreach (var folderKey in plan.FolderKeys)
                EnsureFolder(tree, folderKey, createdFolders, createdDirectories);

            var buildResult = _serializerFactory.BuildResources(tree, ToBuildModels(tree, plan.Resources.Select(x => (x.Model, x.ParentKey, x.Name))));
            if (buildResult.HasSucceeded)
            {
                var writer = _serializerFactory.CreateWriter(tree);
                var (writeNodes, hashes) = PrepareWrite(tree, writer, plan.Resources.Select(x => x.Key));
                var generation = plan.ManifestGeneration with
                {
                    Resources = plan.ManifestGeneration.Resources.Select(x => x with { Hash = hashes.GetValueOrDefault(x.Key) }).ToList()
                };

                var writeResult = await WriteGenerationAsync(tree, writer, writeNodes, plan.ManifestFileName,
                    manifestResult.AsSuccess.Result.WithGeneration(generation), saveProject, null);
                if (writeResult.HasSucceeded)
                    return writeResult;

                failure = writeResult;
            }
            else
            {
                failure = new MagitekResult.Failed(string.Join("\n", buildResult.AsError.Reasons));
            }
        }
        catch (Exception ex)
        {
            failure = new MagitekResult.Failed($"Generation failed: {ex.Message}");
        }

        RollBackGeneration(tree, plan.Resources.Select(x => x.Key), createdFolders, createdDirectories);
        return failure;
    }

    private static List<(ResourceModel Model, string ParentNodePath, string FileLocation)> ToBuildModels(ProjectTree tree,
        IEnumerable<(ResourceModel Model, string ParentKey, string Name)> resources) =>
        resources.Select(resource =>
        {
            tree.TryGetNode(resource.ParentKey, out var parent);
            return (resource.Model, resource.ParentKey.TrimStart('/'), ResourceFileLocator.LocateByParent(tree, parent ?? tree.Root, resource.Name, false));
        }).ToList();

    // The builder records each model as persisted; clearing it makes the write include every built resource.
    // The project file is a candidate too, so a format version the new resources need is written.
    private static (List<ResourceNode> Nodes, Dictionary<string, string> Hashes) PrepareWrite(ProjectTree tree, IProjectWriter writer, IEnumerable<string> keys)
    {
        var nodes = new List<ResourceNode> { tree.Root };
        var hashes = new Dictionary<string, string>();

        foreach (var key in keys)
        {
            if (tree.TryGetNode(key, out var node))
            {
                node.Model = null;
                nodes.Add(node);
                hashes[key] = TableManifest.HashContents(writer.SerializeResource(node));
            }
        }

        return (nodes, hashes);
    }

    private static Task<MagitekResult> WriteGenerationAsync(ProjectTree tree, IProjectWriter writer, List<ResourceNode> nodes, string manifestFileName,
        TableManifest manifest, bool saveProject, Dictionary<IProjectResource, string>? resourceMap)
    {
        var manifestFiles = new[] { (manifestFileName, XmlTableManifestWriter.Serialize(manifest)) };
        return saveProject
            ? writer.WriteProjectAsync(tree.Root.DiskLocation!, manifestFiles, resourceMap)
            : writer.WriteResourcesAsync(nodes, manifestFiles, resourceMap);
    }

    private async Task<MagitekResult> RegenerateCoreAsync(ProjectTree tree, GenerationPlan plan, RegenerationPlan regeneration, TableManifest manifest,
        bool saveProject, bool confirmRemovals)
    {
        var stale = regeneration.Resources.Select(x => x.Entry?.CurrentKey)
            .Concat(regeneration.Removed.Select(x => x.CurrentKey))
            .OfType<string>()
            .Where(x => !tree.TryGetNode(x, out _))
            .ToList();
        if (stale.Count > 0)
            return new MagitekResult.Failed($"The project changed after regeneration was planned; these resources are gone:\n{string.Join("\n", stale)}");

        if (regeneration.Removed.Count > 0 && !confirmRemovals)
            return new MagitekResult.Failed(
                $"Regeneration would remove {regeneration.Removed.Count} resources:\n{string.Join("\n", regeneration.Removed.Select(x => x.CurrentKey))}");

        var removals = CollectRemovals(tree, regeneration);
        var written = regeneration.Written.ToList();
        var replaced = written
            .Where(x => x.Kind == RegenerationKind.Changed)
            .Select(x => (Resource: x, Node: tree.TryGetNode(x.Entry!.CurrentKey!, out var node) ? node : null!))
            .ToList();
        var oldNodes = replaced.Select(x => x.Node).ToHashSet();
        var detached = new List<(ResourceNode Node, ResourceNode Parent)>();
        var createdFolders = new List<ResourceNode>();
        var createdDirectories = new List<string>();
        MagitekResult failure;

        try
        {
            var usedFolders = written.Select(x => x.ParentKey).ToHashSet(StringComparer.Ordinal);
            foreach (var folderKey in plan.FolderKeys.Where(x => usedFolders.Any(used => used == x || used.StartsWith(x == "/" ? "/" : x + "/", StringComparison.Ordinal))))
                EnsureFolder(tree, folderKey, createdFolders, createdDirectories);

            var buildResult = tree.ReplaceNodes(replaced.Select(x => (x.Node, x.Resource.TargetKey)).ToList(), () =>
            {
                foreach (var (_, node) in replaced)
                {
                    var parent = node.Parent!;
                    parent.DetachChildNode(node.Name);
                    detached.Add((node, parent));
                }

                return _serializerFactory.BuildResources(tree, ToBuildModels(tree, written.Select(x => (x.Model, x.ParentKey, x.Name))));
            });
            if (buildResult.HasSucceeded)
            {
                var writer = _serializerFactory.CreateWriter(tree);
                var (writeNodes, hashes) = PrepareWrite(tree, writer, written.Select(x => x.TargetKey));

                foreach (var resource in regeneration.Resources.Where(x => x.Kind == RegenerationKind.Unchanged))
                {
                    if (tree.TryGetNode(resource.TargetKey, out var node) && node.DiskLocation is { } location && File.Exists(location))
                        hashes[resource.TargetKey] = TableManifest.HashFile(location);
                }

                var replacements = FindReplacements(tree, replaced);
                var resourceMap = tree.CreateResourceMap(_serializerFactory.GlobalResources);
                foreach (var (old, replacement) in replacements)
                    resourceMap[old] = resourceMap[replacement];

                writeNodes.AddRange(FindDependents(tree, writer, replacements, resourceMap, removals, writeNodes));

                var writeResult = await WriteGenerationAsync(tree, writer, writeNodes, plan.ManifestFileName,
                    manifest.WithGeneration(regeneration.ToManifestGeneration(plan.ManifestGeneration, hashes)), saveProject, resourceMap);
                if (writeResult.HasSucceeded)
                    return await FinishRegenerationAsync(tree, replaced, replacements, removals);

                failure = writeResult;
            }
            else
            {
                failure = new MagitekResult.Failed(string.Join("\n", buildResult.AsError.Reasons));
            }
        }
        catch (Exception ex)
        {
            failure = new MagitekResult.Failed($"Regeneration failed: {ex.Message}");
        }

        var builtKeys = written.Select(x => x.TargetKey).Where(x => tree.TryGetNode(x, out var node) && !oldNodes.Contains(node)).ToList();
        var restored = replaced
            .Select(x => (Built: tree.TryGetNode(x.Resource.TargetKey, out var built) ? built : null, Key: x.Resource.Entry!.CurrentKey!))
            .Where(x => x.Built is not null && !oldNodes.Contains(x.Built))
            .Select(x => (x.Built!, x.Key))
            .ToList();

        tree.ReplaceNodes(restored, () =>
        {
            RollBackGeneration(tree, builtKeys, createdFolders, createdDirectories, restored.Select(x => x.Item1).ToHashSet());

            foreach (var (node, parent) in Enumerable.Reverse(detached))
                parent.AttachChildNode(node);
        });

        return failure;
    }

    // Found again at generation, so the changes hold the nodes as they are now
    private static List<ResourceChange> CollectRemovals(ProjectTree tree, RegenerationPlan regeneration) =>
        FindNodes(tree, regeneration.Removed.Select(x => x.CurrentKey))
            .Distinct()
            .Select(x => new ResourceChange(x, tree.CreatePathKey(x), true, false, false))
            .ToList();

    private static Dictionary<IProjectResource, IProjectResource> FindReplacements(ProjectTree tree, List<(RegeneratedResource Resource, ResourceNode Node)> replaced)
    {
        var replacements = new Dictionary<IProjectResource, IProjectResource>(ReferenceEqualityComparer.Instance);

        foreach (var (resource, oldNode) in replaced)
        {
            if (!tree.TryGetNode(resource.TargetKey, out var newNode))
                continue;

            // Users that still name the old key read whatever now holds it, as a reload would, such as a palette added at a renamed one's key
            var atOldKey = tree.TryGetNode(resource.Entry!.CurrentKey!, out var occupant) && occupant.Item.GetType() == oldNode.Item.GetType() ? occupant : newNode;
            replacements[oldNode.Item] = atOldKey.Item;
        }

        return replacements;
    }

    // A dependent that cannot be mapped is left for the retarget after the commit to report, as it cannot be written
    private static IEnumerable<ResourceNode> FindDependents(ProjectTree tree, IProjectWriter writer, Dictionary<IProjectResource, IProjectResource> replacements,
        Dictionary<IProjectResource, string> resourceMap, List<ResourceChange> removals, List<ResourceNode> writeNodes)
    {
        if (replacements.Count == 0)
            return [];

        var excluded = removals.Select(x => x.ResourceNode).Concat(writeNodes).ToHashSet();

        return new ResourceGraph(tree).RebuildOrder(replacements.Keys)
            .Concat(tree.EnumerateDepthFirst().Where(x => x.Item.Dependencies.Any(edge => replacements.ContainsKey(edge.Target))))
            .Where(x => !excluded.Contains(x))
            .Distinct()
            .Where(x => writer.MapCurrentModel(x, resourceMap).HasSucceeded)
            .ToList();
    }

    private async Task<MagitekResult> FinishRegenerationAsync(ProjectTree tree, List<(RegeneratedResource Resource, ResourceNode Node)> replaced,
        Dictionary<IProjectResource, IProjectResource> replacements, List<ResourceChange> removals)
    {
        var reasons = new List<string>();
        var skipped = removals.Select(x => x.ResourceNode).ToHashSet();

        foreach (var (resource, oldNode) in replaced)
        {
            if (!tree.TryGetNode(resource.TargetKey, out var newNode))
                continue;

            if (oldNode.DiskLocation is { } oldLocation && newNode.DiskLocation is { } newLocation && !string.Equals(oldLocation, newLocation, StringComparison.Ordinal))
            {
                try
                {
                    ProjectWorkspace.RemoveRenamedFile(oldLocation, newLocation);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    reasons.Add($"'{oldLocation}' could not be deleted and will reappear when the project is reopened: {ex.Message}");
                }
            }
        }

        workspace.RetargetReferences(tree, replacements, skipped, reasons);

        if (removals.Count > 0)
        {
            var deleteResult = await workspace.ApplyDeletionAsync(new ResourceDeletionPlan(tree, removals), _serializerFactory.GlobalResources.OfType<Palette>().First());
            if (deleteResult.HasFailed)
                reasons.Add(deleteResult.AsError.Reason);
        }

        return reasons.Count > 0 ? new MagitekResult.Failed(string.Join("\n\n", reasons)) : MagitekResult.SuccessResult;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> FindGenerationConflicts(ProjectTree tree, GenerationPlan plan)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(plan);

        var conflicts = new List<string>();
        var folderKeys = plan.FolderKeys.ToHashSet(StringComparer.Ordinal);
        var resourceKeys = plan.Resources.Select(x => x.Key);
        var movingAway = new HashSet<ResourceNode>();

        // A regeneration checks only the keys it writes to that are new, and a changed resource that moves frees its old key
        if (plan.Regeneration is { } regeneration)
        {
            var moving = regeneration.Written.Where(x => x.Kind == RegenerationKind.Added || x.TargetKey != x.Entry?.CurrentKey).ToList();
            resourceKeys = moving.Select(x => x.TargetKey);
            movingAway = FindNodes(tree, moving.Select(x => x.Entry?.CurrentKey)).ToHashSet();
        }

        foreach (var key in plan.FolderKeys.Concat(resourceKeys))
        {
            var isFolder = folderKeys.Contains(key);
            var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            ResourceNode? parent = tree.Root;
            foreach (var segment in segments[..^1])
                parent = parent?.ChildNodes.FirstOrDefault(x => x.Name == segment);

            if (parent is null)
                continue;

            var name = segments[^1];
            var sibling = parent.ChildNodes.FirstOrDefault(x => ResourceName.AreSame(x.Name, name) && !movingAway.Contains(x));
            if (sibling is not null && !(isFolder && sibling is ResourceFolderNode && sibling.Name == name))
                conflicts.Add($"'{key}' clashes with '{tree.CreatePathKey(sibling)}'");
            else if (!isFolder && parent is ProjectNode && ResourceName.AreSame(name, tree.Name))
                conflicts.Add($"'{key}' cannot share the project's name '{tree.Name}'");
        }

        return conflicts;
    }

    private static void EnsureFolder(ProjectTree tree, string folderKey, List<ResourceNode> createdFolders, List<string> createdDirectories)
    {
        ResourceNode parent = tree.Root;

        foreach (var segment in folderKey.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (parent.ChildNodes.FirstOrDefault(x => x.Name == segment) is { } existing)
            {
                parent = existing;
                continue;
            }

            var node = new ResourceFolderNode(segment, new ResourceFolder(segment));
            node.DiskLocation = ResourceFileLocator.LocateByParent(tree, parent, node);

            if (!Directory.Exists(node.DiskLocation))
            {
                Directory.CreateDirectory(node.DiskLocation);
                createdDirectories.Add(node.DiskLocation);
            }

            parent.AttachChildNode(node);
            createdFolders.Add(node);
            parent = node;
        }
    }

    private static void RollBackGeneration(ProjectTree tree, IEnumerable<string> keys, List<ResourceNode> createdFolders, List<string> createdDirectories,
        IReadOnlySet<ResourceNode>? swappedBack = null)
    {
        var created = createdFolders.ToHashSet();

        foreach (var key in keys.Reverse())
        {
            if (!tree.TryGetNode(key, out var node))
                continue;

            // A node swapped back needs its own Removed to pair into the reverse Replaced, even under a folder this run created
            if (node.Parent is { } parent && (!created.Contains(parent) || swappedBack?.Contains(node) == true))
                parent.RemoveChildNode(node.Name);

            if (node.Item is IDisposable disposable)
                disposable.Dispose();
        }

        foreach (var folder in Enumerable.Reverse(createdFolders))
        {
            if (folder.Parent is { } parent && !created.Contains(parent))
                parent.RemoveChildNode(folder.Name);
        }

        foreach (var directory in Enumerable.Reverse(createdDirectories))
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An empty directory left behind loads as an empty folder and loses nothing
            }
        }
    }
}
