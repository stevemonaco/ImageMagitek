using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// The resource kinds a project can hold, in the order a project load builds them (LIB-PROJECT-FORMAT-021)
/// </summary>
public sealed class ResourceKindRegistry
{
    /// <summary>
    /// The built-in kinds: data file, compressed data, palette, arranger, metatile set, tilemap and metatile map
    /// </summary>
    public static ResourceKindRegistry BuiltIn { get; } = new(
    [
        new DataFileKind(), new CompressedDataKind(), new PaletteKind(), new ScatteredArrangerKind(),
        new MetatileSetKind(), new TilemapKind(), new MetatileMapKind()
    ]);

    public IReadOnlyList<ResourceKind> Kinds { get; }

    private readonly Dictionary<string, ResourceKind> _byElement = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ResourceKind> _byModel = [];
    private readonly Dictionary<Type, ResourceKind> _byNode = [];
    private readonly Dictionary<Type, ResourceKind> _byResource = [];

    /// <param name="kinds">The kinds in build order</param>
    /// <exception cref="ArgumentException">Two kinds share an element name, model, node or resource type</exception>
    public ResourceKindRegistry(IEnumerable<ResourceKind> kinds)
    {
        Kinds = kinds.ToList();

        foreach (var kind in Kinds)
        {
            Add(_byElement, kind.ElementName, kind, "element name");
            Add(_byModel, kind.ModelType, kind, "model type");
            Add(_byNode, kind.NodeType, kind, "node type");
            Add(_byResource, kind.ResourceType, kind, "resource type");
        }
    }

    public ResourceKind? FindByElement(string elementName) => _byElement.GetValueOrDefault(elementName);

    public ResourceKind? FindByModel(ResourceModel model) => _byModel.GetValueOrDefault(model.GetType());

    public ResourceKind? FindByNode(ResourceNode node) => _byNode.GetValueOrDefault(node.GetType());

    /// <summary>
    /// Finds the kind of <paramref name="resource"/>'s type or its nearest base type that a kind names
    /// </summary>
    public ResourceKind? FindByResource(IProjectResource resource)
    {
        for (var type = resource.GetType(); type is not null; type = type.BaseType)
        {
            if (_byResource.TryGetValue(type, out var kind))
                return kind;
        }

        return null;
    }

    private static void Add<TKey>(Dictionary<TKey, ResourceKind> map, TKey key, ResourceKind kind, string description) where TKey : notnull
    {
        if (!map.TryAdd(key, kind))
            throw new ArgumentException($"Resource kinds '{map[key].ElementName}' and '{kind.ElementName}' share the {description} '{key}'");
    }
}
