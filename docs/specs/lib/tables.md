---
id: LIB-TABLES
title: Table-driven resources
status: draft
project: ImageMagitek
sources:
  - ImageMagitek/Tables
  - ImageMagitek/_schemas/TableSchema.xsd
  - ImageMagitek/_schemas/CharmapSchema.xsd
  - ImageMagitek/_schemas/TableManifestSchema.xsd
  - Samples/Tables
types:
  - TableDefinition
  - XmlTableDefinitionReader
  - RecordType
  - FieldDefinition
  - RecordVariant
  - TableDeclaration
  - LookupDeclaration
  - NameTable
  - OutputDefinition
  - NameTemplate
  - Charmap
  - XmlCharmapReader
  - IAddressMapper
  - GbaAddressMapper
  - LinearAddressMapper
  - AddressMappers
  - TableExpression
  - TableEvaluator
  - TableEvaluation
  - RecordInstance
  - TableGenerator
  - GenerationPlan
  - PlannedResource
  - GenerationReport
  - TableManifest
  - XmlTableManifestReader
  - XmlTableManifestWriter
tests:
  - AddressMapperTests
  - TableDefinitionReaderTests
  - TableExpressionTests
  - RecordFieldTests
  - TableEvaluatorTests
  - CharmapTests
  - NameTableTests
  - TableGeneratorTests
  - TableManifestTests
  - TableGenerationProjectTests
depends:
  - LIB-DATASOURCE
  - LIB-PROJECT-FORMAT
  - LIB-PROJECT-TREE
  - LIB-COMPRESSION
  - LIB-PALETTES
  - LIB-TILEMAPS
  - LIB-METATILE-MAPS
---

# Table-driven resources

## Purpose

Generates ordinary project resources (compressed data, palettes, metatile sets, metatile maps, tilemaps) from a game's record tables. A **table definition** is a declarative XML file that describes how the game's pointers map to file offsets, its record types and tables, small integer expressions over their fields, its text encodings (**charmaps**) and name tables, and one **output** per kind of resource to create from a record. `TableEvaluator` walks the tables over a `DataSource` (LIB-DATASOURCE) into one `RecordInstance` per record type and file offset; `TableGenerator` turns the instances into a `GenerationPlan` of LIB-PROJECT-FORMAT resource models with names, folders, a report and a **manifest** entry. LIB-PROJECT-SERVICE builds and writes a plan into a project in one transaction (its Generate group). The manifest is an open XML format, specified here so other programs can write one. Phase 1 covers definitions and generation; regeneration from a manifest is pending (proposal [table-driven resources](../../changes/table-resources.md)).

## Requirements

### Loading

- **LIB-TABLES-001** — A definition shall be read from a file and validated against `TableSchema.xsd`. If the file is not well-formed XML or fails validation, then loading shall fail naming the file and, for each error, its line.
  - Tests: `TableDefinitionReaderTests.SchemaError_NamesFileAndLine`
- **LIB-TABLES-002** — If a definition names a record type, table, lookup, name table, charmap, output or field that it does not declare, declares a name twice (record types, tables, lookups and name tables share one namespace, so a pointer's `to` and a `name[expr]` reference are never ambiguous; output ids share another), names a field with a reserved word (`index`, `address`, `i`, `slot`, `context`, `min`, `max`, `name`, `n`), or names a record type `palette`, then loading shall fail naming the file, the line, the element and the attribute.
  - Tests: `TableDefinitionReaderTests.UnknownField_FailsAtLoadNamingElementAndAttribute`, `TableDefinitionReaderTests.UnknownRecordType_FailsAtLoad`, `TableDefinitionReaderTests.UnknownCharmap_FailsAtLoad`
- **LIB-TABLES-003** — If an expression or a name or folder template does not parse, or names an identifier its scope does not have (LIB-TABLES-041), then loading shall fail naming the file, the line, the element, the attribute and the text, so no expression fails to parse during evaluation.
  - Tests: `TableDefinitionReaderTests.BadExpression_FailsAtLoad`
- **LIB-TABLES-004** — A `charmap` element's `file` shall resolve against the definition's directory, and the charmap file shall be validated against `CharmapSchema.xsd`; if it is missing or invalid, then loading shall fail naming the charmap file.
  - Tests: `TableDefinitionReaderTests.UnknownCharmap_FailsAtLoad`, `TableDefinitionReaderTests.ShippedDefinitions_Load`
- **LIB-TABLES-005** — If a definition uses a pending address mapper (LIB-TABLES-013) or the pending `arranger` output type (LIB-TABLES-079), then loading shall fail naming the mapper or output type as not implemented yet.
  - Tests: `AddressMapperTests.PendingMapper_FailsLoad`, `TableDefinitionReaderTests.PendingOutputType_FailsLoad`
- **LIB-TABLES-006** — Loading shall collect every semantic error in the definition before failing, not only the first.
  - Tests: `TableDefinitionReaderTests.UnknownField_FailsAtLoadNamingElementAndAttribute`
- **LIB-TABLES-007** — Integer attributes that are not expressions (sizes, offsets, bit counts, list-literal counts, override values) shall accept decimal and `0x` hexadecimal.
  - Tests: `TableDefinitionReaderTests.ShippedDefinitions_Load`
- **LIB-TABLES-008** — The definitions and charmaps shipped in `Samples/Tables` shall load.
  - Tests: `TableDefinitionReaderTests.ShippedDefinitions_Load`

### Address space

- **LIB-TABLES-010** — The `gba` mapper shall map a pointer in `0x08000000`–`0x09FFFFFF` to the file offset `pointer − 0x08000000`, and treat any other non-zero pointer as unmapped; a file offset maps back to `offset + 0x08000000`.
  - Tests: `AddressMapperTests.Gba_RoundTrips`, `AddressMapperTests.Gba_OutsideRom_IsUnmapped`
- **LIB-TABLES-011** — The `linear` mapper shall map a pointer to `pointer − base` (`base` from the `addressspace` element, 0 by default), treat a pointer below `base` as unmapped, and map a file offset back to `offset + base`.
  - Tests: `AddressMapperTests.Linear_RoundTripsWithBase`
- **LIB-TABLES-012** — For every mapper, the pointer value 0 shall be null: it maps to no offset and is not unmapped.
  - Tests: `AddressMapperTests.Gba_NullPointer`
- **LIB-TABLES-013** — The schema shall accept the mappers `snes-lorom`, `snes-hirom`, `nes` and `gb`; they are pending and refused at load (LIB-TABLES-005).
  - Tests: `AddressMapperTests.PendingMapper_FailsLoad`
- **LIB-TABLES-014** — Every `address` in a definition (tables, lookups, name tables) shall be a pointer in the game's address space, mapped through the address space when used; an address that does not map shall be reported as a warning and the table, lookup entry or name entry treated as unreadable.
  - Tests: `TableEvaluatorTests.UnmappedTableAddress_Warns`

### Records and fields

- **LIB-TABLES-020** — A record type shall have a byte `size` and fields. A field's value shall be the unsigned integer formed by the `ceil((lowbit + bits) / 8)` bytes at its byte `offset` in the record, assembled in its `endian` order (little by default), shifted right by `lowbit` (0 to 7) and masked to `bits` (1 to 64, with `lowbit + bits` ≤ 64).
  - Tests: `RecordFieldTests.BitFields_EveryAlignmentAndEndian`
- **LIB-TABLES-021** — If a field's bytes extend past its record's size, then loading shall fail naming the field.
  - Tests: `TableDefinitionReaderTests.FieldOutsideRecord_FailsAtLoad`
- **LIB-TABLES-022** — A field of kind `pointer` shall evaluate to its mapped file offset, a null pointer to 0, and an unmapped pointer shall be an evaluation error when an expression reads it.
  - Tests: `RecordFieldTests.Pointer_EvaluatesToMappedOffset`
- **LIB-TABLES-023** — A pointer field whose `to` names a record type or a table shall be followed: the record at, or the table starting at, its file offset is walked as a child of the record holding the field. A null pointer shall have no child.
  - Tests: `TableEvaluatorTests.SharedChild_YieldsOneInstance`, `TableEvaluatorTests.NullPointer_NoChild`
- **LIB-TABLES-024** — If a followed pointer is unmapped, or the record it reaches extends past the end of the data, then evaluation shall report it as a skipped record naming the record type, the offset when known, the parent record and the field, and continue with the next field; it shall not throw.
  - Tests: `TableEvaluatorTests.PointerPastEnd_ReportedPerRecord`

### Tables and counts

- **LIB-TABLES-030** — A table shall hold either records of a type (`record`), each row at `start + i × stride` with `stride` the record size by default, or pointers (`pointer`) to a record type or another table, each row a `pointerbits`-bit (32 by default) pointer in `endian` order (little by default) at `start + i × stride`, `stride` defaulting to `pointerbits / 8`.
  - Tests: `TableEvaluatorTests.SharedChild_YieldsOneInstance`, `TableEvaluatorTests.ListLiteralCounts_IndexedByParent`
- **LIB-TABLES-031** — A table's `count` shall be an expression evaluated with the pointing record's fields and `index` in scope (only literals and lookups for a table with an `address`); a negative count or an evaluation error shall be reported as a warning and the table read as empty.
  - Tests: `TableEvaluatorTests.ExpressionCount`
- **LIB-TABLES-032** — A table with `until` shall read rows until the predicate holds for a row (fields of a record row, or `value`, the raw pointer, of a pointer row, plus `index`); the terminating row is excluded. If the data ends first, then the rows read so far shall be kept and a warning reported.
  - Tests: `TableEvaluatorTests.SentinelTerminatedTable`, `TableEvaluatorTests.SentinelHitsEndOfData_Warns`
- **LIB-TABLES-033** — A table with `counts` (a list literal of integers separated by spaces or commas) shall take as its count the entry at the index of the row that points at it. If the list has no entry at that index, then the table shall be read as empty and a warning reported naming the table and the index.
  - Tests: `TableEvaluatorTests.ListLiteralCounts_IndexedByParent`, `TableEvaluatorTests.ListLiteralTooShort_Reported`
- **LIB-TABLES-034** — Tables with an `address` shall be the roots of evaluation, walked in declaration order; a table without one is read only when a pointer reaches it. A table shall declare exactly one of `count`, `until` and `counts`, and exactly one of `record` and `pointer`.
  - Tests: `TableEvaluatorTests.SharedChild_YieldsOneInstance`, `TableDefinitionReaderTests.UnknownRecordType_FailsAtLoad`
- **LIB-TABLES-035** — A lookup shall be a table of records at an `address` with a literal `count`; `name[expr].field` shall read that field of the record at index `expr`. An index outside `0 … count − 1`, or a record past the end of the data, shall be an evaluation error. Lookups are not walked for outputs.
  - Tests: `TableEvaluatorTests.Lookup_ReadsIndexedRecordField`

### Expressions

- **LIB-TABLES-040** — Expressions shall evaluate on 64-bit signed integers with decimal and `0x` literals, parentheses, unary `-` and `~`, `* / %` (truncating), `+ -`, `<< >>` (arithmetic), `< <= > >=`, `== !=`, `&`, `^`, `|` and `?:`, at C precedence and associativity, comparisons giving 1 or 0, any non-zero value counting as true, and the functions `min(a, b, …)` and `max(a, b, …)`. No other syntax is accepted and no code is compiled or run.
  - Tests: `TableExpressionTests.Precedence`, `TableExpressionTests.HexAndDecimal`, `TableExpressionTests.MinMax`, `TableExpressionTests.Ternary`, `TableExpressionTests.ShiftsAndBitwise`
- **LIB-TABLES-041** — An expression's identifiers shall be the fields of the record in scope (with the output's variant, LIB-TABLES-051), `index` (the row index of the table row that first reached the record; a record reached through a pointer field takes its parent's index), `address` (the record's file offset), `i` (the repeat counter, 0 without `repeat`), `slot` (in a palette output, after `slot` itself), `context.<field>` (in an output with `context`), and `<lookup>[expr].<field>`.
  - Tests: `TableExpressionTests.Precedence`, `TableEvaluatorTests.Lookup_ReadsIndexedRecordField`, `TableGeneratorTests.ContextMode_PicksMostCommonSection`
- **LIB-TABLES-042** — Division or modulo by zero, a shift count outside 0–63, a read of an unmapped pointer field, a lookup index out of range and a missing context shall be evaluation errors.
  - Tests: `TableExpressionTests.DivisionByZero_IsEvaluationError`, `TableExpressionTests.ModuloByZero_IsEvaluationError`, `TableExpressionTests.ShiftsAndBitwise`
- **LIB-TABLES-043** — An evaluation error in an output for one record shall skip that output for that record and be reported as a skipped record naming the output and the error; other records and outputs continue.
  - Tests: `TableGeneratorTests.EvaluationError_SkipsOutputAndReports`

### Variants

- **LIB-TABLES-050** — A record type may declare variants, each with a `when` predicate over the base fields and fields of its own; a record shall use the first variant whose predicate holds, its fields adding to the base fields or replacing those of the same name, or none.
  - Tests: `TableEvaluatorTests.Variant_SelectedByPredicate`
- **LIB-TABLES-051** — An output naming a `variant` shall apply only to records using that variant, and its expressions may read that variant's fields; an output without one sees only the base fields.
  - Tests: `TableGeneratorTests.OutputVariant_OnlyMatchingRecords`

### Text

- **LIB-TABLES-060** — A charmap shall map single bytes to text: a `map` element maps one byte to its whole text, and a `run` element maps consecutive bytes from its `byte` to the consecutive characters of its text. Decoding shall stop at the charmap's terminator byte.
  - Tests: `CharmapTests.Runs_Decode`, `CharmapTests.Terminator_EndsString`
- **LIB-TABLES-061** — A byte the charmap does not map shall decode as `[XX]` (two hex digits) and be reported.
  - Tests: `CharmapTests.UnmappedByte_RendersHexAndReports`
- **LIB-TABLES-062** — A name table shall be `count` entries at its `address`, either pointers to terminated strings (`kind="pointer"`, `pointerbits` 32 by default) or fixed-width strings (`kind="fixed"`, `width` bytes, ending at the terminator or the width), decoded through its charmap; a pointer string ends at its terminator or after 256 bytes.
  - Tests: `NameTableTests.PointerStrings`, `NameTableTests.FixedWidth`
- **LIB-TABLES-063** — Looking up value *v* shall read entry `v − indexbase` (`indexbase` 0 by default).
  - Tests: `NameTableTests.IndexBase`
- **LIB-TABLES-064** — An `override` shall replace the name of the looked-up value it is keyed by, used verbatim, whether or not the entry exists.
  - Tests: `NameTableTests.Override_ReplacesName`
- **LIB-TABLES-065** — With `case="title"`, a decoded name shall have each letter that is not preceded by a letter or an apostrophe in upper case and every other letter in lower case.
  - Tests: `NameTableTests.TitleCase`
- **LIB-TABLES-066** — If the entry index is outside the table, or its pointer is unmapped or its string past the end of the data, then the lookup shall give `<name table> 0x<value>` and report a warning, once per distinct value.
  - Tests: `NameTableTests.IndexOutOfRange_FallsBack`

### Outputs

- **LIB-TABLES-070** — Each output shall produce resources for every record instance of its `record` type in traversal order (LIB-TABLES-101), skipping those whose variant does not match (LIB-TABLES-051) or whose `when` is false, and one resource per `i` in `0 … repeat − 1` when it has `repeat` (0 to 256). A false `when` is not reported.
  - Tests: `TableGeneratorTests.SharedTileset_OneSetReferencedByBothMaps`, `TableGeneratorTests.PalettesDedupedByAddressAndEntries`
- **LIB-TABLES-071** — A `compresseddata` output shall create compressed data over the generation's data file at file offset `offset` with the compression format named by `compression`.
  - Tests: `TableGeneratorTests.CompressedAndUncompressedTiles`
- **LIB-TABLES-072** — A `palette` output shall create a palette of `entries` colors in the `color` model, with `zeroindextransparent` (false by default), read from the data file at `offset` in `endian` order. Palettes shall be deduplicated by file offset, entry count, color model and transparency across all palette outputs: a later one reuses the first one's resource and key.
  - Tests: `TableGeneratorTests.PalettesDedupedByAddressAndEntries`
- **LIB-TABLES-073** — A `metatileset` output shall create a metatile set (LIB-METATILE-MAPS-001) from `offset`, `count`, `metatilewidth` and `metatileheight` (2 by default), `layers` (1 by default), `format`, `order` (`interleaved` by default), `codec` and `firsttile` (0 by default); its tiles from the first `tiles` child whose `when` holds, either the resource of a `compresseddata` output of the same record (tile offset 0, no tile count) or the data file at `offset` with an optional `count`; its palette slots from a `paletteslots` child naming a `palette` output of the same record, each palette at its `slot`; and an optional attribute table from an `attributes` child (`offset`, `size`).
  - Tests: `TableGeneratorTests.CompressedAndUncompressedTiles`, `TableGeneratorTests.PalettesDedupedByAddressAndEntries`
- **LIB-TABLES-074** — A `metatilemap` output shall create a metatile map (LIB-METATILE-MAPS-010) from `offset`, `width`, `height`, `format`, `layout` (`linear` by default) and `emptyblock` (0 by default), listing for each `set` child, in order, the resource of the named `metatileset` output for the record its pointer `field` reaches, at `base`.
  - Tests: `TableGeneratorTests.SharedTileset_OneSetReferencedByBothMaps`
- **LIB-TABLES-075** — A `tilemap` output shall create a tilemap (LIB-TILEMAPS) from `offset`, `width`, `height`, `format`, `layout`, `codec`, `firsttile` and `emptytile`, with tiles and palette slots as for a metatile set.
  - Tests: `TableGeneratorTests.TilemapOutput_BuildsModel`
- **LIB-TABLES-076** — If an output's `tiles`, `paletteslots` or `set` reference reaches a resource that is not generated (its output's `when` was false, it was skipped, or the pointer had no record), then the output shall be skipped for that record with a reason naming the reference; outputs that reference the skipped resource are skipped in turn.
  - Tests: `TableGeneratorTests.OutputReferencingSkippedResource_Skipped`
- **LIB-TABLES-077** — A child element's `when` (on `tiles`, `name`, `folder`) shall select the first child whose `when` holds or that has none.
  - Tests: `TableGeneratorTests.CompressedAndUncompressedTiles`, `TableGeneratorTests.NameTemplates_RenderSegments`
- **LIB-TABLES-078** — Generated models shall reference the data file by the key generation was given, and every other generated resource by the key it is planned at.
  - Tests: `TableGenerationProjectTests.Generate_SavesAndReloadsWithoutDefinition`
- **LIB-TABLES-079** — The schema shall accept the `arranger` output type with a `layout` of `rect`, `mask` or `indexmap`; it is pending and refused at load (LIB-TABLES-005).
  - Tests: `TableDefinitionReaderTests.PendingOutputType_FailsLoad`

### Names and folders

- **LIB-TABLES-080** — A name template shall be literal text with `{…}` segments: `{expr}` (decimal), `{expr:Xn}` or `{expr:Dn}` (hexadecimal or decimal, at least *n* digits), `{table[expr]}` (the name table's text for the value), `{name:outputId}` (the final name of the same record's resource from an earlier-declared output without `repeat`), and `{n}` (LIB-TABLES-085); `{{` and `}}` are literal braces.
  - Tests: `TableGeneratorTests.NameTemplates_RenderSegments`
- **LIB-TABLES-081** — An output's name shall come from the first `name` child whose `when` holds or that has none, and its folder from the first such `folder` child; with no name the fallback name (LIB-TABLES-083) is used, and with no folder the resource goes in the target folder.
  - Tests: `TableGeneratorTests.NameTemplates_RenderSegments`
- **LIB-TABLES-082** — A folder template shall be a path relative to the target folder with `/` separators, each segment sanitized as a folder name (LIB-TABLES-083), empty segments dropped, and a segment still invalid after sanitizing dropped with a warning. A folder template shall not use `{n}` or `{name:…}`.
  - Tests: `TableGeneratorTests.NamesSanitizedAndUnique`
- **LIB-TABLES-083** — A generated name shall be sanitized by removing the characters LIB-PROJECT-TREE-030 forbids and control characters, then trimming leading whitespace and `.` and trailing whitespace and `.`. If the result is empty or still breaks the name rule, then the name shall be `<output id> <offset as six hex digits>`.
  - Tests: `TableGeneratorTests.NamesSanitizedAndUnique`
- **LIB-TABLES-084** — Names shall be unique within a folder among the generated resources and folders, compared with LIB-PROJECT-TREE-031. On a clash the later resource's name changes by its output's `duplicates` rule: `number` (the default) appends " 2", " 3", …, the first free; `address` appends its record's offset as six hex digits, then numbers if that still clashes.
  - Tests: `TableGeneratorTests.Duplicates_NumberAndAddress`, `TableGeneratorTests.NamesSanitizedAndUnique`
- **LIB-TABLES-085** — `{n}` shall be the 1-based position of the resource among the same output's resources in the same folder whose name renders the same apart from `{n}`, in traversal order.
  - Tests: `TableGeneratorTests.NameTemplates_RenderSegments`

### Context

- **LIB-TABLES-090** — An output with `context="<RecordType>"` shall evaluate `context.<field>` against one ancestor of that type, among the distinct ancestor instances reached by walking referrers up from the record, ordered by when they were first reached: `choose="first"` (the default) picks the first; `choose="mode(expr)"` evaluates `expr` on each and picks the first one with the most common value. The context type shall differ from the output's record type.
  - Tests: `TableGeneratorTests.ContextMode_PicksMostCommonSection`
- **LIB-TABLES-091** — If a record has no ancestor of the context type, then an expression reading `context` shall be an evaluation error (LIB-TABLES-043).
  - Tests: `TableGeneratorTests.EvaluationError_SkipsOutputAndReports`

### Record instances

- **LIB-TABLES-100** — Evaluation shall create one record instance per record type and file offset, however many rows and pointers reach it, accumulating each reference (the parent instance or none, the field or table, and the row index); a record's pointer fields are followed only when it is first reached.
  - Tests: `TableEvaluatorTests.SharedChild_YieldsOneInstance`
- **LIB-TABLES-101** — Traversal shall be depth-first in table and field order, and instances shall be ordered by when they were first reached, so the same data and definition always give the same instances, resources and names.
  - Tests: `TableEvaluatorTests.SharedChild_YieldsOneInstance`

### Count check

- **LIB-TABLES-110** — After planning, generation shall read every generated metatile map's blocks through its block format, attribute each block number *n* to the listed set with the greatest base ≤ *n*, and when a set's declared count is at most `n − base` for some number, raise the count to `max(n − base) + 1` and report "Metatile set '<key>' declared N metatiles, maps use up to M, raised to M". If the block format is not registered, the check shall skip that map with a warning.
  - Tests: `TableGeneratorTests.UndercountedSet_RaisedAndReported`

### Plan and report

- **LIB-TABLES-120** — A plan shall list each resource with its key, type, name, folder, model, output and record identity, in build order (LIB-PROJECT-FORMAT-021: compressed data, palettes, metatile sets, tilemaps, metatile maps), with a size summary (`N colors`, `N metatiles`, `WxH blocks`, `WxH tiles`, or the compression format's name), and the folder keys it needs, parents first. Planning shall write nothing.
  - Tests: `TableGeneratorTests.SharedTileset_OneSetReferencedByBothMaps`, `TableGenerationProjectTests.PlanGeneration_WritesNothing`
- **LIB-TABLES-121** — The report shall give the count of planned resources per type, every skipped record with its reason, and the warnings, each warning once.
  - Tests: `TableGeneratorTests.UndercountedSet_RaisedAndReported`, `TableGeneratorTests.EvaluationError_SkipsOutputAndReports`
- **LIB-TABLES-122** — The manifest shall be written beside the definition as `<stem>.manifest.xml`, where the stem is the definition's file name without `.table.xml` (or without `.xml`).
  - Tests: `TableGenerationProjectTests.Generate_WritesResourcesAndManifestInOneTransaction`

### Manifest

- **LIB-TABLES-130** — A manifest shall be an XML file valid against `TableManifestSchema.xsd`: a root `tablemanifest` with `version="1.0"` holding `generation` elements. A `generation` shall have `project` (the project file's path relative to the manifest's directory, `/`-separated), `folder` (the target folder's tree path key, LIB-PROJECT-FORMAT-019, `/` for the project root), `datafile` (the data file's tree path key) and `definition` (the definition's path relative to the manifest's directory, `/`-separated), and one `resource` element per generated resource.
  - Tests: `TableManifestTests.WriteRead_RoundTrip`
- **LIB-TABLES-131** — A `resource` shall have `key` (its tree path key, unique within the generation), `type` (its resource file's root element name: `compresseddata`, `palette`, `metatileset`, `metatilemap`, `tilemap` or `arranger`), `output` (the output id), `record` (the record type it came from, or `palette` for a palette, which is deduplicated as LIB-TABLES-072 says), `offset` (that record's or palette's file offset, hexadecimal without prefix), and optionally `entries` (a palette's entry count) and `index` (the repeat counter `i`). A resource's identity is (`record`, `offset`, `output`, `entries`, `index`), since palettes at one offset with different entry counts are separate resources.
  - Tests: `TableManifestTests.WriteRead_RoundTrip`
- **LIB-TABLES-132** — The reader shall accept any manifest that validates, whoever wrote it, with elements in any order; the writer shall write UTF-8 with a declaration, tab indentation, generations and resources in their model order, and no timestamps.
  - Tests: `TableManifestTests.ForeignWrittenManifest_Reads`, `TableManifestTests.WriteRead_RoundTrip`
- **LIB-TABLES-133** — Writing a generation into an existing manifest shall replace the generation with the same `project` and `folder` (compared ignoring case) and keep every other generation.
  - Tests: `TableManifestTests.Rewrite_KeepsOtherGenerations`
- **LIB-TABLES-134** — If a manifest is not well-formed or fails validation, then reading shall fail naming the file and the line.
  - Tests: `TableManifestTests.Invalid_FailsNamingLine`

### Generated resources

- **LIB-TABLES-140** — Generated resources shall be ordinary resources with no reference to the definition, so the project loads, saves and exports without it.
  - Tests: `TableGenerationProjectTests.Generate_SavesAndReloadsWithoutDefinition`

## Invariants

- Evaluation and planning never write the data file or any project file; they only read the data source.
- One record type and file offset is one instance, and one instance and output (and repeat index) is at most one resource.
- The same data, definition, data file key and target folder give the same plan, byte for byte in the manifest.

## Edge cases

- Two palettes at one address with different entry counts are separate resources.
- A name that renders empty, such as a name table entry of spaces, takes the fallback name (LIB-TABLES-083).
- A resource skipped after naming (LIB-TABLES-076) still took its name, so a later resource with the same rendered name keeps its " 2".
- A pointer row in a pointer table that is null adds no child and does not end the table; only `until` does.
- `index` of an instance reached first through a pointer field is its parent's index, which can differ from a later row that reaches it.
- Dry run (planning) cannot see failures only the project builder raises, such as an unknown codec or overlapping set ranges; they fail the generation itself (LIB-PROJECT-SERVICE).

## Threading and lifetime

- Reader, evaluator and generator are synchronous and single-use per call; a loaded `TableDefinition` is immutable and may be reused.
- The evaluator reads the data source through its own lock (LIB-DATASOURCE); nothing is cached across calls.

## Decisions

- **Definitions are data first; plugins cover the rest.** A declarative definition covers fixed-stride records, bit fields, pointers to records, lookups, expressions, variants, text and the three arranger layouts. A game whose structure is procedural uses a generator plugin (pending). Reason: this follows the codec decision (XML first, C# when a format cannot be expressed). Rejected: plugins only (every game needs C#); a general scripting language (a non-goal and a security surface).
- **Records can point at records.** A pointer field may name the record type or table it points to, and evaluation follows it. Reason: map, tileset and sprite data are trees (FireRed: groups → headers → layouts → tilesets), and flattening them would repeat shared records. Rejected: lookups only, which cannot reach a record through a pointer.
- **One resource per record instance.** Records reached by several paths are identified by record type and file offset, and their outputs are generated once and referenced by key. Reason: 425 FireRed headers reach 309 layouts and 61 tilesets. Rejected: one resource per reference.
- **A small expression grammar, not fixed attributes.** C-style integer expressions with `min`/`max` and `?:`, parsed into a tree at load and interpreted. Reason: real pointers and counts need masks, scales, bank arithmetic and conditions (FireRed's secondary metatile count is a pointer difference). Rejected: embedding C# or Lua expressions. The logical operators `&&`, `||` and `!` are left out: comparisons give 0 or 1, so `&` and `|` combine them, and C precedence already puts comparisons above `&`.
- **Counts are declared, and generation checks them.** A count comes from the definition; when a generated metatile map uses numbers past a set's count, generation raises it to the highest used number and reports it. Reason: FireRed does not store secondary metatile counts, and the pointer-difference rule undercounts tilesets whose attributes are not stored right after their metatiles. Rejected: failing generation (the maps are correct data); inferring counts from usage alone (undercounts sets whose last metatiles are unused).
- **Generated resources are ordinary resources.** Reason: a project loads without the definition and nothing in the project format changes. Rejected: a live `table` resource rendered from the current bytes on every open.
- **Definitions are files, not project resources.** A definition lives anywhere, typically beside the project as `<game>.table.xml`, and its root is foreign to the project reader (LIB-PROJECT-FORMAT-005). Reason: one definition can serve several projects for the same game.
- **The manifest is an open format.** It is XML validated by a published schema and documented in LIB-TABLES-130 to -134, and TileShop treats a manifest written by another program exactly like its own. Reason: a decompilation exporter or a script in any language can generate resources plus a manifest. Rejected: JSON (no schema in use in the project); a TileShop-private format.
- **Charmaps are shared files.** One small XML file per text encoding, referenced by path from any definition. Reason: games of one engine share an encoding (FireRed, LeafGreen and Emerald). Rejected: charmaps inline in each definition.
- **FireRed maps drive phase 1.** The first implementation is built and checked against the FireRed definition with the scratchpad script's 364-map result as the reference; the FF4 definition follows. Reason: user decision; FireRed exercises nested records, deduplication and metatile outputs.
- **Name overrides are part of the definition.** Reason: a game's own text tables are sometimes misleading for a project tree (FireRed section `0xC4`), and the fix belongs with the definition every user of that game shares. Rejected: renaming after generation (lost on regeneration).
- **Relocation is a separate proposal.** Moving a record's data and rewriting its pointer needs a free-space model shared with tilemap re-tile import.
- **Shipped definitions live in `Samples/Tables`.** `FireRed.table.xml` and `gen3.charmap.xml` are kept in the repo as documentation and test fixtures, beside the other samples, and are not copied to the build output. Reason: tables are per game and per revision, so a shipped `_tables` folder that TileShop loads at startup would suggest a completeness it cannot have, while a sample is something to copy and adapt. Rejected: `docs/tables` (not loaded by tests through a stable path beside the code); a shipped `_tables` folder (per-revision data, see above).
- **Models then the project builder.** The generator produces LIB-PROJECT-FORMAT models with absolute keys, and LIB-PROJECT-SERVICE builds them into the open tree with `ProjectTreeBuilder`. Reason: generated resources get exactly the resolution, validation and error messages of a project load, and there is no second construction path to keep in step. Rejected: constructing `MetatileSet`, `Palette` and the others directly in the generator (a copy of the builder's checks).
- **Unmapped bytes as `[XX]`.** A byte the charmap does not map renders as its hex value in brackets and is reported. Reason: the name stays readable and unique, and the report says which byte to add to the charmap. Rejected: dropping the byte (merges names); failing the lookup (loses every name for one stray byte).
- **Sanitize generated names (not user names).** Generated names have forbidden characters removed and fall back to `<output id> <offset>`. Reason: game text holds characters such as `/` and `:` that the name rule forbids, and the user did not type these names, so refusing them would only block generation. The service's rule that typed names are refused, never sanitized (LIB-PROJECT-SERVICE "One rule for names"), still holds for names a user types.
- **The manifest holds one generation per project and folder.** Reason: the manifest sits beside the definition, which several projects for the same game can share, and one project can generate into several folders; keying by both lets each run replace only its own entry. Rejected: one manifest per project (the definition's directory is the only place every user of the definition knows); one per run (stale entries pile up).
- **Template order follows declaration order.** `{name:outputId}` may only name an earlier output, and outputs are named in declaration order. Reason: names never depend on later names, so naming is one pass per output. Folder templates cannot use `{name:…}` or `{n}`, so every folder is known before any name is checked for clashes.
- **`slot`, not `index`, for palette positions.** A palette output's position is its `slot` attribute and identifier. Reason: `index` already means the record's row index. Rejected: overloading `index` inside palette outputs.

## Non-goals

- Relocating data or rewriting pointers.
- Regeneration from a manifest, and the diff and confirmation it needs (phase 2).
- Interpreting resources' contents (attribute bits, scripts, events) beyond the fields a definition names.

## Open items

- Pending: the `snes-lorom`, `snes-hirom`, `nes` and `gb` address mappers (accepted by the schema, refused at load).
- Pending: the scattered arranger output (`arranger` with `rect`, `mask` and `indexmap` layouts), accepted by the schema and refused at load.
- Pending: the generator plugin contract (LIB-PLUGINS).
- Pending: regeneration (phase 2) and the FF4 definition. The UI generation dialog is UI-PROJECT-TREE-138 to -145.
- The manifest is always written beside the definition; a `--manifest` location option could be added for read-only definition folders.
- Manual: generating FireRed's maps from `Samples/Tables/FireRed.table.xml` into a copy of the FireRed project matches the scratchpad script's CLI export pixel for pixel for every layout a header reaches (see the proposal's Status line).
