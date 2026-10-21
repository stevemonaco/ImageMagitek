using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Tables;

/// <summary>
/// Turns a definition's record instances into a plan of project resource models with names, folders, a report and a manifest entry
/// (LIB-TABLES-070 to -122). Planning reads the data source and writes nothing.
/// </summary>
public sealed class TableGenerator
{
    private const char CounterMarker = '\u0001';
    private const int MaxRepeat = 256;
    private const int MaxArrangerElements = 65536;

    private static readonly OutputType[] _buildOrder =
        [OutputType.CompressedData, OutputType.Palette, OutputType.Arranger, OutputType.MetatileSet, OutputType.Tilemap, OutputType.MetatileMap];

    private readonly TableDefinition _definition;
    private readonly DataSource _source;
    private readonly string _dataFileKey;
    private readonly string _targetFolderKey;
    private readonly IMapFormatFactory _mapFormats;

    private TableData _data = null!;
    private readonly List<SkippedRecord> _skipped = [];
    private readonly List<Candidate> _candidates = [];
    private readonly Dictionary<(OutputDefinition, RecordInstance, long), (Candidate Candidate, long? Slot)> _resources = [];
    private readonly HashSet<(OutputDefinition, RecordInstance)> _generated = [];
    private readonly Dictionary<(string, RecordInstance), string> _names = [];
    private readonly Dictionary<(long, int, string, string), Candidate> _palettes = [];
    private readonly Dictionary<string, Namespace> _namespaces = [];
    private readonly List<string> _folderKeys = [];

    /// <param name="dataFileKey">Tree path key of the data file the models read</param>
    /// <param name="targetFolderKey">Tree path key of the folder resources are generated under, '/' for the project root</param>
    public TableGenerator(TableDefinition definition, DataSource source, string dataFileKey, string targetFolderKey, IMapFormatFactory mapFormats)
    {
        _definition = definition;
        _source = source;
        _dataFileKey = dataFileKey;
        _targetFolderKey = targetFolderKey;
        _mapFormats = mapFormats;
    }

    private static string ChildKey(string parentKey, string name) => parentKey == "/" ? $"/{name}" : $"{parentKey}/{name}";

    /// <summary>
    /// The manifest beside a definition: <c>&lt;stem&gt;.manifest.xml</c> without <c>.table.xml</c> or <c>.xml</c> (LIB-TABLES-122)
    /// </summary>
    public static string LocateManifest(string definitionFileName)
    {
        var fullName = Path.GetFullPath(definitionFileName);
        var fileName = Path.GetFileName(fullName);
        var stem = fileName.EndsWith(".table.xml", StringComparison.OrdinalIgnoreCase) ? fileName[..^".table.xml".Length]
            : fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? fileName[..^".xml".Length]
            : fileName;

        return Path.Combine(Path.GetDirectoryName(fullName)!, $"{stem}.manifest.xml");
    }

    public GenerationPlan Plan(string projectFileName)
    {
        var evaluation = new TableEvaluator(_definition, _source).Evaluate();
        _data = evaluation.Data;
        _skipped.AddRange(evaluation.Skipped);

        _namespaces[_targetFolderKey] = new Namespace();
        _folderKeys.Add(_targetFolderKey);

        foreach (var output in _definition.Outputs)
            CreateCandidates(evaluation, output);

        foreach (var output in _definition.Outputs)
            NameCandidates(output);

        foreach (var type in _buildOrder)
        {
            foreach (var candidate in _candidates.Where(x => x.Output.Type == type && !x.IsSkipped))
                BuildModel(candidate);
        }

        CheckMetatileCounts();

        var resources = _buildOrder
            .SelectMany(type => _candidates.Where(x => x.Output.Type == type && !x.IsSkipped))
            .Select(ToPlannedResource)
            .ToList();

        var manifestFileName = LocateManifest(_definition.FileName);
        var manifestDirectory = Path.GetDirectoryName(manifestFileName)!;

        return new GenerationPlan
        {
            DataFileKey = _dataFileKey,
            ManifestFileName = manifestFileName,
            ManifestSchemaFileName = Path.Combine(_definition.SchemaDirectory, XmlTableDefinitionReader.ManifestSchemaFileName),
            Resources = resources,
            FolderKeys = UsedFolderKeys(resources),
            Report = new GenerationReport
            {
                Counts = resources.GroupBy(x => x.Type).Select(x => (x.Key, x.Count())).ToList(),
                Skipped = _skipped,
                Warnings = _data.Warnings
            },
            ManifestGeneration = new TableManifestGeneration(
                RelativePath(manifestDirectory, projectFileName),
                _targetFolderKey,
                _dataFileKey,
                RelativePath(manifestDirectory, _definition.FileName),
                resources.Select(x => new TableManifestResource(x.Key, x.Type, x.OutputId, x.Record, x.Offset, x.Entries, x.Index)).ToList())
        };
    }

    private List<string> UsedFolderKeys(IEnumerable<PlannedResource> resources)
    {
        var used = new HashSet<string> { _targetFolderKey };
        foreach (var resource in resources)
        {
            for (var key = resource.ParentKey; used.Add(key);)
                key = key.LastIndexOf('/') is var slash and > 0 ? key[..slash] : "/";
        }

        return _folderKeys.Where(used.Contains).ToList();
    }

    private static string RelativePath(string directory, string fileName) =>
        Path.GetRelativePath(directory, Path.GetFullPath(fileName)).Replace('\\', '/');

    private sealed class Candidate(OutputDefinition output, RecordInstance instance, long i, EvaluationScope scope)
    {
        public OutputDefinition Output { get; } = output;
        public RecordInstance Instance { get; } = instance;
        public long I { get; } = i;
        public EvaluationScope Scope { get; } = scope;
        public string ParentKey { get; set; } = "/";
        public string Name { get; set; } = "";
        public string Key => ChildKey(ParentKey, Name);
        public long PaletteOffset { get; set; }
        public int PaletteEntries { get; set; }
        public ResourceModel? Model { get; set; }
        public string Summary { get; set; } = "";
        public int? RaisedCount { get; set; }
        public bool IsSkipped { get; set; }
    }

    // Names taken in one folder, compared as LIB-PROJECT-TREE-031 compares siblings
    private sealed class Namespace
    {
        private readonly Dictionary<string, string> _taken = new(StringComparer.OrdinalIgnoreCase);

        public bool Contains(string name) => _taken.ContainsKey(ResourceName.Normalize(name));

        public string TakeOrFind(string name)
        {
            if (_taken.TryGetValue(ResourceName.Normalize(name), out var existing))
                return existing;

            _taken[ResourceName.Normalize(name)] = name;
            return name;
        }
    }

    private void Skip(RecordInstance instance, OutputDefinition output, string reason) =>
        _skipped.Add(new SkippedRecord(instance.Type.Name, instance.Offset, $"output '{output.Id}': {reason}"));

    private RecordInstance? SelectContext(OutputDefinition output, RecordInstance instance)
    {
        if (output.Context is null)
            return null;

        var ancestors = instance.FindAncestors(output.Context);
        if (ancestors.Count == 0 || output.ChooseMode is not { } mode)
            return ancestors.FirstOrDefault();

        var values = new List<(RecordInstance Ancestor, long Value)>();
        foreach (var ancestor in ancestors)
        {
            var result = mode.TryEvaluate(EvaluationScope.For(_data, ancestor));
            if (result.HasSucceeded)
                values.Add((ancestor, result.AsSuccess.Result));
            else
                _data.Warn($"Output '{output.Id}' context {ancestor}: {result.AsError.Reason}");
        }

        if (values.Count == 0)
            return ancestors[0];

        var counts = values.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Count());
        var most = counts.Values.Max();
        return values.First(x => counts[x.Value] == most).Ancestor;
    }

    private void CreateCandidates(TableEvaluation evaluation, OutputDefinition output)
    {
        foreach (var instance in evaluation.InstancesOf(output.Record))
        {
            if (output.Variant is not null && instance.Variant?.Name != output.Variant)
                continue;

            var context = SelectContext(output, instance);
            var created = new List<(Candidate Candidate, long? Slot)>();
            var newPalettes = new Dictionary<(long, int, string, string), Candidate>();

            try
            {
                var baseScope = EvaluationScope.For(_data, instance, 0, null, output.Context, context);
                if (output.When is { } when && !when.IsTrue(baseScope))
                    continue;

                var repeat = output.Repeat?.Evaluate(baseScope) ?? 1;
                if (repeat is < 0 or > MaxRepeat)
                    throw new TableEvaluationException($"repeat {repeat} is outside 0 to {MaxRepeat}");

                for (long i = 0; i < repeat; i++)
                {
                    var scope = EvaluationScope.For(_data, instance, i, null, output.Context, context);
                    if (output.Type != OutputType.Palette)
                    {
                        created.Add((new Candidate(output, instance, i, scope), null));
                        continue;
                    }

                    var slot = output.Expression("slot")?.Evaluate(scope) ?? i;
                    scope = EvaluationScope.For(_data, instance, i, slot, output.Context, context);

                    var offset = output.Expression("offset")!.Evaluate(scope);
                    var entries = output.Expression("entries")!.Evaluate(scope);
                    if (entries is < 1 or > Palette.MaxEntries)
                        throw new TableEvaluationException($"palette entries {entries} is outside 1 to {Palette.MaxEntries}");

                    var paletteKey = (offset, (int)entries, output.Value("color")!, output.Value("zeroindextransparent") ?? "false");
                    if (!_palettes.TryGetValue(paletteKey, out var palette) && !newPalettes.TryGetValue(paletteKey, out palette))
                    {
                        palette = new Candidate(output, instance, i, scope) { PaletteOffset = offset, PaletteEntries = (int)entries };
                        newPalettes[paletteKey] = palette;
                    }

                    created.Add((palette, slot));
                }

                foreach (var candidate in created.Select(x => x.Candidate).Where(IsNew).Distinct())
                    candidate.ParentKey = RegisterFolder(candidate);
            }
            catch (TableEvaluationException ex)
            {
                Skip(instance, output, ex.Message);
                continue;
            }

            _generated.Add((output, instance));
            foreach (var (key, palette) in newPalettes)
                _palettes[key] = palette;

            for (var i = 0; i < created.Count; i++)
            {
                var (candidate, slot) = created[i];
                if (IsNew(candidate) && !_candidates.Contains(candidate))
                    _candidates.Add(candidate);

                _resources[(output, instance, i)] = (candidate, slot);
            }

            bool IsNew(Candidate candidate) => candidate.Instance == instance && candidate.Output == output;
        }
    }

    private string RegisterFolder(Candidate candidate)
    {
        var parentKey = _targetFolderKey;
        var template = FirstMatch(candidate.Output.Folders, candidate.Scope);
        if (template is null)
            return parentKey;

        var path = Render(template, candidate, null);
        foreach (var segment in path.Split('/'))
        {
            var name = Sanitize(segment);
            if (name.Length == 0)
                continue;

            if (ResourceName.Validate(name, true).HasFailed)
            {
                _data.Warn($"Output '{candidate.Output.Id}' folder segment '{segment}' is not a valid folder name and was left out");
                continue;
            }

            var spelling = _namespaces[parentKey].TakeOrFind(name);
            var key = ChildKey(parentKey, spelling);
            if (_namespaces.TryAdd(key, new Namespace()))
                _folderKeys.Add(key);

            parentKey = key;
        }

        return parentKey;
    }

    private ConditionalTemplate? FirstMatch(IReadOnlyList<ConditionalTemplate> templates, EvaluationScope scope) =>
        templates.FirstOrDefault(x => x.When is null || x.When.IsTrue(scope));

    private string Render(ConditionalTemplate template, Candidate candidate, string? counter)
    {
        var builder = new StringBuilder();

        foreach (var segment in template.Template.Segments)
        {
            switch (segment)
            {
                case NameTemplate.LiteralSegment literal:
                    builder.Append(literal.Text);
                    break;
                case NameTemplate.ExpressionSegment expression:
                    builder.Append(NameTemplate.FormatNumber(expression.Expression.Evaluate(candidate.Scope), expression.Format, expression.Digits));
                    break;
                case NameTemplate.NameTableSegment table:
                    builder.Append(_data.LookupName(table.Table, table.Value.Evaluate(candidate.Scope)));
                    break;
                case NameTemplate.OutputNameSegment outputName:
                    builder.Append(_names.TryGetValue((outputName.OutputId, candidate.Instance), out var name)
                        ? name
                        : throw new TableEvaluationException($"output '{outputName.OutputId}' generated no resource for {candidate.Instance}"));
                    break;
                case NameTemplate.CounterSegment:
                    builder.Append(counter ?? CounterMarker.ToString());
                    break;
            }
        }

        return builder.ToString();
    }

    private string FallbackName(Candidate candidate) =>
        $"{candidate.Output.Id} {(candidate.Output.Type == OutputType.Palette ? candidate.PaletteOffset : candidate.Instance.Offset):X6}";

    /// <summary>
    /// Removes forbidden and control characters and trims leading and trailing whitespace and '.' (LIB-TABLES-083)
    /// </summary>
    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c >= ' ' && "<>:\"/\\|?*".IndexOf(c) < 0)
                builder.Append(c);
        }

        return builder.ToString().TrimStart().TrimStart('.').TrimStart().TrimEnd().TrimEnd('.').TrimEnd();
    }

    private void NameCandidates(OutputDefinition output)
    {
        var rendered = new List<(Candidate Candidate, string Text)>();

        foreach (var candidate in _candidates.Where(x => x.Output == output && !x.IsSkipped))
        {
            try
            {
                var template = FirstMatch(output.Names, candidate.Scope);
                rendered.Add((candidate, template is null ? FallbackName(candidate) : Render(template, candidate, null)));
            }
            catch (TableEvaluationException ex)
            {
                candidate.IsSkipped = true;
                Skip(candidate.Instance, output, ex.Message);
            }
        }

        var counters = new Dictionary<(string, string), int>();
        foreach (var (candidate, text) in rendered)
        {
            var name = text;
            if (text.Contains(CounterMarker))
            {
                var group = (candidate.ParentKey, text);
                counters[group] = counters.GetValueOrDefault(group) + 1;
                name = text.Replace(CounterMarker.ToString(), counters[group].ToString(CultureInfo.InvariantCulture));
            }

            name = Sanitize(name);
            if (name.Length == 0 || ResourceName.Validate(name, false).HasFailed)
                name = FallbackName(candidate);

            candidate.Name = TakeUniqueName(_namespaces[candidate.ParentKey], name, candidate);

            if (output.Repeat is null)
                _names[(output.Id, candidate.Instance)] = candidate.Name;
        }
    }

    private string TakeUniqueName(Namespace names, string name, Candidate candidate)
    {
        if (!names.Contains(name))
            return names.TakeOrFind(name);

        if (candidate.Output.Duplicates == DuplicateRule.Address)
        {
            var addressed = $"{name} {candidate.Instance.Offset:X6}";
            if (!names.Contains(addressed))
                return names.TakeOrFind(addressed);

            name = addressed;
        }

        for (var number = 2; ; number++)
        {
            var numbered = $"{name} {number}";
            if (!names.Contains(numbered))
                return names.TakeOrFind(numbered);
        }
    }

    private Candidate ResolveReference(string outputId, RecordInstance instance, string what)
    {
        var output = _definition.FindOutput(outputId)!;
        if (_resources.TryGetValue((output, instance, 0), out var entry) && entry.Candidate.Model is not null)
            return entry.Candidate;

        throw new TableEvaluationException($"{what} output '{outputId}' generated no resource for {instance}");
    }

    private (string Key, long Offset, int? Count) ResolveTiles(Candidate candidate)
    {
        var source = candidate.Output.Tiles.FirstOrDefault(x => x.When is null || x.When.IsTrue(candidate.Scope))
            ?? throw new TableEvaluationException("no 'tiles' element applies");

        if (source.OutputId is { } outputId)
            return (ResolveReference(outputId, candidate.Instance, "tiles").Key, 0, null);

        var count = source.Count?.Evaluate(candidate.Scope);
        if (count is < 1 or > int.MaxValue)
            throw new TableEvaluationException($"tile count {count} is not positive");

        return (_dataFileKey, source.Offset!.Evaluate(candidate.Scope), (int?)count);
    }

    private Dictionary<int, string> ResolvePaletteSlots(Candidate candidate)
    {
        var slots = new Dictionary<int, string>();
        if (candidate.Output.PaletteSlotsOutput is not { } outputId)
            return slots;

        var output = _definition.FindOutput(outputId)!;
        if (!_generated.Contains((output, candidate.Instance)))
            throw new TableEvaluationException($"palette output '{outputId}' generated no resource for {candidate.Instance}");

        foreach (var ((paletteOutput, instance, _), (palette, slot)) in _resources)
        {
            if (paletteOutput != output || instance != candidate.Instance)
                continue;

            if (palette.Model is null)
                throw new TableEvaluationException($"palette output '{outputId}' generated no resource for slot {slot}");

            if (slot is not (>= 0 and <= 255))
                throw new TableEvaluationException($"palette slot {slot} is outside 0 to 255");

            slots[(int)slot.Value] = palette.Key;
        }

        return slots;
    }

    private int PositiveInt(Candidate candidate, string attribute)
    {
        var value = candidate.Output.Expression(attribute)!.Evaluate(candidate.Scope);
        return value is >= 1 and <= int.MaxValue ? (int)value : throw new TableEvaluationException($"{attribute} {value} is not positive");
    }

    private int NonNegativeInt(Candidate candidate, string attribute)
    {
        var value = candidate.Output.Expression(attribute)?.Evaluate(candidate.Scope) ?? 0;
        return value is >= 0 and <= int.MaxValue ? (int)value : throw new TableEvaluationException($"{attribute} {value} is negative");
    }

    private long Offset(Candidate candidate)
    {
        var offset = candidate.Output.Expression("offset")!.Evaluate(candidate.Scope);
        return offset >= 0 ? offset : throw new TableEvaluationException($"offset {offset} is negative");
    }

    private static int IntValue(Candidate candidate, string attribute) =>
        int.Parse(candidate.Output.Value(attribute)!, CultureInfo.InvariantCulture);

    private void BuildModel(Candidate candidate)
    {
        var output = candidate.Output;

        try
        {
            switch (output.Type)
            {
                case OutputType.CompressedData:
                    candidate.Model = new CompressedDataModel
                    {
                        Name = candidate.Name,
                        ParentKey = _dataFileKey,
                        FileOffset = Offset(candidate),
                        Compression = output.Value("compression")!
                    };
                    candidate.Summary = output.Value("compression")!;
                    break;

                case OutputType.Palette:
                    candidate.Model = new PaletteModel
                    {
                        Name = candidate.Name,
                        ColorModel = Enum.Parse<ColorModel>(output.Value("color")!),
                        DataFileKey = _dataFileKey,
                        ZeroIndexTransparent = output.Value("zeroindextransparent") is "true" or "1",
                        ColorSources = [new FileColorSourceModel(candidate.PaletteOffset, candidate.PaletteEntries, output.Value("endian") == "big" ? Endian.Big : Endian.Little)]
                    };
                    candidate.Summary = $"{candidate.PaletteEntries} colors";
                    break;

                case OutputType.MetatileSet:
                {
                    var tiles = ResolveTiles(candidate);
                    var count = candidate.RaisedCount ?? PositiveInt(candidate, "count");
                    candidate.Model = new MetatileSetModel
                    {
                        Name = candidate.Name,
                        MetatileDataKey = _dataFileKey,
                        MetatileOffset = Offset(candidate),
                        MetatileCount = count,
                        MetatileWidth = IntValue(candidate, "metatilewidth"),
                        MetatileHeight = IntValue(candidate, "metatileheight"),
                        Layers = IntValue(candidate, "layers"),
                        FormatName = output.Value("format")!,
                        Order = MetatileOrderExtensions.FromXmlName(output.Value("order")),
                        TileDataKey = tiles.Key,
                        TileOffset = tiles.Offset,
                        CodecName = output.Value("codec")!,
                        FirstTile = NonNegativeInt(candidate, "firsttile"),
                        TileCount = tiles.Count,
                        PaletteSlots = ResolvePaletteSlots(candidate),
                        AttributeDataKey = output.Attributes is null ? null : _dataFileKey,
                        AttributeOffset = output.Attributes?.Offset.Evaluate(candidate.Scope) ?? 0,
                        AttributeSize = output.Attributes?.Size
                    };
                    candidate.Summary = $"{count} metatiles";
                    break;
                }

                case OutputType.MetatileMap:
                {
                    var sets = new List<(string, int)>();
                    foreach (var set in output.Sets)
                    {
                        if (!candidate.Instance.Children.TryGetValue(set.Field, out var child))
                            throw new TableEvaluationException($"set field '{set.Field}' reaches no record");

                        sets.Add((ResolveReference(set.OutputId, child, "set").Key, set.Base));
                    }

                    var width = PositiveInt(candidate, "width");
                    var height = PositiveInt(candidate, "height");
                    candidate.Model = new MetatileMapModel
                    {
                        Name = candidate.Name,
                        MapDataKey = _dataFileKey,
                        MapOffset = Offset(candidate),
                        Width = width,
                        Height = height,
                        Layout = MapScreenLayoutExtensions.FromXmlName(output.Value("layout")),
                        FormatName = output.Value("format")!,
                        EmptyBlock = IntValue(candidate, "emptyblock"),
                        Sets = sets
                    };
                    candidate.Summary = $"{width}x{height} blocks";
                    break;
                }

                case OutputType.Tilemap:
                {
                    var tiles = ResolveTiles(candidate);
                    var width = PositiveInt(candidate, "width");
                    var height = PositiveInt(candidate, "height");
                    candidate.Model = new TilemapModel
                    {
                        Name = candidate.Name,
                        MapDataKey = _dataFileKey,
                        MapOffset = Offset(candidate),
                        Width = width,
                        Height = height,
                        Layout = MapScreenLayoutExtensions.FromXmlName(output.Value("layout")),
                        FormatName = output.Value("format")!,
                        TileDataKey = tiles.Key,
                        TileOffset = tiles.Offset,
                        CodecName = output.Value("codec")!,
                        FirstTile = NonNegativeInt(candidate, "firsttile"),
                        EmptyTile = IntValue(candidate, "emptytile"),
                        TileCount = tiles.Count,
                        PaletteSlots = ResolvePaletteSlots(candidate)
                    };
                    candidate.Summary = $"{width}x{height} tiles";
                    break;
                }

                case OutputType.Arranger:
                    candidate.Model = BuildArranger(candidate);
                    break;
            }
        }
        catch (TableEvaluationException ex)
        {
            candidate.IsSkipped = true;
            candidate.Model = null;
            Skip(candidate.Instance, output, ex.Message);
        }
    }

    private ScatteredArrangerModel BuildArranger(Candidate candidate)
    {
        var output = candidate.Output;
        var width = PositiveInt(candidate, "width");
        var height = PositiveInt(candidate, "height");
        if ((long)width * height > MaxArrangerElements)
            throw new TableEvaluationException($"arranger of {width}x{height} elements has more than {MaxArrangerElements}");

        var stride = PositiveInt(candidate, "tilestride");
        var tileBase = Offset(candidate);
        var paletteKey = output.Value("palette") is { } paletteId ? ResolveReference(paletteId, candidate.Instance, "palette").Key : null;
        var codec = output.Value("codec")!;

        var cells = output.Value("layout") == "indexmap"
            ? ReadCellStream(candidate, width * height)
            : Enumerable.Range(0, width * height).Select(x => (int?)x).ToList();

        var grid = new ArrangerElementModel[width, height];
        for (var n = 0; n < cells.Count; n++)
        {
            if (cells[n] is not { } tile)
                continue;

            var offset = tileBase + (long)tile * stride;
            if (offset + stride > _data.Length)
                throw new TableEvaluationException($"tile {tile} at 0x{offset:X} is past the end of the data");

            grid[n % width, n / width] = new ArrangerElementModel
            {
                DataFileKey = _dataFileKey,
                PaletteKey = paletteKey,
                CodecName = codec,
                FileAddress = new BitAddress(offset, 0),
                PositionX = n % width,
                PositionY = n / width
            };
        }

        candidate.Summary = $"{width}x{height} elements";
        return new ScatteredArrangerModel
        {
            Name = candidate.Name,
            ElementGrid = grid,
            ArrangerElementSize = new Size(width, height),
            ElementPixelSize = new Size(IntValue(candidate, "elementwidth"), IntValue(candidate, "elementheight")),
            Layout = ElementLayout.Tiled,
            ColorType = PixelColorType.Indexed
        };
    }

    private List<int?> ReadCellStream(Candidate candidate, int cellCount)
    {
        var output = candidate.Output;
        var start = output.Expression("mapoffset")!.Evaluate(candidate.Scope);
        int? emptyCode = output.Value("emptycode") is not null ? IntValue(candidate, "emptycode") : null;
        int? runCode = output.Value("runcode") is not null ? IntValue(candidate, "runcode") : null;

        var cells = new List<int?>(cellCount);
        for (var offset = start; cells.Count < cellCount;)
        {
            var code = ReadByte(offset++);
            if (code == emptyCode)
            {
                cells.Add(null);
            }
            else if (code == runCode)
            {
                var count = ReadByte(offset++);
                cells.AddRange(Enumerable.Repeat((int?)null, Math.Min(count, cellCount - cells.Count)));
            }
            else
            {
                cells.Add(code);
            }
        }

        return cells;

        int ReadByte(long offset) => _data.TryRead(offset, 1) is [var b]
            ? b
            : throw new TableEvaluationException($"cell stream at 0x{start:X} ends after {cells.Count} of {cellCount} cells");
    }

    private void CheckMetatileCounts()
    {
        var highest = new Dictionary<string, int>();

        foreach (var candidate in _candidates.Where(x => x.Model is MetatileMapModel))
        {
            var model = (MetatileMapModel)candidate.Model!;
            if (_mapFormats.Find(model.FormatName) is not { } format)
            {
                _data.Warn($"Metatile map '{candidate.Key}' uses block format '{model.FormatName}', which is not registered; its metatile counts were not checked");
                continue;
            }

            var data = MapDataReader.Read(_source, model.MapOffset, (long)model.Width * model.Height, format.EntryBytes);
            var sets = model.Sets.OrderByDescending(x => x.Base).ToList();

            for (var i = 0; i < data.ReadEntries; i++)
            {
                var number = format.Decode(data.Data.AsSpan(i * format.EntryBytes, format.EntryBytes)).TileIndex;
                if (sets.FirstOrDefault(x => x.Base <= number) is not ({ } setKey, var setBase))
                    continue;

                highest[setKey] = Math.Max(highest.GetValueOrDefault(setKey, -1), number - setBase);
            }
        }

        foreach (var candidate in _candidates.Where(x => x.Model is MetatileSetModel))
        {
            var model = (MetatileSetModel)candidate.Model!;
            if (!highest.TryGetValue(candidate.Key, out var used) || used < model.MetatileCount)
                continue;

            _data.Warn($"Metatile set '{candidate.Key}' declared {model.MetatileCount} metatiles, maps use up to {used + 1}, raised to {used + 1}");
            candidate.RaisedCount = used + 1;
            BuildModel(candidate);
        }
    }

    private PlannedResource ToPlannedResource(Candidate candidate)
    {
        var isPalette = candidate.Output.Type == OutputType.Palette;
        return new PlannedResource(candidate.Key, candidate.ParentKey, candidate.Name, candidate.Output.TypeName, candidate.Model!, candidate.Output.Id,
            isPalette ? "palette" : candidate.Instance.Type.Name,
            isPalette ? candidate.PaletteOffset : candidate.Instance.Offset,
            candidate.Output.Repeat is null ? null : (int)candidate.I,
            isPalette ? candidate.PaletteEntries : null,
            candidate.Summary);
    }
}
