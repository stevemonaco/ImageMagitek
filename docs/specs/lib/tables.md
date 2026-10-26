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
  - SnesLoRomAddressMapper
  - AddressMappers
  - TableExpression
  - TableEvaluator
  - TableEvaluation
  - RecordInstance
  - TableGenerator
  - GenerationPlan
  - PlannedResource
  - GenerationReport
  - RegenerationPlan
  - RegenerationEntry
  - RegeneratedResource
  - KeptResource
  - RegenerationKind
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
  - TableRegenerationTests
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

Generates ordinary project resources (compressed data, palettes, scattered arrangers, metatile sets, metatile maps, tilemaps) from a game's record tables. A **table definition** is a declarative XML file that describes how the game's pointers map to file offsets, its record types and tables, small integer expressions over their fields, its text encodings (**charmaps**) and name tables, and one **output** per kind of resource to create from a record. `TableEvaluator` walks the tables over a `DataSource` (LIB-DATASOURCE) into one `RecordInstance` per record type and file offset; `TableGenerator` turns the instances into a `GenerationPlan` of LIB-PROJECT-FORMAT resource models with names, folders, a report and a **manifest** entry. LIB-PROJECT-SERVICE builds and writes a plan into a project in one transaction (its Generate group). The manifest is an open XML format, specified here so other programs can write one. Running a definition again into a folder its manifest lists regenerates: the plan is matched to the manifest's resources by record path, and only what changed is written (LIB-TABLES-160 to -166). Phase 1 (definitions and generation) and phase 2 (regeneration) are implemented; the generator plugin contract and the remaining mappers are in the backlog.

## Requirements

### Loading

- **LIB-TABLES-001** — A definition shall be read from a file and validated against `TableSchema.xsd`. If the file is not well-formed XML or fails validation, then loading shall fail naming the file and, for each error, its line.
  - Tests: `TableDefinitionReaderTests.SchemaError_NamesFileAndLine`
- **LIB-TABLES-002** — If a definition names a record type, table, lookup, name table, charmap, output or field that it does not declare, declares a name twice (record types, tables, lookups and name tables share one namespace, so a pointer's `to` and a `name[expr]` reference are never ambiguous; output ids share another), names a field with a reserved word (`index`, `address`, `i`, `slot`, `context`, `min`, `max`, `map`, `name`, `n`), or names a record type `palette`, then loading shall fail naming the file, the line, the element and the attribute.
  - Tests: `TableDefinitionReaderTests.UnknownField_FailsAtLoadNamingElementAndAttribute`, `TableDefinitionReaderTests.UnknownRecordType_FailsAtLoad`, `TableDefinitionReaderTests.UnknownCharmap_FailsAtLoad`, `TableDefinitionReaderTests.FieldOutsideRecord_FailsAtLoad`
- **LIB-TABLES-003** — If an expression or a name or folder template does not parse, or names an identifier its scope does not have (LIB-TABLES-041), then loading shall fail naming the file, the line, the element, the attribute and the text, so no expression fails to parse during evaluation.
  - Tests: `TableDefinitionReaderTests.BadExpression_FailsAtLoad`
- **LIB-TABLES-004** — A `charmap` element's `file` shall resolve against the definition's directory, and the charmap file shall be validated against `CharmapSchema.xsd`; if it is missing or invalid, then loading shall fail naming the charmap file.
  - Tests: `TableDefinitionReaderTests.UnknownCharmap_FailsAtLoad`, `TableDefinitionReaderTests.ShippedDefinitions_Load`
- **LIB-TABLES-005** — If a definition uses a pending address mapper (LIB-TABLES-013) or the pending arranger `mask` layout (LIB-TABLES-079), then loading shall fail naming the mapper or layout as not implemented yet.
  - Tests: `AddressMapperTests.PendingMapper_FailsLoad`, `TableDefinitionReaderTests.PendingArrangerLayout_FailsLoad`
- **LIB-TABLES-006** — Loading shall collect every semantic error in the definition before failing, not only the first.
  - Tests: `TableDefinitionReaderTests.UnknownField_FailsAtLoadNamingElementAndAttribute`
- **LIB-TABLES-007** — Integer attributes that are not expressions (sizes, offsets, bit counts, list-literal counts, override values) shall accept decimal and `0x` hexadecimal.
  - Tests: `TableDefinitionReaderTests.ShippedDefinitions_Load`
- **LIB-TABLES-008** — The definitions and charmaps shipped in `Samples/Tables` shall load.
  - Tests: `TableDefinitionReaderTests.ShippedDefinitions_Load`, `TableDefinitionReaderTests.ShippedFf4Definition_Loads`

### Address space

- **LIB-TABLES-010** — The `gba` mapper shall map a pointer in `0x08000000`–`0x09FFFFFF` to the file offset `pointer − 0x08000000`, and treat any other non-zero pointer as unmapped; a file offset maps back to `offset + 0x08000000`.
  - Tests: `AddressMapperTests.Gba_RoundTrips`, `AddressMapperTests.Gba_OutsideRom_IsUnmapped`
- **LIB-TABLES-011** — The `linear` mapper shall map a pointer to `pointer − base` (`base` from the `addressspace` element, 0 by default), treat a pointer below `base` as unmapped, and map a file offset back to `offset + base`.
  - Tests: `AddressMapperTests.Linear_RoundTripsWithBase`
- **LIB-TABLES-012** — For every mapper, the pointer value 0 shall be null: it maps to no offset and is not unmapped.
  - Tests: `AddressMapperTests.Gba_NullPointer`
- **LIB-TABLES-013** — The schema shall accept the mappers `snes-hirom`, `nes` and `gb`; they are pending and refused at load (LIB-TABLES-005). The `snes-lorom` mapper is LIB-TABLES-015.
  - Tests: `AddressMapperTests.PendingMapper_FailsLoad`
- **LIB-TABLES-014** — Every `address` in a definition (tables, lookups, name tables) shall be a pointer in the game's address space, mapped through the address space when used; an address that does not map shall be reported as a warning and the table, lookup entry or name entry treated as unreadable.
  - Tests: `TableEvaluatorTests.UnmappedTableAddress_Warns`
- **LIB-TABLES-015** — The `snes-lorom` mapper shall map a pointer with bank `b = (pointer >> 16) & 0x7F` and address `a = pointer & 0xFFFF` to the file offset `b × 0x8000 + (a − 0x8000) + header`, so banks `0x80` and up mirror banks `0x00` and up. A pointer above `0xFFFFFF`, with an address below `0x8000`, or in banks `0x7E` and `0x7F` (work RAM) shall be unmapped. A file offset shall map back to bank `(offset − header) / 0x8000` (with bit 7 set for banks `0x7E` and up) and address `0x8000 + (offset − header) % 0x8000`.
  - Tests: `AddressMapperTests.SnesLoRom_RoundTrips`, `AddressMapperTests.SnesLoRom_LowHalfAndWram_Unmapped`
- **LIB-TABLES-016** — The `addressspace` element's `header` attribute shall set the copier header size of the `snes-lorom` mapper (0 to `0x7FFF`). Without it, the header shall be 512 when the data length modulo `0x8000` is 512, and 0 otherwise, decided per data source and applied to every pointer the definition maps. If `header` is given for any other mapper than `snes-lorom` or `snes-hirom`, or outside that range, then loading shall fail.
  - Tests: `AddressMapperTests.SnesLoRom_HeaderDetectedFromLength`, `AddressMapperTests.SnesLoRom_ExplicitHeader`, `AddressMapperTests.Header_OnNonSnesMapper_FailsLoad`, `TableGeneratorTests.BankImpliedPointer_MapsThroughLoRom`

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

- **LIB-TABLES-040** — Expressions shall evaluate on 64-bit signed integers with decimal and `0x` literals, parentheses, unary `-` and `~`, `* / %` (truncating), `+ -`, `<< >>` (arithmetic), `< <= > >=`, `== !=`, `&`, `^`, `|` and `?:`, at C precedence and associativity, comparisons giving 1 or 0, any non-zero value counting as true, and the functions `min(a, b, …)`, `max(a, b, …)` and `map(p)` (LIB-TABLES-044). No other syntax is accepted and no code is compiled or run.
  - Tests: `TableExpressionTests.Precedence`, `TableExpressionTests.HexAndDecimal`, `TableExpressionTests.MinMax`, `TableExpressionTests.Ternary`, `TableExpressionTests.ShiftsAndBitwise`
- **LIB-TABLES-041** — An expression's identifiers shall be the fields of the record in scope (with the output's variant, LIB-TABLES-051), `index` (the row index of the table row that first reached the record; a record reached through a pointer field takes its parent's index), `address` (the record's file offset), `i` (the repeat counter, 0 without `repeat`), `slot` (in a palette output, after `slot` itself), `context.<field>` (in an output with `context`), and `<lookup>[expr].<field>`.
  - Tests: `TableExpressionTests.Precedence`, `TableEvaluatorTests.Lookup_ReadsIndexedRecordField`, `TableGeneratorTests.ContextMode_PicksMostCommonSection`
- **LIB-TABLES-042** — Division or modulo by zero, a shift count outside 0–63, a read of an unmapped pointer field, a `map` of a null or unmapped pointer, a lookup index out of range and a missing context shall be evaluation errors.
  - Tests: `TableExpressionTests.DivisionByZero_IsEvaluationError`, `TableExpressionTests.ModuloByZero_IsEvaluationError`, `TableExpressionTests.ShiftsAndBitwise`
- **LIB-TABLES-043** — An evaluation error in an output for one record shall skip that output for that record and be reported as a skipped record naming the output and the error; other records and outputs continue.
  - Tests: `TableGeneratorTests.EvaluationError_SkipsOutputAndReports`
- **LIB-TABLES-044** — `map(p)` shall evaluate to the file offset the address space maps the pointer `p` to (LIB-TABLES-010 to -016, with the detected copier header). If `p` is null or unmapped, then it shall be an evaluation error naming the pointer.
  - Tests: `TableExpressionTests.Map_MapsThroughAddressSpace`, `TableExpressionTests.Map_Unmapped_IsEvaluationError`, `TableGeneratorTests.BankImpliedPointer_MapsThroughLoRom`

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
- **LIB-TABLES-076** — If an output's `tiles`, `paletteslots`, `set` or `palette` reference reaches a resource that is not generated (its output's `when` was false, it was skipped, or the pointer had no record), then the output shall be skipped for that record with a reason naming the reference; outputs that reference the skipped resource are skipped in turn.
  - Tests: `TableGeneratorTests.OutputReferencingSkippedResource_Skipped`, `TableGeneratorTests.ArrangerPaletteNotGenerated_SkipsAndReports`
- **LIB-TABLES-077** — A child element's `when` (on `tiles`, `name`, `folder`) shall select the first child whose `when` holds or that has none.
  - Tests: `TableGeneratorTests.CompressedAndUncompressedTiles`, `TableGeneratorTests.NameTemplates_RenderSegments`
- **LIB-TABLES-078** — Generated models shall reference the data file by the key generation was given, and every other generated resource by the key it is planned at.
  - Tests: `TableGenerationProjectTests.Generate_SavesAndReloadsWithoutDefinition`
- **LIB-TABLES-079** — The schema shall accept the `arranger` output type with a `layout` of `rect`, `mask` or `indexmap`. The `rect` and `indexmap` layouts are LIB-TABLES-150 to -155; `mask` is pending and refused at load (LIB-TABLES-005).
  - Tests: `TableDefinitionReaderTests.PendingArrangerLayout_FailsLoad`

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

- **LIB-TABLES-120** — A plan shall list each resource with its key, type, name, folder, model, output and record identity, in build order (LIB-PROJECT-FORMAT-021: compressed data, palettes, scattered arrangers, metatile sets, tilemaps, metatile maps), with a size summary (`N colors`, `WxH elements`, `N metatiles`, `WxH blocks`, `WxH tiles`, or the compression format's name), and the folder keys it needs, parents first. Planning shall write nothing.
  - Tests: `TableGeneratorTests.SharedTileset_OneSetReferencedByBothMaps`, `TableGenerationProjectTests.PlanGeneration_WritesNothing`, `TableGeneratorTests.BuildOrder_PalettesBeforeArrangers`
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
- **LIB-TABLES-135** — A `resource` may have `path` (its record path, LIB-TABLES-160) and `hash` (the lowercase hexadecimal SHA-256 of the resource file's bytes as written, LIB-PROJECT-SERVICE-074). The writer shall write both for every resource TileShop generated; the reader shall accept a resource with either or neither.
  - Tests: `TableManifestTests.WriteRead_RoundTrip`, `TableManifestTests.ForeignWrittenManifest_Reads`

### Regeneration

A `RegenerationPlan` holds the match below: the added, changed and unchanged resources, the removed, kept and skipped ones, and the `Affected` set of resources the run takes out of the tree. `Affected` is empty from the match and filled by the generation service when it plans (LIB-PROJECT-SERVICE-081), since only it has the tree.

- **LIB-TABLES-160** — Every planned resource shall carry its record's path: the root table's name and row as `<table>[<row>]`, then for each step from that row to the record's first reference (LIB-TABLES-100) `.<field>` for a pointer field that reaches a record, `.<field>[<row>]` for a pointer field that reaches a table, and `[<row>]` for a row of a table a pointer row reaches. A palette's path shall be that of the record whose output first planned it (LIB-TABLES-072).
  - Tests: `TableGeneratorTests.RecordPath_FollowsFirstReference`, `TableGeneratorTests.RecordPath_DedupedPaletteTakesFirstRecord`
- **LIB-TABLES-161** — Given a manifest generation and a plan, regeneration shall match each planned resource to at most one entry and each entry to at most one planned resource: a planned palette first matches an entry with its identity (LIB-TABLES-131), since its path is whichever record planned it first (LIB-TABLES-160); otherwise an entry with a `path` matches the planned resource with the same `type`, `output`, `path` and `index`; an entry without one matches by its identity (LIB-TABLES-131). Matching shall write nothing and need no project.
  - Tests: `TableRegenerationTests.MatchByPath_RepointedRecordIsChanged`, `TableRegenerationTests.EntryWithoutPath_MatchesByOffset`, `TableGenerationProjectTests.Regenerate_RecordSwitchesToPaletteAnotherUses_PalettesUnchanged`, `TableGenerationProjectTests.Regenerate_RecordSwitchesToUnusedPalette_AddsIt`
- **LIB-TABLES-162** — A planned resource with no entry shall be added, an entry with no planned resource shall be removed, and a matched pair shall be changed when the planned model differs from the current resource's model and unchanged otherwise.
  - Tests: `TableRegenerationTests.AddedRemovedChangedUnchanged`, `SerializationModelTests.EveryProperty_ChangesEquality`
- **LIB-TABLES-163** — Before matched models are compared or built, every reference in a planned model to a matched resource shall be rewritten to that resource's current key (LIB-PROJECT-SERVICE-073), so a resource the user moved is still referenced.
  - Tests: `TableRegenerationTests.ReferenceToMovedResource_UsesCurrentKey`, `TableRegenerationTests.WithKeys_RoundTripPreservesEveryProperty`
- **LIB-TABLES-164** — A changed resource shall be described as `repointed` when any file offset in its model differs, `re-paletted` when any palette reference differs, `resized` when its size summary (LIB-TABLES-120) differs, and `changed` when it differs in none of these; one resource may be several.
  - Tests: `TableRegenerationTests.ChangeKinds_Described`
- **LIB-TABLES-165** — The regeneration report shall give "Added N <type>", "Changed N <type>", "Unchanged N <type>" and "Removed N <type>" per type in build order, then "Changed <key>: <kinds>" per changed resource, "Removed <key>" per removed one, each kept resource with its reason (LIB-PROJECT-SERVICE-075 to -077), or as "Kept <key>: references deleted <missing key>" when it is kept for a deleted reference (LIB-TABLES-167), then "Skipped <key>: references deleted <missing key>" per skipped resource, followed by the skipped records and warnings (LIB-TABLES-121).
  - Tests: `TableRegenerationTests.Report_ListsEveryCategory`, `TableRegenerationTests.AddedReferencingDeleted_SkippedWithDependents`, `TableRegenerationTests.ChangedReferencingDeleted_KeptAsIs`
- **LIB-TABLES-166** — After regeneration the manifest's generation shall list every resource written or left unchanged at its current key with its new hash (none for an unchanged resource whose file is missing, so it counts as edited next time), every kept resource (edited, missing, still referenced or referencing a deleted resource) with its old entry at its current key (the entry's own key when missing), and no removed or skipped resource, so a later run adds a skipped resource once the resource it needs is back.
  - Tests: `TableGenerationProjectTests.Regenerate_ManifestListsKeptAndDropsRemoved`, `TableRegenerationTests.AddedReferencingDeleted_SkippedWithDependents`, `TableRegenerationTests.ChangedReferencingDeleted_KeptAsIs`, `TableGenerationProjectTests.Regenerate_ArrangerReferencingDeletedPalette_KeptAsIs`
- **LIB-TABLES-167** — A planned resource that references a planned resource whose entry is missing (LIB-PROJECT-SERVICE-076) shall be skipped when it is added, and reported as "Skipped <key>: references deleted <missing key>", with the skip passing on to the resources that reference it (as LIB-TABLES-076); when it is matched, it shall be kept as it is at its current key and reported as "Kept <key>: references deleted <missing key>", and later resources shall reference it there.
  - Tests: `TableRegenerationTests.AddedReferencingDeleted_SkippedWithDependents`, `TableRegenerationTests.ChangedReferencingDeleted_KeptAsIs`, `TableGenerationProjectTests.Regenerate_ArrangerReferencingDeletedPalette_KeptAsIs`

### Arranger outputs

- **LIB-TABLES-150** — An `arranger` output shall have the expressions `offset` (the file offset of tile 0), `width` and `height` (in elements) and `tilestride` (bytes per tile), the names `codec` and `layout`, and optionally `elementwidth` and `elementheight` (integers 1 to 64, 8 by default) and `palette`, the id of a `palette` output of the same record type without `repeat`. An `indexmap` layout shall also have the expression `mapoffset` and optionally the integers `emptycode` and `runcode` (0 to 255). If a required attribute is missing, `palette` names anything else, or `mapoffset`, `emptycode` or `runcode` is on a `rect` layout, then loading shall fail.
  - Tests: `TableDefinitionReaderTests.ArrangerOutput_MissingAttributes_FailsAtLoad`, `TableGeneratorTests.RectArranger_SequentialElements`
- **LIB-TABLES-151** — A `rect` arranger shall place tile *n* in cell *n*, in rows of `width` cells.
  - Tests: `TableGeneratorTests.RectArranger_SequentialElements`
- **LIB-TABLES-152** — An `indexmap` arranger shall read bytes from `mapoffset` until it has `width × height` cells, in rows: the `emptycode` byte is one empty cell, the `runcode` byte followed by a count byte *n* is *n* empty cells, and any other byte is that tile index. Cells past `width × height` shall be cut off; if the data ends first, then it shall be an evaluation error naming the stream's offset and the cells read (LIB-TABLES-043).
  - Tests: `TableGeneratorTests.IndexmapArranger_SkipCodes`, `TableGeneratorTests.IndexmapStreamPastEnd_SkipsAndReports`
- **LIB-TABLES-153** — The element for tile *t* shall read the data file at `offset + t × tilestride`, bit 0. If `width × height` is more than 65536, or a tile's `tilestride` bytes extend past the end of the data, then it shall be an evaluation error.
  - Tests: `TableGeneratorTests.RectArranger_SequentialElements`, `TableGeneratorTests.BossVariant_SelectsCodecByPointerBit`, `TableGeneratorTests.ArrangerOverElementLimit_SkipsAndReports`, `TableGeneratorTests.ArrangerTilePastEnd_SkipsAndReports`
- **LIB-TABLES-154** — Every element shall use the resource of the `palette` output for the same record, which is the deduplicated palette (LIB-TABLES-072), or no palette (the global default) without `palette`. If that output generated no resource for the record, then the arranger shall be skipped as LIB-TABLES-076 says.
  - Tests: `TableGeneratorTests.MonsterPalettes_DedupedAcrossOutputs`, `TableGeneratorTests.ArrangerPaletteNotGenerated_SkipsAndReports`
- **LIB-TABLES-155** — The arranger shall be a scattered arranger model with the tiled layout, indexed color, `width × height` elements of `elementwidth × elementheight` pixels, each with the output's `codec`, the generation's data file and its cell's position, and no element in an empty cell.
  - Tests: `TableGeneratorTests.RectArranger_SequentialElements`, `TableGeneratorTests.IndexmapArranger_SkipCodes`, `TableGenerationProjectTests.GenerateArrangers_SaveAndReloadWithoutDefinition`

### Generated resources

- **LIB-TABLES-140** — Generated resources shall be ordinary resources with no reference to the definition, so the project loads, saves and exports without it.
  - Tests: `TableGenerationProjectTests.Generate_SavesAndReloadsWithoutDefinition`

## Invariants

- Evaluation and planning never write the data file or any project file; they only read the data source.
- One record type and file offset is one instance, and one instance and output (and repeat index) is at most one resource.
- The same data, definition, data file key and target folder give the same plan, byte for byte in the manifest.

## Edge cases

- Two palettes at one address with different entry counts are separate resources.
- A record repointed to data another record already reaches becomes the same instance (LIB-TABLES-100), so one of the two resources is removed and the other's references change.
- A manifest written before LIB-TABLES-135 has no hashes, so every one of its resources counts as edited on the first regeneration (LIB-PROJECT-SERVICE-075).
- A name that renders empty, such as a name table entry of spaces, takes the fallback name (LIB-TABLES-083).
- A resource skipped after naming (LIB-TABLES-076) still took its name, so a later resource with the same rendered name keeps its " 2".
- A pointer row in a pointer table that is null adds no child and does not end the table; only `until` does.
- `index` of an instance reached first through a pointer field is its parent's index, which can differ from a later row that reaches it.
- An indexmap run that reaches past `width × height` is cut off like any other cell past the end.
- Two monsters that share graphics with different palettes are two arrangers over the same offsets, each with its own palette; two identical records are two arrangers with the same content (FF4's Dark Elf `AB` and `AC`).
- Dry run (planning) cannot see failures only the project builder raises, such as an unknown codec or overlapping set ranges; they fail the generation itself (LIB-PROJECT-SERVICE).

## Threading and lifetime

- Reader, evaluator and generator are synchronous and single-use per call; a loaded `TableDefinition` is immutable and may be reused.
- The evaluator reads the data source through its own lock (LIB-DATASOURCE); nothing is cached across calls.

## Decisions

- **Key rewriting copies models with `with`.** `RegenerationPlan.WithKeys` changes only the name and the reference fields of a model, and the record copy carries every other field, including ones added later (LIB-TABLES-163). Rejected: per-type hand copies, where a forgotten field was silently dropped on regeneration.
- **Definitions are data first; plugins cover the rest.** A declarative definition covers fixed-stride records, bit fields, pointers to records, lookups, expressions, variants, text and the three arranger layouts. A game whose structure is procedural uses a generator plugin (pending): a contract that takes the data file's bytes and returns record descriptions with no core types, so the FF5 samples could become one. Reason: this follows the codec decision (XML first, C# when a format cannot be expressed). Rejected: plugins only (every game needs C#); a general scripting language (a non-goal and a security surface).
- **Records can point at records.** A pointer field may name the record type or table it points to, and evaluation follows it. Reason: map, tileset and sprite data are trees (FireRed: groups → headers → layouts → tilesets), and flattening them would repeat shared records. Rejected: lookups only, which cannot reach a record through a pointer.
- **One resource per record instance.** Records reached by several paths are identified by record type and file offset, and their outputs are generated once and referenced by key. Reason: 425 FireRed headers reach 309 layouts and 61 tilesets. Rejected: one resource per reference.
- **A small expression grammar, not fixed attributes.** C-style integer expressions with `min`/`max` and `?:`, parsed into a tree at load and interpreted. Reason: real pointers and counts need masks, scales, bank arithmetic and conditions (FireRed's secondary metatile count is a pointer difference). Rejected: embedding C# or Lua expressions. The logical operators `&&`, `||` and `!` are left out: comparisons give 0 or 1, so `&` and `|` combine them, and C precedence already puts comparisons above `&`.
- **Counts are declared, and generation checks them.** A count comes from the definition; when a generated metatile map uses numbers past a set's count, generation raises it to the highest used number and reports it. Reason: FireRed does not store secondary metatile counts, and the pointer-difference rule undercounts tilesets whose attributes are not stored right after their metatiles. Rejected: failing generation (the maps are correct data); inferring counts from usage alone (undercounts sets whose last metatiles are unused).
- **Generated resources are ordinary resources.** Reason: a project loads without the definition and nothing in the project format changes. Rejected: a live `table` resource rendered from the current bytes on every open.
- **Definitions are files, not project resources.** A definition lives anywhere, typically beside the project as `<game>.table.xml`, and its root is foreign to the project reader (LIB-PROJECT-FORMAT-005). Reason: one definition can serve several projects for the same game.
- **The manifest is an open format.** It is XML validated by a published schema and documented in LIB-TABLES-130 to -135, and TileShop treats a manifest written by another program exactly like its own. Reason: a decompilation exporter or a script in any language can generate resources plus a manifest. Rejected: JSON (no schema in use in the project); a TileShop-private format.
- **Charmaps are shared files.** One small XML file per text encoding, referenced by path from any definition. Reason: games of one engine share an encoding (FireRed, LeafGreen and Emerald). Rejected: charmaps inline in each definition.
- **FireRed maps drive phase 1.** The first implementation is built and checked against the FireRed definition with the scratchpad script's 364-map result as the reference; the FF4 definition followed and is checked against the project `ff4gen.py` wrote. Reason: user decision; FireRed exercises nested records, deduplication and metatile outputs.
- **`map(p)`, not a bank attribute.** FF4 stores 16-bit pointers whose bank only the code knows (boss tilemaps in bank `$0E`), and definitions name literal bases. `map(0x0E0000 | ptr)` maps such a pointer, and `map(0x098000) + …` keeps a literal base right whether or not the ROM has a copier header. Rejected: a `bank` attribute on pointer fields, which covers only the first case and would add an attribute to fields, tables and lookups; raw file offsets in expressions (wrong on a headered ROM).
- **The tile stride is explicit.** An arranger's `tilestride` is an expression rather than taken from the codec. Reason: the generator needs no codec factory, a dry run resolves no codecs, and a stride that differs from the codec's size can be stated. Rejected: deriving it from the codec.
- **No tile-bias attribute.** A bias on tile indexes is part of `offset` (`offset + bias × tilestride`). Reason: one expression already says it. Rejected: a separate `tilebias` attribute.
- **The copier header is detected from the length.** Without `header`, a `snes-lorom` data source whose length modulo `0x8000` is 512 has a 512-byte header. Reason: one definition serves headered and headerless copies of a ROM, since copier headers are 512 bytes and ROMs are whole banks. Rejected: requiring `header` (two definitions per game); detecting from the header's contents (copiers wrote different contents).
- **FF4's skip list is an index predicate.** The FF4 definition leaves out records `53`, `CA`–`CE` and `D6`–`DE` except `D8` by index, where `ff4gen.py` also compared the name with "Dummy". Reason: the expression grammar is integer-only, and the list is fixed for the game. Rejected: string comparison in expressions (a second value type for one definition). A hack that renames or reuses a dummy record needs the definition edited.
- **Bit 0x40 of an FF4 boss's size byte is not a tile bias.** `ff4boss.py` added `0x100` to the tile index when it is set; `ff4gen.py`, which wrote the reference project, did not. Rendered both ways, Calbrena (`B3`), Elements (`C2`), Defender (`C6`), Titan (`CF`) and Attacker (`D5`) are whole sprites without the bias and noise or other monsters' tiles with it, and all six bosses that set the bit (with MomBomb `A4`) use tile indexes inside the room before the next monster's graphics only without it. The definition uses no bias. Rejected: the `ff4boss.py` reading.
- **Name overrides are part of the definition.** Reason: a game's own text tables are sometimes misleading for a project tree (FireRed section `0xC4`), and the fix belongs with the definition every user of that game shares. Rejected: renaming after generation (lost on regeneration).
- **Relocation is a separate proposal.** Moving a record's data to free space and rewriting its pointer needs a free-space model, which tilemap re-tile import also needs, so the two will be designed together. Reason: generation and regeneration are useful without it, and leaving it out let them ship without changing ARCHITECTURE's "relocating blocks" non-goal. Rejected: relocation as a third phase of table-driven resources (the original plan).
- **Shipped definitions live in `Samples/Tables`.** `FireRed.table.xml`, `FF4.table.xml` and their charmaps `gen3.charmap.xml` and `ff4.charmap.xml` are kept in the repo as documentation and test fixtures, beside the other samples, and are not copied to the build output. Reason: tables are per game and per revision, so a shipped `_tables` folder that TileShop loads at startup would suggest a completeness it cannot have, while a sample is something to copy and adapt. Rejected: `docs/tables` (not loaded by tests through a stable path beside the code); a shipped `_tables` folder (per-revision data, see above).
- **Models then the project builder.** The generator produces LIB-PROJECT-FORMAT models with absolute keys, and LIB-PROJECT-SERVICE builds them into the open tree with `ProjectTreeBuilder`. Reason: generated resources get exactly the resolution, validation and error messages of a project load, and there is no second construction path to keep in step. Rejected: constructing `MetatileSet`, `Palette` and the others directly in the generator (a copy of the builder's checks).
- **Unmapped bytes as `[XX]`.** A byte the charmap does not map renders as its hex value in brackets and is reported. Reason: the name stays readable and unique, and the report says which byte to add to the charmap. Rejected: dropping the byte (merges names); failing the lookup (loses every name for one stray byte).
- **Sanitize generated names (not user names).** Generated names have forbidden characters removed and fall back to `<output id> <offset>`. Reason: game text holds characters such as `/` and `:` that the name rule forbids, and the user did not type these names, so refusing them would only block generation. The service's rule that typed names are refused, never sanitized (LIB-PROJECT-SERVICE "One rule for names"), still holds for names a user types.
- **The manifest holds one generation per project and folder.** Reason: the manifest sits beside the definition, which several projects for the same game can share, and one project can generate into several folders; keying by both lets each run replace only its own entry. Rejected: one manifest per project (the definition's directory is the only place every user of the definition knows); one per run (stale entries pile up).
- **Template order follows declaration order.** `{name:outputId}` may only name an earlier output, and outputs are named in declaration order. Reason: names never depend on later names, so naming is one pass per output. Folder templates cannot use `{name:…}` or `{n}`, so every folder is known before any name is checked for clashes.
- **Regeneration owns only what it created.** A generation writes a manifest listing the resources it created and the record each came from, and regeneration touches only those: it writes the ones that changed, removes those whose record is gone after confirmation, and keeps the ones the user edited or deleted (LIB-TABLES-160 to -167, LIB-PROJECT-SERVICE-073 to -079). Resources the user created in the target folder are never touched. Reason: the project format has no "generated by" marker. Rejected: regenerating a whole folder (deletes the user's own resources); tracking by name pattern (breaks on rename); a live `table` project resource (user decision: a format change and a load-time dependency on the definition).
- **Regeneration matches by record path.** A generated resource is identified by the path from a root table row to its record's first reference, plus its output and repeat index. Reason: a repointed record keeps its path but not its offset, so offset identity would report a repointed map as one removed and one added and lose any move the user made. Manifests without paths (foreign or written before paths) fall back to offset identity. Rejected: offset identity (above); the resource key (breaks when the user renames or moves it, and when a definition's name template changes).
- **A kept resource's entry moves with it.** A kept edited resource the user moved is listed with its old entry (path, hash) at its current key. Reason: an edited resource's hash no longer matches its file, so the next regeneration could not find it by hash and would count it missing. Rejected: keeping the entry's old key (the next run reports the resource missing and stops tracking it).
- **Edits are detected by the file's hash.** The manifest records each resource file's SHA-256 as written; a different hash, a missing hash or unsaved changes mean the user edited it, and regeneration keeps it unless asked to overwrite. The file hash is checked first. A differing hash is checked again: when the file is exactly what TileShop writes for the resource's current model, and that model, with its references to found generated resources put back to their entry keys, serializes to the entry's hash, the file was only rewritten by a move or rename of a resource it references and is not an edit (LIB-PROJECT-SERVICE-075). Reason: the project format has no "generated by" marker, a file hash works the same for resources another program wrote, and moving a generated resource rewrites the files that reference it. Rejected: comparing with a re-serialized model alone (TileShop's formatting differs from other writers' and from legacy files, and a hand-added comment would stop counting as an edit); updating the manifest on a move (the service does not know where manifests live, since they sit beside definitions); always overwriting (loses hand fixes); merging (a non-goal).
- **A resource needing a deleted one is skipped or kept.** An added resource that references a deleted generated resource is skipped, a matched one is kept as it is, and both are reported with the deleted key (LIB-TABLES-167). Reason: deleting a generated resource is how the user says they do not want it (LIB-PROJECT-SERVICE "A deleted generated resource stays deleted"), and one deletion should not block the whole run. Rejected: failing the run (the old behavior, since the builder cannot resolve the deleted key); recreating the deleted resource (undoes the user); pointing the reference at the default palette (changes the resource silently).
- **`slot`, not `index`, for palette positions.** A palette output's position is its `slot` attribute and identifier. Reason: `index` already means the record's row index. Rejected: overloading `index` inside palette outputs.
- **Per-kind regeneration facets live on the resource kinds.** Size summaries, offsets, palette keys and key remapping (LIB-TABLES-120, -163, -164) are members of each resource kind descriptor (LIB-PROJECT-FORMAT-075); `TableGenerator.Summarize` and `RegenerationPlan.WithKeys` dispatch to them, and `ReferencedKeys` still derives from `WithKeys`. `OutputType` and its build order stay in the generator, because they list what a definition can produce, not every resource kind. Rejected: deriving the output types from the registry (a data file is a kind but never an output).

## Non-goals

- Relocating data or rewriting pointers.
- Merging a user's edits to a generated resource with its regenerated contents; an edited resource is kept or overwritten whole.
- Interpreting resources' contents (attribute bits, scripts, events) beyond the fields a definition names.

## Open items

- Pending: the `snes-hirom`, `nes` and `gb` address mappers (accepted by the schema, refused at load).
- Pending: the arranger `mask` layout, accepted by the schema and refused at load. No game in scope uses it, so its bit order cannot be checked.
- Pending: the generator plugin contract (LIB-PLUGINS).
- Manual: hex-edited Pallet Town's block map pointer (layout `0x2DD4C0`, `0x082DD100` to `0x082DD102`) in a ROM copy after generating FireRed into `/Regen` of a project copy: an unedited rerun reported all 1101 resources unchanged and wrote no file; after the edit, regeneration reported only "Changed /Regen/Maps/Pallet Town/Pallet Town: repointed", rewrote only that file (`mapoffset="2DD102"`) and the manifest (its new hash matching the file), and a further run reported everything unchanged.
- A bank-implied 16-bit pointer can be mapped with `map` but not followed with a field's `to`, which maps the raw field value.
- FF4's "All Monsters" overview, which shelf-packs every monster into one arranger with per-element palettes, cannot be expressed: an output makes one resource per record.
- The manifest is always written beside the definition; a `--manifest` location option could be added for read-only definition folders.
- Manual: generating FireRed's maps from `Samples/Tables/FireRed.table.xml` into a copy of the FireRed project (ROM SHA-1 unchanged) gives 618 metatile maps (309 layouts a header reaches and their 309 borders), each pixel-identical to the scratchpad script's CLI export matched by block map offset and size; the 55 unused layouts, their 27 borders and the one tileset only they use are not generated, and secondary sets `0x2A87B4`, `0x2B3524` and `0x2B4E4C` keep their pointer-difference counts (64, 67, 24) because the reached maps stay within them.
- Manual: generating FF4's monsters from `Samples/Tables/FF4.table.xml` into a copy of the FF2 project gives the reference project's 209 arrangers and 195 palettes with the same elements, codecs, palettes and colors, pixel-identical in a CLI export; a copy of the ROM with a 512-byte header gives the same resources at offsets 512 higher.
