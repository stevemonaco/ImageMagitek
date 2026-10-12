---
id: LIB-PALETTES
title: Palettes
project: ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek/Colors/Palette.cs
  - ImageMagitek/Colors/ColorSources
  - ImageMagitek/Colors/Serialization/ColorSourceSerializer.cs
  - ImageMagitek/Colors/Serialization/PaletteFileSerializer.cs
  - ImageMagitek/Colors/Serialization/PaletteJsonSerializer.cs
  - ImageMagitek/Colors/SerializationModels/PaletteJsonModel.cs
  - ImageMagitek/Endian.cs
  - ImageMagitek/_palettes
  - ImageMagitek.Services/PaletteService.cs
  - ImageMagitek.Services/Stores/PaletteStore.cs
  - ImageMagitek.Services/BootstrapService.cs
types:
  - Palette
  - PaletteStorageSource
  - IColorSource
  - FileColorSource
  - ProjectNativeColorSource
  - ProjectForeignColorSource
  - ScatteredColorSource
  - ColorField
  - ColorFieldRun
  - ColorFieldException
  - Endian
  - IColorSourceSerializer
  - ColorSourceSerializer
  - PaletteFileSerializer
  - PaletteJsonSerializer
  - PaletteJsonModel
  - IPaletteService
  - PaletteService
  - PaletteStore
tests:
  - PaletteTests
  - ScatteredColorSourceTests
  - PaletteFileSerializerTests
  - PaletteEditSessionTests
  - ProjectServiceTests
  - SettingsServiceTests
  - BootstrapServiceTests
  - GlobalPaletteDesignTests
  - OkLabTests
depends:
  - LIB-DATASOURCE
  - LIB-COLORS
---

# Palettes

## Purpose

A palette is an ordered list of colors in one color model (LIB-COLORS). Each entry comes from a color source: a value in a data file (LIB-DATASOURCE), a value assembled from bit fields scattered through the data file, or a native or foreign value stored in the project. Project palettes are saved in the project (LIB-PROJECT-FORMAT) and can be written back to their data file. Global palettes are read-only JSON files shipped with the app, one of which serves as the default palette for new codecs (LIB-CODECS) and one as the NES master palette. Arrangers assign palettes to elements (LIB-ARRANGERS); the palette editor in TileShop.UI edits them. This spec also covers `BootstrapService.CreatePaletteStore`.

## Requirements

### Entries and sources

- **LIB-PALETTES-001** — A palette shall have one entry per color source, in source order.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`
- **LIB-PALETTES-002** — A file color source shall supply the value of the palette model's storage size read at its byte offset in the palette's data source, interpreted in the palette's color model.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **LIB-PALETTES-003** — A file color source shall read and write 2- and 4-byte values in its endianness, 1-byte values directly, and 3-byte values little-endian whatever its endianness.
  - Tests: untested
- **LIB-PALETTES-004** — If a file color source's model is wider than 4 bytes, then reading it shall throw `NotSupportedException`.
  - Tests: untested
- **LIB-PALETTES-005** — A project foreign color source shall supply its stored foreign value.
  - Tests: `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`
- **LIB-PALETTES-006** — In a project palette, a project native color source shall supply its stored native color as the native entry and its conversion to the palette model as the foreign entry, as in a global palette (LIB-PALETTES-007).
  - Tests: `PaletteTests.ProjectPalette_NativeSource_KeepsNativeAndConvertsForeignToModel`
- **LIB-PALETTES-007** — In a global palette, a native source shall supply its native color and its conversion to the palette model as the foreign entry; a foreign source shall supply its value and its native conversion.
  - Tests: `PaletteTests.SetNativeColor_Components_StoresForeignColorInPaletteModel`
- **LIB-PALETTES-008** — A palette shall mix source kinds freely in one ordered list.
  - Tests: `PaletteEditSessionTests.SetColor_ProjectNative_UpdatesSourceValueInMemoryUntilDiscard`
- **LIB-PALETTES-009** — A scattered color source shall supply the raw value assembled from its fields: each field reads the byte at its offset, takes its width in bits starting at its low bit (bit 0 being the least significant), shifts them left by its shift and ORs them in; bits no field covers are 0. The result is interpreted in the palette's color model.
  - Tests: `ScatteredColorSourceTests.Load_PerChannelByteTables_Bgr15`, `ScatteredColorSourceTests.Load_LowBitInsideByte`, `ScatteredColorSourceTests.Load_NibblePackedStride4_LowNibbleFirst`, `ScatteredColorSourceTests.Load_LowAndHighByteTables`, `ScatteredColorSourceTests.Load_UncoveredBitsAreZero`, `ScatteredColorSourceTests.Load_NesTableModel`
- **LIB-PALETTES-010** — The file run length of a source list shall count consecutive file sources from a start index whose byte offsets are evenly spaced by the given color size in bits divided by 8 and whose endianness matches the first, and be 0 when the start is not a file source.
  - Tests: `SerializationMapperTests.MapToModel_MixedEndianRun_SplitsAtEndianChange`, `SerializationMapperTests.MapToModel_GapInRun_SplitsAtGap`
- **LIB-PALETTES-052** — The scattered run length of a source list shall count consecutive scattered sources from a start index that have the first's field count and, per field, its width and shift, with each field's bit position advancing by one constant positive stride. It shall be 0 when the start is not a scattered source.
  - Tests: `PaletteTests.GetScatteredRun_Count`
- **LIB-PALETTES-053** — If a source list holds a color source of a type the serializer does not know, then loading or saving its colors shall throw `NotSupportedException` naming the type.
  - Tests: `PaletteTests.LoadColors_UnknownColorSource_Throws`, `PaletteTests.StoreColors_UnknownColorSource_Throws`

### Loading and reloading

- **LIB-PALETTES-011** — A palette shall load its colors lazily, on the first color access after construction or `Reload`.
  - Tests: untested
- **LIB-PALETTES-012** — When `Reload` is called, the palette shall discard its loaded colors, including unsaved edits, so the next access re-reads every source, and raise `Changed`.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`, `ProjectServiceTests.PaletteChange_RaisesServiceResourceChangedFromTree`
- **LIB-PALETTES-013** — If a project palette has no data source, then its first color access shall throw.
  - Tests: untested

### Editing colors

- **LIB-PALETTES-014** — When `SetForeignColor` is called, the palette shall store the foreign color at that index, set the native entry to its native conversion, and raise `Changed`.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **LIB-PALETTES-015** — When `SetForeignColor` is called with components, the palette shall create the foreign color from components in the palette model's channel ranges (LIB-COLORS).
  - Tests: untested
- **LIB-PALETTES-016** — When `SetNativeColor` is called, the palette shall set the foreign entry to the color's conversion into the palette model and raise `Changed`; for a project native source it shall store the native color unchanged, and for any other source the native conversion of that foreign entry. Scattered entries also clear uncovered bits (LIB-PALETTES-054).
  - Tests: `PaletteTests.SetNativeColor_Components_StoresForeignColorInPaletteModel`, `PaletteTests.SetNativeColor_FileSource_StoresRoundTrippedNative`
- **LIB-PALETTES-054** — When a foreign or native color is set on a scattered entry, the palette shall clear the raw bits no field covers before storing it, and store the native conversion of that masked foreign color.
  - Tests: `ScatteredColorSourceTests.SetColor_ClearsUncoveredBits`
- **LIB-PALETTES-017** — If `SetForeignColor` or `SetNativeColor` is given an index at or past the entry count, then it shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **LIB-PALETTES-018** — Color edits shall stay in memory and not touch the data source or the color sources until the palette is saved.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **LIB-PALETTES-019** — When `SetColorSources` is called, the palette shall replace its sources and reload.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`
- **LIB-PALETTES-020** — When `SetColorModel` is called, the palette shall change its model, replace its sources with the ones given and reload; the caller supplies file sources respaced for the new color size.
  - Tests: `PaletteEditSessionTests.ChangeColorModel_ThenUndo_RestoresModelAndColors`
- **LIB-PALETTES-055** — If a scattered source has no fields, a low bit outside 0–7, a width outside 1–8, a low bit plus width over 8, a shift outside 0–31, a negative offset, or two fields that overlap in value bits or in file bits, then constructing it shall throw `ColorFieldException` (an `ArgumentException`) naming the field.
  - Tests: `ScatteredColorSourceTests.Construct_InvalidFields_Throws`
- **LIB-PALETTES-056** — If a palette is constructed, or given sources or a color model, where a scattered field's shift plus width exceeds the color model's size, then it shall throw `ColorFieldException` naming the entry and the field, and keep its previous state.
  - Tests: `PaletteTests.Construct_ScatteredFieldOverflowsModel_Throws`, `PaletteTests.SetColorModel_ScatteredFieldOverflowsModel_Throws`
- **LIB-PALETTES-021** — When `ZeroIndexTransparent` changes value, the palette shall raise `Changed`; setting the current value raises nothing.
  - Tests: untested
- **LIB-PALETTES-022** — `ZeroIndexTransparent` shall be a flag for consumers (renderers, PNG export) to treat index 0 as transparent; it does not change the stored colors.
  - Tests: untested

### Saving

- **LIB-PALETTES-023** — When a project palette with a data source is saved, the palette shall write each file source's foreign color to its address, write each scattered source's foreign color, split into its fields, changing only each field's bits in its byte, set each project native source to the current native color and each project foreign source to the current foreign color, and return true.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`, `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`, `ScatteredColorSourceTests.Store_WritesOnlyFieldBits`, `ScatteredColorSourceTests.Store_RoundTripsEveryEntry`
- **LIB-PALETTES-049** — A palette shall be read-only when it is a global palette, or when it has a file or scattered color source and its data source is read-only (LIB-DATASOURCE).
  - Tests: `PaletteTests.IsReadOnly_FileColorsOnReadOnlySource_IsTrue`, `PaletteTests.IsReadOnly_ProjectColorsOnly_IsFalse`, `PaletteTests.IsReadOnly_ScatteredOnReadOnlySource`
- **LIB-PALETTES-024** — When a read-only palette is saved, the palette shall write nothing and return false.
  - Tests: `PaletteTests.SavePalette_ReadOnly_WritesNothingReturnsFalse`
- **LIB-PALETTES-025** — When a palette save writes any file or scattered color, the palette shall flush its data source before returning; saving shall not raise `DataWritten`, and project sources reach disk only when the caller saves the project.
  - Tests: `PaletteTests.SavePalette_FileColor_IsOnDiskWhenSaveReturns`, `PaletteTests.SavePalette_DoesNotRaiseDataWritten`

### Resource behavior

- **LIB-PALETTES-026** — A palette shall contain no child resources and link its data source, when it has one.
  - Tests: untested
- **LIB-PALETTES-027** — A global palette shall have no data source.
  - Tests: untested

### Global JSON palettes

- **LIB-PALETTES-028** — A JSON palette shall be an object with `name` and a `colors` array of `#RRGGBB` or `#RRGGBBAA` strings, matched case-insensitively, with other properties ignored.
  - Tests: `PaletteTests.DeserializePalette_InvalidColor_ThrowsNamingEntry`
- **LIB-PALETTES-029** — A JSON palette shall load as a global palette in the Rgba32 model with one project native source per color.
  - Tests: untested
- **LIB-PALETTES-030** (inherited) — When a JSON palette omits `zeroIndexTransparent`, it shall default to true.
  - Tests: untested
- **LIB-PALETTES-031** — If a JSON palette color does not parse, then loading shall throw `InvalidDataException` naming the palette, the entry index and the bad value.
  - Tests: `PaletteTests.DeserializePalette_InvalidColor_ThrowsNamingEntry`
- **LIB-PALETTES-032** — If the JSON is malformed or lacks `name` or `colors`, then loading shall throw `JsonException`.
  - Tests: untested
- **LIB-PALETTES-033** — If the JSON palette file does not exist, then the palette service shall throw `FileNotFoundException`.
  - Tests: untested
- **LIB-PALETTES-057** — Each shipped 256-color TileShop global palette (`Vivid`, `Synthwave`, `Dusk`) shall have 256 opaque entries.
  - Tests: `GlobalPaletteDesignTests.Has256OpaqueEntries`
- **LIB-PALETTES-058** — In each shipped TileShop global palette, entries 0–3 shall increase in OKLab lightness by at least 0.15 per step.
  - Tests: `GlobalPaletteDesignTests.FirstFour_StepInLightness`
- **LIB-PALETTES-059** — In each shipped TileShop global palette, every pair among entries 0–15 shall be at least 0.12 apart in OKLab distance.
  - Tests: `GlobalPaletteDesignTests.FirstSixteen_PairwiseDistinct`
- **LIB-PALETTES-060** — In each shipped TileShop global palette, each row of 16 entries from entry 16 shall strictly increase in OKLab lightness.
  - Tests: `GlobalPaletteDesignTests.Rows_RampInLightness`

### Palette store

- **LIB-PALETTES-034** — When the bootstrapper creates the palette store, it shall load `<palettes>/<name>.json` for each name in the settings' global palettes, in order, and log, record as a startup issue and skip any that is missing, malformed or empty.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_MissingGlobalPalette_SkipsAndRecordsIssue`, `BootstrapServiceTests.CreatePaletteStore_MalformedJson_SkipsAndRecordsIssue`
- **LIB-PALETTES-035** — The store's default palette shall be the first global palette loaded; setting a default palette that is not in the global list shall append it.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_MalformedJson_SkipsAndRecordsIssue`
- **LIB-PALETTES-036** — When the bootstrapper creates the palette store with an NES palette override, it shall use the override if it loads with at least 64 entries; otherwise it shall record a startup issue and load the settings' NES palette.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_UnusableOverride_FallsBackAndRecordsIssue`
- **LIB-PALETTES-061** — When the bootstrapper creates the palette store with a default palette override, it shall move the loaded global palette of that name, ignoring case, to the front so it becomes the default; if none is loaded, it shall record a startup issue and keep the settings order.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_DefaultOverride_MovesPaletteFirst`, `BootstrapServiceTests.CreatePaletteStore_UnknownDefaultOverride_KeepsSettingsOrderAndRecordsIssue`
- **LIB-PALETTES-050** — If no global palette loads, then creating the palette store shall fail as an essential resource (LIB-SERVICES).
  - Tests: `BootstrapServiceTests.CreatePaletteStore_NoGlobalPalette_ThrowsBootstrapException`
- **LIB-PALETTES-051** — If the settings' NES master palette is missing, malformed or has fewer than 64 entries, then creating the palette store shall fail as an essential resource (LIB-SERVICES).
  - Tests: `BootstrapServiceTests.CreatePaletteStore_ShortNesPalette_ThrowsBootstrapException`
- **LIB-PALETTES-037** — When the store has an NES master palette, the host shall set it on the color factory before any Nes color is converted (LIB-COLORS).
  - Tests: untested
- **LIB-PALETTES-038** — When settings are absent, the global palettes shall be `Vivid`, `Synthwave`, `Dusk` and `Classic`, in that order, and the NES master palette `DefaultNes`.
  - Tests: `SettingsServiceTests.ReadSettings_MissingFile_ReturnsDefaults`, `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`
- **LIB-PALETTES-039** — The app shall ship `Vivid`, `Synthwave`, `Dusk` and `Classic` (256 colors each) and `DefaultNes` (64 colors), all with index 0 not transparent.
  - Tests: `GlobalPaletteDesignTests.ShippedPalette_HasEntriesAndOpaqueZeroIndex`

### Palette files

- **LIB-PALETTES-040** — When colors are written as JASC, the serializer shall write `JASC-PAL`, `0100`, the color count and one `R G B` line per color, with CRLF line endings and alpha dropped.
  - Tests: `PaletteFileSerializerTests.Jasc_RoundTrips`
- **LIB-PALETTES-041** — When colors are written as GIMP, the serializer shall write `GIMP Palette`, a `Name:` line, `Columns: 16`, `#`, and one right-aligned `R G B` line per color followed by a tab and `Index n`, with LF line endings.
  - Tests: `PaletteFileSerializerTests.Gpl_RoundTrips`
- **LIB-PALETTES-042** — When a palette file is read, the serializer shall pick JASC or GIMP from the first non-blank line, accepting CRLF, LF or CR line endings.
  - Tests: `PaletteFileSerializerTests.Jasc_RoundTrips`, `PaletteFileSerializerTests.Gpl_RoundTrips`
- **LIB-PALETTES-043** — If the file is empty or the header is neither, then reading shall fail naming the line.
  - Tests: `PaletteFileSerializerTests.Read_UnknownHeader_Fails`
- **LIB-PALETTES-044** — When a JASC file is read, the serializer shall require version `0100` and a count, then read exactly that many colors and ignore any lines after them.
  - Tests: `PaletteFileSerializerTests.Jasc_RoundTrips`
- **LIB-PALETTES-045** — If a JASC file has fewer colors than its count, then reading shall fail.
  - Tests: `PaletteFileSerializerTests.Read_JascTooFewEntries_Fails`
- **LIB-PALETTES-046** — When a GIMP file is read, the serializer shall skip blank lines, `#` comments and `Name:` and `Columns:` lines, and read every other line as a color.
  - Tests: `PaletteFileSerializerTests.Gpl_RoundTrips`
- **LIB-PALETTES-047** — A color line shall be at least three whitespace-separated integers 0–255, any further text ignored, read as an opaque color.
  - Tests: `PaletteFileSerializerTests.Gpl_RoundTrips`
- **LIB-PALETTES-048** — If a color line does not parse, then reading shall fail naming its 1-based line number.
  - Tests: `PaletteFileSerializerTests.Read_JascBadEntry_NamesLine`, `PaletteFileSerializerTests.Read_GplBadEntry_NamesLine`

## Invariants

- `Entries` always equals the number of color sources.
- After a load or a color edit, each entry's native color is the conversion of its foreign color, except project native sources, whose native color is kept exactly. A scattered entry's native color is the conversion of its masked foreign color.
- A scattered entry's foreign value never has bits set outside its fields.

## Edge cases

- `SetForeignColor` accepts a color of any model; nothing checks it matches the palette's model.
- Negative indexes throw `IndexOutOfRangeException` rather than `ArgumentOutOfRangeException`.
- A global palette whose model is Nes cannot load while native-to-Nes conversion throws (LIB-COLORS).
- A palette file with more than 256 colors reads fully; the caller decides how many to use.

## Threading and lifetime

- Palettes are not thread-safe. They are shared: every editor and arranger element that uses a palette holds the same instance, so pending edits show everywhere at once.
- `Changed` is raised synchronously on the calling thread. `ProjectTree` subscribes to palettes in its index (LIB-PROJECT-TREE).
- The palette store and its global palettes are created once at startup and live for the app.

## Decisions

- **Pending edits live in the shared palette until Save.** Color edits change only the in-memory palette, so every graphics editor previews them live; `SavePalette` writes the data file and updates project sources, and `Reload` discards. The project writer serializes the node's committed model while edits are pending (LIB-PROJECT-FORMAT). Reason: palette edits used to write to the ROM immediately and could not be undone. Rejected: writing through on every edit.
- **`Changed` is the palette's only change signal.** Raised from every color, source, model and transparency change and from `Reload`. Reason: the domain announces its own changes, so no caller has to remember to notify editors. Rejected: UI messages sent by whoever edited the palette.
- **`SetNativeColor` converts into the palette model.** Reason: it used to write an RGBA32 value into the foreign palette whatever the model.
- **Only a project native source keeps an exact native color.** Its foreign entry is the conversion into the palette model, in project and global palettes alike; every other entry's native color is the conversion of its foreign color, including after `SetNativeColor`. Reason: a native source stores a native color, so that is what Save keeps, while a file, scattered or foreign entry can only save what the model holds, so showing more would preview a color that never reaches the file. Project native sources used to report their Rgba32 value as the foreign color whatever the model, so the color editors opened them in the wrong model. Rejected: quantizing native sources too (global palettes and JASC imports into a narrower model would lose precision on display).
- **Bad JSON palette entries fail the load.** The error names the entry. Reason: dropping an unparsable entry shifted every later index. Rejected: skipping with a warning.
- **Global palettes are read-only, and so are palettes with file colors on a read-only data file.** Global palettes have no data source; a palette whose file colors live on a read-only data source cannot write them. `IsReadOnly` covers both, `SavePalette` returns false for it, and the palette editor's read-only mode keys off it; for global palettes the editor offers "Duplicate to project". A palette made only of project colors stays editable on a read-only data file. Reason: the editor's read-only mode already hides every edit path. Rejected: a second, ROM-specific read-only mode that keeps Sources visible (two modes to keep in step).
- **A palette save flushes and stays silent.** The flush makes Save mean "on disk", so a crash after Save keeps the colors. No `DataWritten`, because that event reloads graphics editors and clears their history (LIB-DATASOURCE decision "`DataWritten` is not raised from `Flush`"). Rejected: `Flush(true)` to the physical disk, which image saves do not do either.
- **JASC and GIMP only.** Palette files import into project native colors and export from native colors. Reason: the two common text formats cover the external editors users have. Rejected for now: RIFF `.pal`, `.act` and `.hex`.
- **Scattered fields assemble a raw foreign value; the color model interprets it.** Reason: one mechanism covers per-channel tables, split bytes and table models, and conversion stays in the color factory (ARCHITECTURE §6). Rejected: named R/G/B channel fields (they need per-model channel definitions, and table models such as NES have no channels); per-channel scale factors (scaling is the color model's job; a game needing it gets a color model).
- **A field reads bits from one byte; there is no endianness.** Reason: the user describes each byte a color reads from, the bits taken and where they go, which is how such formats are documented. A wider value is several byte fields, which also places each byte explicitly. Rejected: multi-byte fields with an endian attribute. Endianness combined with bit-granular widths and offsets has no single obvious meaning, and LIB-PALETTES-003 reads 3-byte values little-endian whatever the endianness, which would silently ignore `endian="big"`.
- **Field bits are numbered from the least significant bit (`lowbit`).** Reason: games store channel values in the low bits of a byte, so a table of 0–31 values is `bits="5"` with no offset, and a stride-4 run starts at the low nibble. This deliberately differs from `BitAddress` and the arranger element's `bitoffset`, which count from the most significant bit; the attribute is named `lowbit` so the two are not confused. Rejected: MSB-first `bitoffset` (a 0–31 byte table needs `bitoffset="3"`, and entry 0 of a nibble run is the high nibble).
- **File color sources are byte-addressed.** `FileColorSource.Offset` is a `long` byte offset. Reason: every color model is a whole number of bytes, nothing in the editor creates a bit offset, and the in-memory model then cannot hold a value the format cannot save (LIB-PROJECT-FORMAT drops `bitoffset` from `filesource`). Rejected: keeping `BitAddress` in memory with a byte-only format (the writer would need a refusal rule for a state nothing produces).
- **All fields read from the palette's one data file.** Reason: the feature is non-contiguous storage, not multi-file palettes. A palette still has one data source, so relinking, read-only detection and the missing-file gate need no new rules. Rejected: a per-field or per-source data file.
- **Uncovered bits are 0 and are cleared on assignment.** Some formats store fewer bits than the model holds, for example 4-bit channels placed in the top of a 5-bit Bgr15 channel. Reading them as 0 and clearing them when a color is assigned keeps the palette equal to what Save writes. Rejected: requiring fields to cover every bit of the model (rules out real formats); keeping uncovered bits in memory (shows colors that never reach the file).
- **Scattered runs have a per-field stride in bits.** In memory each entry is its own `ScatteredColorSource` with absolute field positions, as file sources are; `Palette.GetScatteredRun` groups consecutive entries whose fields share widths and shifts and advance by one constant positive stride per field, and `ScatteredColorSource.ExpandRun` expands a run back into entries. Reason: per-channel tables (stride 8) and nibble-packed tables (stride 4) are both common, and runs keep 256-entry palettes readable, like file runs. The writer chooses runs and the reader expands them, so source editing, undo snapshots and the round-trip contract work unchanged. Rejected: one element per entry; a stride in bytes (cannot express packed nibbles); a nibble-order attribute or negative stride for high-nibble-first tables, which have no constant stride and are written as short runs instead (verbose but correct, and rare).
- **The source validates its own fields; the palette checks them against its color model.** A source does not know its palette's model, and Change Color Model keeps the same fields under a new one. So the `ScatteredColorSource` constructor checks field shape and overlap, and the palette (on construction, `SetColorSources` and `SetColorModel`), the project reader and the editor dialogs check that each field fits the model. Errors are a `ColorFieldException` carrying the entry and field index, so the reader and dialogs report them without parsing messages.
- **Within one entry, fields may not overlap in the value or in the file.** Two fields setting the same value bit, or writing the same file bit, make Save order-dependent. Fields of different entries are not cross-checked, matching file sources, which are not checked against each other either. Rejected: a palette-wide overlap check (adds a rule file sources do not have).
- **The NES master palette is configurable.** Its name comes from `appsettings.json`, with a user preference override that TileShop.UI passes to `CreatePaletteStore`, which validates it (at least 64 entries) and falls back to the settings' palette. Reason: the 64-entry rule lives in one place and the CLI gets it too. Rejected: keeping the UI's two-pass fallback (the CLI stays unchecked).
- **The global and NES master palettes are essentials.** See LIB-SERVICES "Essentials are the two schemas, a global palette and a 64-entry NES master palette".
- **New built-in palettes are hand-tuned JSON pinned by design tests, not generated at build.** Colors are tuned by eye in a sequential editor. `GlobalPaletteDesignTests` enforces what must hold (entry count, opaque, lightness steps in 0–3, distinctness in 0–15, monotonic row ramps), so a later tweak cannot regress them. Each palette follows one layout: index 0 is a tinted near-black, 0–3 step in lightness so 2bpp graphics read by lightness alone, 0–15 are clearly distinct for 4bpp, and 16–255 are 15 dark-to-light ramps of one hue family each for 8bpp. Rejected: a checked-in generator (every hand tweak would have to be expressed as generator code); no tests ("high contrast" would be an opinion, not a requirement).
- **Distinctness in OKLab.** OKLab distance tracks perceived difference far better than RGB distance, at a few lines of code with no new package; the conversion lives in the test project because no production code needs it. Rejected: CIEDE2000 (more code for no practical gain here); RGB Euclidean (overrates blue differences).
- **`Vivid` becomes the default; `Classic` stays.** The default palette is what every new codec and every unassigned element uses, and `Classic`'s first 16 colors repeat and blur (three pure blacks). Keeping the old palette, last in the list, keeps projects that reference it by name loading (LIB-PROJECT-FORMAT-023). Changing the default changes how existing projects show elements whose palette is the global default; that is recorded in the release notes. Rejected: replacing `Classic` (breaks projects that name it).
- **Built-in palettes have plain names.** `Vivid`, `Synthwave`, `Dusk` and `Classic` (formerly `DefaultRgba32`, CrystalTile's default) carry no product prefix, since every global palette ships with TileShop. Projects naming `DefaultRgba32` still load (LIB-PROJECT-FORMAT-059). Rejected: a `TileShop` prefix (noise in every palette list).

## Non-goals

- Palette reordering, sub-banks (choosing a 16-color bank of a 256-color palette per element), palette generation from images and color cycling.
- Writing global JSON palettes.

## Open items

- `Palette.HasAlpha` is never set and has no callers; `Palette.GetColor` swaps R and B and has no callers.
- No tests for 3-byte and big-endian file colors, `ZeroIndexTransparent` events, global `SavePalette`, JSON defaults and errors.
