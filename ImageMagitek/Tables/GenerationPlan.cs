using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Tables;

/// <summary>
/// One resource generation would create, with its model and the record it comes from
/// </summary>
/// <param name="Key">Tree path key the resource is created at</param>
/// <param name="ParentKey">Tree path key of its folder, '/' for the project root</param>
/// <param name="Type">The resource file's root element name</param>
/// <param name="Path">The record path of the record it comes from (LIB-TABLES-160)</param>
/// <param name="Summary">A size summary such as '16 colors' or '24x20 blocks'</param>
public sealed record PlannedResource(string Key, string ParentKey, string Name, string Type, ResourceModel Model, string OutputId,
    string Record, long Offset, int? Index, int? Entries, string Path, string Summary);

/// <summary>
/// What a generation would create and why records were left out (LIB-TABLES-121)
/// </summary>
public sealed class GenerationReport
{
    public required IReadOnlyList<(string Type, int Count)> Counts { get; init; }
    public required IReadOnlyList<SkippedRecord> Skipped { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// One '<paramref name="verb"/> N type' line per resource type (CLI-COMMANDS-072)
    /// </summary>
    public IEnumerable<string> CountLines(string verb) => Counts.Select(x => $"{verb} {x.Count} {x.Type}");

    /// <summary>
    /// The skipped records, then each warning prefixed with 'Warning: '
    /// </summary>
    public IEnumerable<string> IssueLines() =>
        Skipped.Select(x => x.ToString()).Concat(Warnings.Select(x => $"Warning: {x}"));
}

/// <summary>
/// The resources, folders, report and manifest entry of one generation, built without writing anything (LIB-TABLES-120)
/// </summary>
public sealed record GenerationPlan
{
    public required string DataFileKey { get; init; }
    public required string ManifestFileName { get; init; }
    public required string ManifestSchemaFileName { get; init; }

    /// <summary>
    /// Resources in build order: compressed data, palettes, arrangers, metatile sets, tilemaps, metatile maps
    /// </summary>
    public required IReadOnlyList<PlannedResource> Resources { get; init; }

    /// <summary>
    /// Keys of every folder the resources go in, the target folder first and parents before children
    /// </summary>
    public required IReadOnlyList<string> FolderKeys { get; init; }

    public required GenerationReport Report { get; init; }
    public required TableManifestGeneration ManifestGeneration { get; init; }

    /// <summary>
    /// How the plan matches the manifest's earlier generation into the same folder, null for a first generation (LIB-PROJECT-SERVICE-073)
    /// </summary>
    public RegenerationPlan? Regeneration { get; init; }

    public PlannedResource? Find(string key) => Resources.FirstOrDefault(x => x.Key == key);
}
