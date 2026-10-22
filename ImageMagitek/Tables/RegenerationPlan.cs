using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Tables;

public enum RegenerationKind { Added, Changed, Unchanged }

/// <summary>
/// An earlier generation's manifest entry with the resource found for it (LIB-PROJECT-SERVICE-073)
/// </summary>
/// <param name="CurrentKey">The found resource's key, or null when it is missing</param>
/// <param name="CurrentModel">The found resource's current model, or null when it is missing or cannot be mapped</param>
/// <param name="IsEdited">Whether the user changed the resource since it was generated (LIB-PROJECT-SERVICE-075)</param>
public sealed record RegenerationEntry(TableManifestResource Entry, string? CurrentKey, ResourceModel? CurrentModel, bool IsEdited);

/// <summary>
/// A planned resource with its regenerated model, at the key regeneration writes or keeps it at
/// </summary>
/// <param name="Model">The planned model with its references rewritten to target keys (LIB-TABLES-163)</param>
/// <param name="Entry">The matched entry, or null for an added resource</param>
/// <param name="ChangeKinds">How a changed resource differs (LIB-TABLES-164), empty otherwise</param>
public sealed record RegeneratedResource(PlannedResource Planned, RegenerationKind Kind, string TargetKey, ResourceModel Model,
    RegenerationEntry? Entry, IReadOnlyList<string> ChangeKinds)
{
    public string ParentKey => RegenerationPlan.ParentKeyOf(TargetKey);
    public string Name => RegenerationPlan.NameOf(TargetKey);
}

/// <summary>
/// An entry whose resource regeneration leaves as it is, with its report line
/// </summary>
/// <param name="Key">The resource's current key, or the entry's key when it is missing</param>
/// <param name="Reason">Why it is kept: edited, missing, referenced or references deleted</param>
/// <param name="UsedBy">For a referenced resource, the keys of the resources that use it</param>
/// <param name="Deleted">For a resource that references a deleted one, the deleted resource's entry key (LIB-TABLES-167)</param>
public sealed record KeptResource(TableManifestResource Entry, string Key, string Reason, IReadOnlyList<string>? UsedBy = null, string? Deleted = null);

/// <summary>
/// An added resource that is not generated because it references a deleted resource, directly or through another skipped one (LIB-TABLES-167)
/// </summary>
/// <param name="Deleted">The deleted resource's entry key</param>
public sealed record SkippedResource(PlannedResource Planned, string Deleted);

/// <summary>
/// A plan matched against the manifest's earlier generation: what is added, changed, unchanged, removed and kept (LIB-TABLES-161 to -166)
/// </summary>
public sealed class RegenerationPlan
{
    /// <summary>
    /// Added, changed and unchanged resources in build order
    /// </summary>
    public IReadOnlyList<RegeneratedResource> Resources { get; }

    public IReadOnlyList<RegenerationEntry> Removed { get; }
    public IReadOnlyList<KeptResource> Kept { get; }
    public IReadOnlyList<SkippedResource> Skipped { get; }

    public IEnumerable<RegeneratedResource> Written => Resources.Where(x => x.Kind != RegenerationKind.Unchanged);

    public static string ParentKeyOf(string key) => key.LastIndexOf('/') is var slash and > 0 ? key[..slash] : "/";
    public static string NameOf(string key) => key[(key.LastIndexOf('/') + 1)..];

    private RegenerationPlan(IReadOnlyList<RegeneratedResource> resources, IReadOnlyList<RegenerationEntry> removed, IReadOnlyList<KeptResource> kept,
        IReadOnlyList<SkippedResource> skipped)
    {
        Resources = resources;
        Removed = removed;
        Kept = kept;
        Skipped = skipped;
    }

    /// <summary>
    /// Matches <paramref name="plan"/> to the entries by record path, or by identity for entries without one, and classifies every resource
    /// </summary>
    /// <param name="overwriteEdited">Also change or remove edited resources instead of keeping them</param>
    public static RegenerationPlan Match(GenerationPlan plan, IReadOnlyList<RegenerationEntry> entries, bool overwriteEdited)
    {
        var byPath = entries.Where(x => x.Entry.Path is not null)
            .ToLookup(x => (x.Entry.Type, x.Entry.Output, x.Entry.Path, x.Entry.Index));
        var byIdentity = entries.ToLookup(x => (x.Entry.Record, x.Entry.Offset, x.Entry.Output, x.Entry.Entries, x.Entry.Index));
        var matched = new HashSet<RegenerationEntry>(ReferenceEqualityComparer.Instance);
        var entryOf = new Dictionary<PlannedResource, RegenerationEntry>(ReferenceEqualityComparer.Instance);

        // A deduplicated palette's path is whichever record planned it first, so a palette at the same address is matched before any path
        foreach (var planned in plan.Resources.Where(x => x.Type == "palette"))
        {
            if (byIdentity[(planned.Record, planned.Offset, planned.OutputId, planned.Entries, planned.Index)].FirstOrDefault(x => !matched.Contains(x)) is { } entry)
            {
                matched.Add(entry);
                entryOf[planned] = entry;
            }
        }

        var pairs = new List<(PlannedResource Planned, RegenerationEntry? Entry)>();

        foreach (var planned in plan.Resources)
        {
            var entry = entryOf.GetValueOrDefault(planned) ?? byPath[(planned.Type, planned.OutputId, planned.Path, planned.Index)]
                .Concat(byIdentity[(planned.Record, planned.Offset, planned.OutputId, planned.Entries, planned.Index)].Where(x => x.Entry.Path is null))
                .FirstOrDefault(x => !matched.Contains(x));

            if (entry is not null)
                matched.Add(entry);

            pairs.Add((planned, entry));
        }

        var targetKeys = pairs.ToDictionary(x => x.Planned.Key, x => TargetKey(x.Planned, x.Entry, overwriteEdited));
        var resources = new List<RegeneratedResource>();
        var kept = new List<KeptResource>();
        var skipped = new List<SkippedResource>();
        var deleted = pairs.Where(x => x.Entry is { CurrentKey: null }).ToDictionary(x => x.Planned.Key, x => x.Entry!.Entry.Key);

        // Build order puts references first, so a skip or keep here is seen by every later resource
        foreach (var (planned, entry) in pairs)
        {
            if (entry is { CurrentKey: null })
            {
                kept.Add(Missing(entry));
                continue;
            }

            if (ReferencedKeys(planned.Model).FirstOrDefault(deleted.ContainsKey) is { } reference)
            {
                if (entry is null)
                {
                    skipped.Add(new SkippedResource(planned, deleted[reference]));
                    deleted[planned.Key] = deleted[reference];
                }
                else
                {
                    kept.Add(new KeptResource(entry.Entry, entry.CurrentKey!, "references deleted", Deleted: deleted[reference]));
                    targetKeys[planned.Key] = entry.CurrentKey!;
                }

                continue;
            }

            var target = targetKeys[planned.Key];
            var model = WithKeys(planned.Model, NameOf(target), key => targetKeys.GetValueOrDefault(key, key));

            if (entry is null)
            {
                resources.Add(new(planned, RegenerationKind.Added, target, model, null, []));
            }
            else if (model == entry.CurrentModel && target == entry.CurrentKey)
            {
                resources.Add(new(planned, RegenerationKind.Unchanged, target, model, entry, []));
            }
            else if (entry.IsEdited && !overwriteEdited)
            {
                kept.Add(Edited(entry));
            }
            else
            {
                resources.Add(new(planned, RegenerationKind.Changed, target, model, entry, DescribeChanges(model, entry.CurrentModel)));
            }
        }

        var removed = new List<RegenerationEntry>();
        foreach (var entry in entries.Where(x => !matched.Contains(x)))
        {
            if (entry.CurrentKey is null)
                kept.Add(Missing(entry));
            else if (entry.IsEdited && !overwriteEdited)
                kept.Add(Edited(entry));
            else
                removed.Add(entry);
        }

        return new RegenerationPlan(resources, removed, kept, skipped);

        static KeptResource Missing(RegenerationEntry entry) => new(entry.Entry, entry.Entry.Key, "missing");
        static KeptResource Edited(RegenerationEntry entry) => new(entry.Entry, entry.CurrentKey!, "edited");
    }

    private static string TargetKey(PlannedResource planned, RegenerationEntry? entry, bool overwriteEdited) => entry switch
    {
        null => planned.Key,
        { CurrentKey: null } => entry.Entry.Key,
        _ when entry.CurrentKey != entry.Entry.Key || (entry.IsEdited && !overwriteEdited) => entry.CurrentKey,
        _ => planned.Key
    };

    /// <summary>
    /// Moves a removed resource to the kept ones because deleting it would affect <paramref name="usedBy"/> (LIB-PROJECT-SERVICE-077)
    /// </summary>
    public RegenerationPlan KeepReferenced(RegenerationEntry removed, IReadOnlyList<string> usedBy) =>
        new(Resources,
            Removed.Where(x => !ReferenceEquals(x, removed)).ToList(),
            Kept.Append(new KeptResource(removed.Entry, removed.CurrentKey!, "referenced", usedBy)).ToList(),
            Skipped);

    /// <summary>
    /// The regeneration report (LIB-TABLES-165), with "Would add", "Would change" and "Would remove" for a dry run (CLI-COMMANDS-077)
    /// </summary>
    public IEnumerable<string> ReportLines(GenerationReport report, bool dryRun) => SummaryLines(dryRun).Concat(report.IssueLines());

    /// <summary>
    /// The regeneration report without the skipped records and warnings
    /// </summary>
    public IEnumerable<string> SummaryLines(bool dryRun)
    {
        var (added, changed, removed) = dryRun ? ("Would add", "Would change", "Would remove") : ("Added", "Changed", "Removed");

        foreach (var type in TableGenerator.BuildOrderTypeNames)
        {
            var counts = new[]
            {
                (added, Resources.Count(x => x.Kind == RegenerationKind.Added && x.Planned.Type == type)),
                (changed, Resources.Count(x => x.Kind == RegenerationKind.Changed && x.Planned.Type == type)),
                ("Unchanged", Resources.Count(x => x.Kind == RegenerationKind.Unchanged && x.Planned.Type == type)),
                (removed, Removed.Count(x => x.Entry.Type == type))
            };

            foreach (var (verb, count) in counts.Where(x => x.Item2 > 0))
                yield return $"{verb} {count} {type}";
        }

        foreach (var resource in Resources.Where(x => x.Kind == RegenerationKind.Changed))
            yield return $"{changed} {resource.TargetKey}: {string.Join(", ", resource.ChangeKinds)}";

        foreach (var entry in Removed)
            yield return $"{removed} {entry.CurrentKey}";

        foreach (var kept in Kept)
        {
            yield return kept switch
            {
                { Deleted: { } deleted } => $"Kept {kept.Key}: references deleted {deleted}",
                { UsedBy: { } usedBy } => $"Kept {kept.Reason} {kept.Key}: used by {string.Join(", ", usedBy)}",
                _ => $"Kept {kept.Reason} {kept.Key}"
            };
        }

        foreach (var skipped in Skipped)
            yield return $"Skipped {skipped.Planned.Key}: references deleted {skipped.Deleted}";
    }

    /// <summary>
    /// The manifest generation after regeneration: written and unchanged resources at their target keys with their new hashes,
    /// then kept resources with their old entries at their current keys (LIB-TABLES-166)
    /// </summary>
    /// <param name="hashes">The hash of every written and unchanged resource, by target key; a resource without one is listed without a hash</param>
    public TableManifestGeneration ToManifestGeneration(TableManifestGeneration planned, IReadOnlyDictionary<string, string> hashes)
    {
        var resources = Resources
            .Select(x => new TableManifestResource(x.TargetKey, x.Planned.Type, x.Planned.OutputId, x.Planned.Record, x.Planned.Offset,
                x.Planned.Entries, x.Planned.Index, x.Planned.Path, hashes.GetValueOrDefault(x.TargetKey)))
            .Concat(Kept.Select(x => x.Entry with { Key = x.Key }))
            .ToList();

        return planned with { Resources = resources };
    }

    /// <summary>
    /// Every key <paramref name="model"/> references: data files, compressed data, palettes and metatile sets
    /// </summary>
    public static IReadOnlyList<string> ReferencedKeys(ResourceModel model)
    {
        var keys = new List<string>();
        WithKeys(model, model.Name, key =>
        {
            keys.Add(key);
            return key;
        });

        return keys;
    }

    private static List<string> DescribeChanges(ResourceModel model, ResourceModel? current)
    {
        if (current is null)
            return ["changed"];

        var kinds = new List<string>();

        if (!Offsets(model).Order().SequenceEqual(Offsets(current).Order()))
            kinds.Add("repointed");

        if (!PaletteKeys(model).Order(StringComparer.Ordinal).SequenceEqual(PaletteKeys(current).Order(StringComparer.Ordinal)))
            kinds.Add("re-paletted");

        if (TableGenerator.Summarize(model) != TableGenerator.Summarize(current))
            kinds.Add("resized");

        if (kinds.Count == 0)
            kinds.Add("changed");

        return kinds;
    }

    private static IEnumerable<long> Offsets(ResourceModel model) => model switch
    {
        CompressedDataModel compressed => [compressed.FileOffset],
        PaletteModel palette => palette.ColorSources.OfType<FileColorSourceModel>().Select(x => x.FileAddress),
        MetatileSetModel set => set.AttributeDataKey is null ? [set.MetatileOffset, set.TileOffset] : [set.MetatileOffset, set.TileOffset, set.AttributeOffset],
        MetatileMapModel map => [map.MapOffset],
        TilemapModel tilemap => [tilemap.MapOffset, tilemap.TileOffset],
        ScatteredArrangerModel arranger => arranger.EnumerateElements().OfType<ArrangerElementModel>()
            .Select(x => x.FileAddress.ByteOffset * 8 + x.FileAddress.BitOffset),
        _ => []
    };

    private static IEnumerable<string> PaletteKeys(ResourceModel model) => model switch
    {
        MetatileSetModel set => set.PaletteSlots.Select(x => $"{x.Key}:{x.Value}"),
        TilemapModel tilemap => tilemap.PaletteSlots.Select(x => $"{x.Key}:{x.Value}"),
        ScatteredArrangerModel arranger => arranger.EnumerateElements().OfType<ArrangerElementModel>().Select(x => x.PaletteKey ?? ""),
        _ => []
    };

    /// <summary>
    /// Copies <paramref name="model"/> with the name <paramref name="name"/> and every key it references passed through <paramref name="map"/>
    /// </summary>
    public static ResourceModel WithKeys(ResourceModel model, string name, Func<string, string> map) => model switch
    {
        CompressedDataModel x => x with { Name = name, ParentKey = map(x.ParentKey) },
        PaletteModel x => x with { Name = name, DataFileKey = x.DataFileKey is null ? null : map(x.DataFileKey) },
        MetatileSetModel x => x with
        {
            Name = name,
            MetatileDataKey = map(x.MetatileDataKey),
            TileDataKey = map(x.TileDataKey),
            PaletteSlots = x.PaletteSlots.ToDictionary(slot => slot.Key, slot => map(slot.Value)),
            AttributeDataKey = x.AttributeDataKey is null ? null : map(x.AttributeDataKey)
        },
        MetatileMapModel x => x with
        {
            Name = name,
            MapDataKey = map(x.MapDataKey),
            Sets = x.Sets.Select(set => (map(set.SetKey), set.Base)).ToList()
        },
        TilemapModel x => x with
        {
            Name = name,
            MapDataKey = map(x.MapDataKey),
            TileDataKey = map(x.TileDataKey),
            TilePaletteKey = x.TilePaletteKey is null ? null : map(x.TilePaletteKey),
            PaletteSlots = x.PaletteSlots.ToDictionary(slot => slot.Key, slot => map(slot.Value))
        },
        ScatteredArrangerModel x => x with { Name = name, ElementGrid = MapGrid(x.ElementGrid, map) },
        _ => throw new NotSupportedException($"Regenerating a '{model.GetType().Name}' is not supported")
    };

    private static ArrangerElementModel[,] MapGrid(ArrangerElementModel[,] grid, Func<string, string> map)
    {
        var mapped = new ArrangerElementModel[grid.GetLength(0), grid.GetLength(1)];

        for (var y = 0; y < grid.GetLength(1); y++)
        {
            for (var x = 0; x < grid.GetLength(0); x++)
            {
                if (grid[x, y] is not { } el)
                    continue;

                mapped[x, y] = el with { DataFileKey = map(el.DataFileKey), PaletteKey = el.PaletteKey is null ? null : map(el.PaletteKey) };
            }
        }

        return mapped;
    }
}
