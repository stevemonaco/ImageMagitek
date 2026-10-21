using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Tables;

/// <summary>
/// Reads table definitions validated against <c>TableSchema.xsd</c>, loading their charmaps and binding every expression (LIB-TABLES-001 to -008)
/// </summary>
public sealed class XmlTableDefinitionReader
{
    public const string TableSchemaFileName = "TableSchema.xsd";
    public const string CharmapSchemaFileName = "CharmapSchema.xsd";
    public const string ManifestSchemaFileName = "TableManifestSchema.xsd";

    private static readonly HashSet<string> _reservedNames = ["index", "address", "i", "slot", "context", "min", "max", "map", "name", "n"];

    private readonly string _schemaDirectory;
    private readonly XmlSchemaSet _schemas;
    private readonly XmlCharmapReader _charmapReader;

    /// <param name="schemaDirectory">Directory holding the table, charmap and manifest schemas</param>
    public XmlTableDefinitionReader(string schemaDirectory)
    {
        _schemaDirectory = Path.GetFullPath(schemaDirectory);
        _schemas = TableXml.LoadSchema(Path.Combine(_schemaDirectory, TableSchemaFileName));
        _charmapReader = new XmlCharmapReader(Path.Combine(_schemaDirectory, CharmapSchemaFileName));
    }

    public MagitekResults<TableDefinition> LoadFromFile(string fileName)
    {
        var fullName = Path.GetFullPath(fileName);
        var loadResult = TableXml.LoadValidated(fullName, _schemas, "Table definition");
        if (loadResult.HasFailed)
            return new MagitekResults<TableDefinition>.Failed(loadResult.AsError.Reasons);

        return new Loader(fullName, _schemaDirectory, _charmapReader).Load(loadResult.AsSuccess.Result.Root!);
    }

    /// <summary>
    /// The lines reporting a failed load of <paramref name="fileName"/>: a count, then each numbered reason (CLI-COMMANDS-071)
    /// </summary>
    public static IEnumerable<string> DescribeErrors(string fileName, IReadOnlyList<string> reasons) =>
        reasons.Select((reason, i) => $"{i + 1}: {reason}").Prepend($"Definition '{fileName}' contained {reasons.Count} errors");

    private sealed class Loader(string fileName, string schemaDirectory, XmlCharmapReader charmapReader)
    {
        private readonly List<string> _errors = [];
        private readonly Dictionary<string, Charmap> _charmaps = [];
        private readonly Dictionary<string, RecordType> _records = [];
        private readonly Dictionary<string, TableDeclaration> _tables = [];
        private readonly Dictionary<string, LookupDeclaration> _lookups = [];
        private readonly Dictionary<string, NameTable> _nameTables = [];
        private readonly List<OutputDefinition> _outputs = [];
        private readonly List<TableDeclaration> _tableList = [];
        private Dictionary<string, (string Type, string Record, bool Repeated)> _outputHeaders = [];
        private readonly HashSet<string> _declaredNames = [];
        private Dictionary<string, IReadOnlySet<string>> _lookupFields = [];

        public MagitekResults<TableDefinition> Load(XElement root)
        {
            var mapper = ReadAddressSpace(root.Element("addressspace")!);

            foreach (var element in root.Elements("charmap"))
                ReadCharmap(element);

            foreach (var element in root.Elements("record"))
                ReadRecord(element);

            foreach (var element in root.Elements("lookup"))
                ReadLookup(element);

            _lookupFields = _lookups.Values.ToDictionary(x => x.Name, x => (IReadOnlySet<string>)x.Record.Fields.Select(f => f.Name).ToHashSet());

            foreach (var element in root.Elements("nametable"))
                ReadNameTable(element);

            var tables = root.Elements("table")
                .Select(element => (Element: element, Table: ReadTable(element)))
                .Where(x => x.Table is not null)
                .ToList();

            CheckPointerTargets(root);

            foreach (var (element, table) in tables)
                BindTableCounts(element, table!);

            _outputHeaders = root.Elements("output")
                .GroupBy(x => x.Attribute("id")!.Value)
                .ToDictionary(x => x.Key, x => (x.First().Attribute("type")!.Value, x.First().Attribute("record")!.Value,
                    x.Any(e => e.Attribute("repeat") is not null)));

            foreach (var element in root.Elements("output"))
                ReadOutput(element);

            if (_errors.Count > 0 || mapper is null)
                return new MagitekResults<TableDefinition>.Failed(_errors);

            return new MagitekResults<TableDefinition>.Success(new TableDefinition
            {
                FileName = fileName,
                SchemaDirectory = schemaDirectory,
                Mapper = mapper,
                RecordTypes = _records.Values.ToList(),
                Tables = _tableList,
                Lookups = _lookups,
                NameTables = _nameTables,
                Outputs = _outputs
            });
        }

        private void Error(XElement element, string? attribute, string message)
        {
            var label = element.Attribute("id")?.Value ?? element.Attribute("name")?.Value;
            var subject = label is null ? element.Name.LocalName : $"{element.Name.LocalName} '{label}'";
            var location = attribute is null ? subject : $"{subject} attribute '{attribute}'";
            _errors.Add($"'{fileName}' line {TableXml.LineOf(element)}: {location}: {message}");
        }

        private static long ParseInteger(string text) =>
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);

        private long? Integer(XElement element, string attribute, long? fallback = null)
        {
            if (element.Attribute(attribute)?.Value is not { } text)
                return fallback;

            try
            {
                return ParseInteger(text);
            }
            catch (OverflowException)
            {
                Error(element, attribute, $"'{text}' is too large");
                return fallback;
            }
        }

        private int IntegerInRange(XElement element, string attribute, int fallback, int min, int max)
        {
            var value = Integer(element, attribute, fallback)!.Value;
            if (value < min || value > max)
            {
                Error(element, attribute, $"{value} is outside {min} to {max}");
                return fallback;
            }

            return (int)value;
        }

        private static Endian EndianOf(XElement element) => element.Attribute("endian")?.Value == "big" ? Endian.Big : Endian.Little;

        private bool Declare(XElement element, string name)
        {
            if (_declaredNames.Add(name))
                return true;

            Error(element, "name", $"'{name}' is already declared as a record type, table, lookup or name table");
            return false;
        }

        private TableExpression? Expression(XElement element, string attribute, ExpressionBinding binding, bool required = false)
        {
            if (element.Attribute(attribute)?.Value is not { } text)
            {
                if (required)
                    Error(element, attribute, "is required");
                return null;
            }

            return Expression(element, attribute, text, binding);
        }

        private TableExpression? Expression(XElement element, string attribute, string text, ExpressionBinding binding)
        {
            var parseResult = TableExpression.Parse(text);
            if (parseResult.HasFailed)
            {
                Error(element, attribute, parseResult.AsError.Reason);
                return null;
            }

            var expression = parseResult.AsSuccess.Result;
            var bindResult = expression.Bind(binding);
            if (bindResult.HasFailed)
            {
                Error(element, attribute, bindResult.AsError.Reason);
                return null;
            }

            return expression;
        }

        private ExpressionBinding Binding(IEnumerable<string> identifiers, IEnumerable<string>? contextFields = null) => new()
        {
            Identifiers = identifiers.ToHashSet(),
            ContextFields = contextFields?.ToHashSet(),
            Lookups = _lookupFields
        };

        private IAddressMapper? ReadAddressSpace(XElement element)
        {
            var result = AddressMappers.Create(element.Attribute("mapper")!.Value, Integer(element, "base", 0)!.Value, Integer(element, "header"));
            if (result.HasSucceeded)
                return result.AsSuccess.Result;

            Error(element, "mapper", result.AsError.Reason);
            return null;
        }

        private void ReadCharmap(XElement element)
        {
            var name = element.Attribute("name")!.Value;
            var file = element.Attribute("file")!.Value;
            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName)!, file));

            if (_charmaps.ContainsKey(name))
            {
                Error(element, "name", $"charmap '{name}' is already declared");
                return;
            }

            var result = charmapReader.LoadFromFile(path);
            if (result.HasFailed)
            {
                foreach (var reason in result.AsError.Reasons)
                    Error(element, "file", reason);
                return;
            }

            _charmaps[name] = result.AsSuccess.Result;
        }

        private List<FieldDefinition> ReadFields(XElement owner, int size, HashSet<string> names)
        {
            var fields = new List<FieldDefinition>();

            foreach (var element in owner.Elements("field"))
            {
                var name = element.Attribute("name")!.Value;
                if (_reservedNames.Contains(name))
                {
                    Error(element, "name", $"'{name}' is a reserved word");
                    continue;
                }

                if (!names.Add(name))
                {
                    Error(element, "name", $"field '{name}' is declared twice");
                    continue;
                }

                var offset = IntegerInRange(element, "offset", 0, 0, int.MaxValue);
                var lowBit = IntegerInRange(element, "lowbit", 0, 0, 7);
                var bits = IntegerInRange(element, "bits", 8, 1, 64);
                var isPointer = element.Attribute("kind")?.Value == "pointer";
                var to = element.Attribute("to")?.Value;

                if (lowBit + bits > 64)
                    Error(element, "bits", $"lowbit {lowBit} plus {bits} bits is more than 64");

                if (to is not null && !isPointer)
                    Error(element, "to", "only a field of kind 'pointer' can name what it points to");

                var field = new FieldDefinition(name, offset, lowBit, bits, EndianOf(element), isPointer, to);
                if (offset + field.ByteCount > size)
                    Error(element, "offset", $"field '{name}' ends at byte {offset + field.ByteCount}, past the record size {size}");

                fields.Add(field);
            }

            return fields;
        }

        private void ReadRecord(XElement element)
        {
            var name = element.Attribute("name")!.Value;
            if (name == "palette")
            {
                Error(element, "name", "'palette' is reserved for palette manifest entries");
                return;
            }

            if (!Declare(element, name))
                return;

            var size = IntegerInRange(element, "size", 1, 1, 0x100000);
            var baseNames = new HashSet<string>();
            var fields = ReadFields(element, size, baseNames);

            var variants = new List<RecordVariant>();
            var baseBinding = Binding(fields.Select(x => x.Name).Concat(["index", "address"]));

            foreach (var variantElement in element.Elements("variant"))
            {
                var variantName = variantElement.Attribute("name")!.Value;
                if (variants.Any(x => x.Name == variantName))
                {
                    Error(variantElement, "name", $"variant '{variantName}' is declared twice");
                    continue;
                }

                var when = Expression(variantElement, "when", baseBinding, true);
                var variantFields = ReadFields(variantElement, size, []);

                if (when is not null)
                    variants.Add(new RecordVariant { Name = variantName, When = when, Fields = variantFields });
            }

            _records[name] = new RecordType { Name = name, Size = size, Fields = fields, Variants = variants };
        }

        private void ReadLookup(XElement element)
        {
            var name = element.Attribute("name")!.Value;
            if (!Declare(element, name))
                return;

            var recordName = element.Attribute("record")!.Value;
            if (!_records.TryGetValue(recordName, out var record))
            {
                Error(element, "record", $"unknown record type '{recordName}'");
                return;
            }

            _lookups[name] = new LookupDeclaration
            {
                Name = name,
                Address = Integer(element, "address")!.Value,
                Record = record,
                Count = IntegerInRange(element, "count", 0, 0, int.MaxValue),
                Stride = IntegerInRange(element, "stride", record.Size, 1, int.MaxValue)
            };
        }

        private void ReadNameTable(XElement element)
        {
            var name = element.Attribute("name")!.Value;
            if (!Declare(element, name))
                return;

            var charmapName = element.Attribute("charmap")!.Value;
            if (!_charmaps.TryGetValue(charmapName, out var charmap))
            {
                Error(element, "charmap", $"unknown charmap '{charmapName}'");
                return;
            }

            var isFixed = element.Attribute("kind")!.Value == "fixed";
            if (isFixed && element.Attribute("width") is null)
                Error(element, "width", "is required for a fixed-width name table");

            var overrides = new Dictionary<long, string>();
            foreach (var overrideElement in element.Elements("override"))
            {
                var value = Integer(overrideElement, "value")!.Value;
                if (!overrides.TryAdd(value, overrideElement.Attribute("text")!.Value))
                    Error(overrideElement, "value", $"value 0x{value:X} is overridden twice");
            }

            _nameTables[name] = new NameTable
            {
                Name = name,
                Address = Integer(element, "address")!.Value,
                IsFixedWidth = isFixed,
                Width = IntegerInRange(element, "width", 1, 1, 256),
                PointerBits = IntegerInRange(element, "pointerbits", 32, 8, 64),
                Endian = EndianOf(element),
                Count = IntegerInRange(element, "count", 0, 0, int.MaxValue),
                Charmap = charmap,
                IndexBase = Integer(element, "indexbase", 0)!.Value,
                TitleCase = element.Attribute("case")?.Value == "title",
                Overrides = overrides
            };
        }

        private TableDeclaration? ReadTable(XElement element)
        {
            var name = element.Attribute("name")!.Value;
            if (!Declare(element, name))
                return null;

            var recordName = element.Attribute("record")?.Value;
            var pointerTarget = element.Attribute("pointer")?.Value;

            if ((recordName is null) == (pointerTarget is null))
            {
                Error(element, recordName is null ? "record" : "pointer", "a table needs exactly one of 'record' and 'pointer'");
                return null;
            }

            var countForms = new[] { "count", "until", "counts" }.Count(x => element.Attribute(x) is not null);
            if (countForms != 1)
                Error(element, "count", "a table needs exactly one of 'count', 'until' and 'counts'");

            RecordType? record = null;
            if (recordName is not null && !_records.TryGetValue(recordName, out record))
                Error(element, "record", $"unknown record type '{recordName}'");

            var pointerBits = IntegerInRange(element, "pointerbits", 32, 8, 64);
            List<long>? counts = null;
            if (element.Attribute("counts")?.Value is { } countsText)
            {
                try
                {
                    counts = countsText.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries).Select(ParseInteger).ToList();
                }
                catch (OverflowException)
                {
                    Error(element, "counts", $"'{countsText}' has an entry that is too large");
                }
            }

            var table = new TableDeclaration
            {
                Name = name,
                Address = Integer(element, "address"),
                Record = record,
                PointerTarget = pointerTarget,
                PointerBits = pointerBits,
                Endian = EndianOf(element),
                Stride = IntegerInRange(element, "stride", record?.Size ?? pointerBits / 8, 1, int.MaxValue),
                Counts = counts
            };

            _tables[name] = table;
            _tableList.Add(table);
            return table;
        }

        private bool IsRecordOrTable(string name) => _records.ContainsKey(name) || _tables.ContainsKey(name);

        private void CheckPointerTargets(XElement root)
        {
            foreach (var recordElement in root.Elements("record"))
            {
                foreach (var fieldElement in recordElement.Descendants("field"))
                {
                    if (fieldElement.Attribute("to")?.Value is { } to && !IsRecordOrTable(to))
                        Error(fieldElement, "to", $"unknown record type or table '{to}'");
                }
            }

            foreach (var tableElement in root.Elements("table"))
            {
                if (tableElement.Attribute("pointer")?.Value is { } target && !IsRecordOrTable(target))
                    Error(tableElement, "pointer", $"unknown record type or table '{target}'");
            }
        }

        private void BindTableCounts(XElement element, TableDeclaration table)
        {
            // A count sees the pointing record's fields only when every path to the table comes from a record field
            var pointingRecords = _records.Values.Where(r => r.Variants.SelectMany(v => v.Fields).Concat(r.Fields).Any(f => f.To == table.Name)).ToList();
            var reachedOtherwise = table.Address is not null || _tables.Values.Any(t => t.PointerTarget == table.Name);
            var countFields = reachedOtherwise || pointingRecords.Count == 0
                ? []
                : pointingRecords.Select(r => r.Fields.Select(f => f.Name)).Aggregate((a, b) => a.Intersect(b));

            table.Count = Expression(element, "count", Binding(countFields.Append("index")));

            var rowIdentifiers = table.Record is { } record ? record.Fields.Select(x => x.Name) : ["value"];
            table.Until = Expression(element, "until", Binding(rowIdentifiers.Append("index")));
        }

        private void ReadOutput(XElement element)
        {
            var id = element.Attribute("id")!.Value;
            if (_outputs.Any(x => x.Id == id))
            {
                Error(element, "id", $"output '{id}' is declared twice");
                return;
            }

            var typeName = element.Attribute("type")!.Value;
            var type = typeName switch
            {
                "compresseddata" => OutputType.CompressedData,
                "palette" => OutputType.Palette,
                "metatileset" => OutputType.MetatileSet,
                "metatilemap" => OutputType.MetatileMap,
                "arranger" => OutputType.Arranger,
                _ => OutputType.Tilemap
            };

            var recordName = element.Attribute("record")!.Value;
            if (!_records.TryGetValue(recordName, out var record))
            {
                Error(element, "record", $"unknown record type '{recordName}'");
                return;
            }

            RecordVariant? variant = null;
            if (element.Attribute("variant")?.Value is { } variantName && (variant = record.FindVariant(variantName)) is null)
                Error(element, "variant", $"record type '{record.Name}' has no variant '{variantName}'");

            RecordType? context = null;
            if (element.Attribute("context")?.Value is { } contextName)
            {
                if (!_records.TryGetValue(contextName, out context))
                    Error(element, "context", $"unknown record type '{contextName}'");
                else if (context == record)
                    Error(element, "context", "the context must be another record type than the output's");
            }

            var fields = record.FieldsWith(variant);
            var identifiers = fields.Select(x => x.Name).Concat(["index", "address", "i"]).ToList();
            var contextFields = context?.Fields.Select(x => x.Name);
            var baseBinding = Binding(identifiers, contextFields);
            var binding = type == OutputType.Palette ? Binding(identifiers.Append("slot"), contextFields) : baseBinding;

            TableExpression? chooseMode = null;
            if (element.Attribute("choose")?.Value is { } choose && choose != "first")
            {
                if (context is null)
                    Error(element, "choose", "'choose' needs a 'context'");
                else if (choose.StartsWith("mode(", StringComparison.Ordinal) && choose.EndsWith(')'))
                    chooseMode = Expression(element, "choose", choose[5..^1], Binding(context.Fields.Select(x => x.Name).Concat(["index", "address"])));
                else
                    Error(element, "choose", $"'{choose}' is not 'first' or 'mode(expression)'");
            }

            var expressions = new Dictionary<string, TableExpression>();
            void Expr(string attribute, bool required)
            {
                if (Expression(element, attribute, binding, required) is { } expression)
                    expressions[attribute] = expression;
            }

            var values = new Dictionary<string, string>();
            void Value(string attribute, bool required)
            {
                if (element.Attribute(attribute)?.Value is { } value)
                    values[attribute] = value;
                else if (required)
                    Error(element, attribute, $"is required for a {typeName} output");
            }

            Expr("offset", true);

            switch (type)
            {
                case OutputType.CompressedData:
                    Value("compression", true);
                    break;
                case OutputType.Palette:
                    Expr("entries", true);
                    if (Expression(element, "slot", baseBinding) is { } slot)
                        expressions["slot"] = slot;
                    Value("color", true);
                    Value("zeroindextransparent", false);
                    Value("endian", false);
                    if (values.TryGetValue("color", out var color) && !Enum.TryParse<ColorModel>(color, false, out _))
                        Error(element, "color", $"unknown color model '{color}'");
                    break;
                case OutputType.MetatileSet:
                    Expr("count", true);
                    Expr("firsttile", false);
                    Value("format", true);
                    Value("codec", true);
                    Value("order", false);
                    values["metatilewidth"] = IntegerInRange(element, "metatilewidth", 2, 1, 8).ToString(CultureInfo.InvariantCulture);
                    values["metatileheight"] = IntegerInRange(element, "metatileheight", 2, 1, 8).ToString(CultureInfo.InvariantCulture);
                    values["layers"] = IntegerInRange(element, "layers", 1, 1, 4).ToString(CultureInfo.InvariantCulture);
                    break;
                case OutputType.MetatileMap:
                    Expr("width", true);
                    Expr("height", true);
                    Value("format", true);
                    Value("layout", false);
                    values["emptyblock"] = IntegerInRange(element, "emptyblock", 0, 0, int.MaxValue).ToString(CultureInfo.InvariantCulture);
                    break;
                case OutputType.Tilemap:
                    Expr("width", true);
                    Expr("height", true);
                    Expr("firsttile", false);
                    Value("format", true);
                    Value("codec", true);
                    Value("layout", false);
                    values["emptytile"] = IntegerInRange(element, "emptytile", 0, 0, int.MaxValue).ToString(CultureInfo.InvariantCulture);
                    break;
                case OutputType.Arranger:
                    Expr("width", true);
                    Expr("height", true);
                    Expr("tilestride", true);
                    Value("codec", true);
                    Value("layout", true);
                    Value("palette", false);
                    values["elementwidth"] = IntegerInRange(element, "elementwidth", 8, 1, 64).ToString(CultureInfo.InvariantCulture);
                    values["elementheight"] = IntegerInRange(element, "elementheight", 8, 1, 64).ToString(CultureInfo.InvariantCulture);

                    string[] codes = ["emptycode", "runcode"];
                    switch (element.Attribute("layout")?.Value)
                    {
                        case "indexmap":
                            Expr("mapoffset", true);
                            foreach (var code in codes.Where(x => element.Attribute(x) is not null))
                                values[code] = IntegerInRange(element, code, 0, 0, 255).ToString(CultureInfo.InvariantCulture);
                            break;
                        case "rect":
                            foreach (var attribute in codes.Prepend("mapoffset").Where(x => element.Attribute(x) is not null))
                                Error(element, attribute, "applies only to an arranger with the 'indexmap' layout");
                            break;
                        case "mask":
                            Error(element, "layout", "arranger layout 'mask' is not implemented yet");
                            break;
                        case { } arrangerLayout:
                            Error(element, "layout", $"unknown arranger layout '{arrangerLayout}'");
                            break;
                    }
                    break;
            }

            if (type is OutputType.Tilemap or OutputType.MetatileMap && values.TryGetValue("layout", out var layout)
                && !IsValid(() => MapScreenLayoutExtensions.FromXmlName(layout)))
                Error(element, "layout", $"unknown map screen layout '{layout}'");

            var tiles = new List<TileSource>();
            foreach (var tilesElement in element.Elements("tiles"))
            {
                var outputId = tilesElement.Attribute("output")?.Value;
                var hasOffset = tilesElement.Attribute("offset") is not null;

                if ((outputId is null) == !hasOffset)
                    Error(tilesElement, "output", "tiles need exactly one of 'output' and 'offset'");
                else if (outputId is not null && tilesElement.Attribute("count") is not null)
                    Error(tilesElement, "count", "a tile count applies only to tiles at an 'offset'");

                tiles.Add(new TileSource(Expression(tilesElement, "when", binding), outputId,
                    Expression(tilesElement, "offset", binding), Expression(tilesElement, "count", binding)));
            }

            if (type is OutputType.MetatileSet or OutputType.Tilemap && tiles.Count == 0)
                Error(element, null, $"a {typeName} output needs a 'tiles' element");

            var paletteSlots = element.Elements("paletteslots").Select(x => x.Attribute("output")!.Value).FirstOrDefault();

            AttributeTableSource? attributes = null;
            if (element.Element("attributes") is { } attributesElement)
            {
                var offset = Expression(attributesElement, "offset", binding, true);
                if (offset is not null)
                    attributes = new AttributeTableSource(offset, IntegerInRange(attributesElement, "size", 1, 1, 8));
            }

            var sets = new List<SetReference>();
            foreach (var setElement in element.Elements("set"))
            {
                var fieldName = setElement.Attribute("field")!.Value;
                var field = fields.FirstOrDefault(x => x.Name == fieldName);

                if (field is null)
                    Error(setElement, "field", $"record type '{record.Name}' has no field '{fieldName}'");
                else if (field.To is null || !_records.ContainsKey(field.To))
                    Error(setElement, "field", $"field '{fieldName}' does not point to a record type");

                sets.Add(new SetReference(setElement.Attribute("output")!.Value, fieldName, IntegerInRange(setElement, "base", 0, 0, int.MaxValue)));
            }

            if (type == OutputType.MetatileMap && sets.Count == 0)
                Error(element, null, "a metatilemap output needs a 'set' element");

            var output = new OutputDefinition
            {
                Id = id,
                Type = type,
                Record = record,
                When = Expression(element, "when", baseBinding),
                Repeat = Expression(element, "repeat", baseBinding),
                Context = context,
                ChooseMode = chooseMode,
                Variant = variant?.Name,
                Duplicates = element.Attribute("duplicates")?.Value == "address" ? DuplicateRule.Address : DuplicateRule.Number,
                Names = ReadTemplates(element, "name", binding, record, false),
                Folders = ReadTemplates(element, "folder", binding, record, true),
                Expressions = expressions,
                Values = values,
                Tiles = tiles,
                PaletteSlotsOutput = paletteSlots,
                Attributes = attributes,
                Sets = sets
            };

            CheckOutputReferences(element, output, fields);
            _outputs.Add(output);
        }

        private static bool IsValid(Action parse)
        {
            try
            {
                parse();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private List<ConditionalTemplate> ReadTemplates(XElement output, string elementName, ExpressionBinding binding, RecordType record, bool isFolder)
        {
            var templates = new List<ConditionalTemplate>();

            foreach (var element in output.Elements(elementName))
            {
                var when = Expression(element, "when", binding);
                var result = NameTemplate.Parse(element.Value.Trim(), _nameTables);

                if (result.HasFailed)
                {
                    Error(element, null, result.AsError.Reason);
                    continue;
                }

                var template = result.AsSuccess.Result;
                foreach (var expression in template.Expressions)
                {
                    var bindResult = expression.Bind(binding);
                    if (bindResult.HasFailed)
                        Error(element, null, bindResult.AsError.Reason);
                }

                if (isFolder && (template.UsesCounter || template.OutputNames.Any()))
                    Error(element, null, "a folder template cannot use {n} or {name:…}");

                foreach (var outputId in template.OutputNames)
                {
                    var referenced = _outputs.FirstOrDefault(x => x.Id == outputId);
                    if (referenced is null)
                        Error(element, null, $"{{name:{outputId}}} must name an output declared earlier");
                    else if (referenced.Record != record || referenced.Repeat is not null)
                        Error(element, null, $"{{name:{outputId}}} must name an output of record type '{record.Name}' without 'repeat'");
                }

                templates.Add(new ConditionalTemplate(when, template));
            }

            return templates;
        }

        private void CheckOutputReferences(XElement element, OutputDefinition output, IReadOnlyList<FieldDefinition> fields)
        {
            bool IsOutput(string id, string type, string record) =>
                _outputHeaders.TryGetValue(id, out var header) && header.Type == type && header.Record == record;

            foreach (var tiles in output.Tiles.Where(x => x.OutputId is not null))
            {
                if (!IsOutput(tiles.OutputId!, "compresseddata", output.Record.Name))
                    Error(element, "tiles", $"'{tiles.OutputId}' is not a compresseddata output of record type '{output.Record.Name}'");
            }

            if (output.PaletteSlotsOutput is { } paletteOutput && !IsOutput(paletteOutput, "palette", output.Record.Name))
                Error(element, "paletteslots", $"'{paletteOutput}' is not a palette output of record type '{output.Record.Name}'");

            if (output.Type == OutputType.Arranger && output.Value("palette") is { } arrangerPalette)
            {
                if (!IsOutput(arrangerPalette, "palette", output.Record.Name))
                    Error(element, "palette", $"'{arrangerPalette}' is not a palette output of record type '{output.Record.Name}'");
                else if (_outputHeaders[arrangerPalette].Repeated)
                    Error(element, "palette", $"palette output '{arrangerPalette}' has 'repeat', so it is not one palette per record");
            }

            foreach (var set in output.Sets)
            {
                var field = fields.FirstOrDefault(x => x.Name == set.Field);
                if (field?.To is null || !IsOutput(set.OutputId, "metatileset", field.To))
                    Error(element, "set", $"'{set.OutputId}' is not a metatileset output of the record type field '{set.Field}' points to");
            }
        }
    }
}
