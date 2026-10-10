# Scattered color sources

## Why

Some games do not store a palette as contiguous packed colors. The red, green and blue values sit in separate tables, a color's low and high bytes are far apart, or 4-bit channel values are packed two to a byte in per-channel tables. Today a palette can read only whole packed colors (`filesource`), so these palettes cannot be viewed or edited, except by retyping every color as a project color, which loses the link to the data file.

`ScatteredColorSource` was reserved for this case but never designed. It is an empty class. Nothing creates one, the schema has no element for it, the writer's palette mapping loops forever on it (`SerializationMapperExtensions.MapToModel` never advances), `ColorSourceSerializer` returns an empty entry or throws, and the palette editor throws when building its source rows. Backlog items (all **1.0**): the `MapToModel` loop, the palette editor throw, the dead reader branches and `ScatteredColorSourceModel`, and the scattered color sources feature gap. [project-format-freeze](project-format-freeze.md) originally removed the source; it now stays and is implemented here, before the format freezes.

The `bitoffset` attribute on `filesource` is unused. Every color model is 8, 16 or 32 bits, so file runs and Change Color Model respacing always land on whole bytes. The editor creates only byte offsets, so only hand-written XML (and the AllFeatures fixture) carries one. Scattered fields cover sub-byte palette data, so the attribute goes before the format freezes.

## What

A **scattered color source** supplies one palette entry whose raw value is assembled from **fields**. A field takes some bits from one byte of the palette's data file and places them in the raw value. All colors in a palette still come from at most one data file, the palette's own.

Each field has:

- an **offset**: the byte it reads;
- a **low bit** (0–7): the lowest bit it reads, numbered from the least significant bit of the byte;
- a **width** of 1–8 bits, with low bit plus width at most 8;
- a **shift**: where the field's lowest bit lands in the raw value.

A field never spans bytes, so there is no endianness. A value wider than a byte, or a channel split across bytes, is several fields.

The entry's raw value is the OR of each field's value shifted into place. The palette's color model then interprets it like any other foreign value, so every model works, table models (NES) included. Bits of the raw value that no field covers read as 0.

**On disk.** Consecutive scattered entries whose fields advance by a constant stride are written as one element, the way consecutive file colors form one `filesource` run. Per-channel byte tables for Bgr15, each holding 0–31 in the low 5 bits:

```xml
<palette datafile="Roms/Game" color="Bgr15" zeroindextransparent="true">
  <scatteredcolor entries="16">
    <field fileoffset="1A000" bits="5" />             <!-- red table -->
    <field fileoffset="1A010" bits="5" shift="5" />   <!-- green table -->
    <field fileoffset="1A020" bits="5" shift="10" />  <!-- blue table -->
  </scatteredcolor>
</palette>
```

4-bit channels packed two entries per byte (entry 0 in the low nibble), placed in the top of each 5-bit Bgr15 channel:

```xml
<scatteredcolor entries="16">
  <field fileoffset="1B000" bits="4" shift="1" stride="4" />
  <field fileoffset="1B008" bits="4" shift="6" stride="4" />
  <field fileoffset="1B010" bits="4" shift="11" stride="4" />
</scatteredcolor>
```

A Bgr15 color whose low and high bytes sit in separate tables:

```xml
<scatteredcolor entries="16">
  <field fileoffset="2000" bits="8" />
  <field fileoffset="2100" bits="7" shift="8" />
</scatteredcolor>
```

`field` attributes: `fileoffset` (hex, required), `lowbit` (0–7, written only when non-zero), `bits` (1–8, required), `shift` (written only when non-zero) and `stride` in bits between consecutive entries (written only when it is not 8 and the run has more than one entry). Entry *n*'s field reads bit position `fileoffset × 8 + lowbit + n × stride`, counted from the least significant bit of each byte: byte = position / 8, low bit = position mod 8. Every entry's field must stay within one byte.

**Reading and saving.** Loading reads each field's byte and assembles the value. Saving splits the entry's foreign value back into its fields and, for each field, reads its byte, replaces only the field's bits and writes the byte back, so neighboring bits in the file are untouched. Setting a color on a scattered entry clears the raw bits no field covers, so the palette shows what will be written.

**Palette editor.**

- The Sources section shows one row per scattered run: "Scattered · <N> colors · <K> fields", with Edit... and remove buttons.
- Add gains "Scattered colors...".
- Both open the **Scattered Colors** dialog: an entry count, a field grid (offset, low bit, bits, shift, stride) with Add Field and Remove buttons, inline validation, and a live swatch preview of the colors the fields decode to. OK applies the run as one "Edit sources" undo step.
- Assign, paste, swap and gradient work on scattered entries as on file entries: pending until Save, then written to the data file.
- The active color's source line reads "Scattered: <K> fields at 0x1A000, 0x1A010, 0x1A020".

**Change color model.** Fields keep their offsets, low bits, widths, shifts and strides. A field whose shift plus width exceeds the new model's size fails the preview, naming the field.

**File color sources are byte-addressed.** `filesource` loses `bitoffset`, so a project that still has one fails schema validation. `FileColorSource.Offset` becomes a byte offset, and the source line loses its " bit <n>" suffix.

**What does not change.** Project native and project foreign sources; file sources apart from their offset type; the arranger element's `bitoffset`, which keeps its name and MSB-first meaning; palettes with no scattered entries and no `bitoffset` load and save byte-identically; a palette still has one data file.

## Decisions

- **Fields assemble a raw foreign value; the color model interprets it.** Reason: one mechanism covers per-channel tables, split bytes and table models, and conversion stays in the color factory (ARCHITECTURE §6). Rejected:
  - Named R/G/B channel fields. These need per-model channel definitions, and table models such as NES have no channels.
  - Per-channel scale factors. Scaling is the color model's job; a game needing it gets a color model.
- **A field reads bits from one byte; there is no endianness.** Reason: the user describes each byte a color reads from, the bits taken and where they go, which is how such formats are documented. A wider value is several byte fields, which also places each byte explicitly. Rejected: multi-byte fields with an endian attribute. Endianness combined with bit-granular widths and offsets has no single obvious meaning, and LIB-PALETTES-003 reads 3-byte values little-endian whatever the endianness, which would silently ignore `endian="big"`.
- **Field bits are numbered from the least significant bit (`lowbit`).** Reason: games store channel values in the low bits of a byte, so a table of 0–31 values is `bits="5"` with no offset, and a stride-4 run starts at the low nibble. This deliberately differs from `BitAddress` and the `bitoffset` attribute elsewhere, which count from the most significant bit; the attribute is named `lowbit` so the two are not confused. Rejected: MSB-first `bitoffset` (a 0–31 byte table needs `bitoffset="3"`, and entry 0 of a nibble run is the high nibble).
- **File color sources lose `bitoffset`; offsets are bytes in memory too.** Reason: no color model has a size that isn't a whole number of bytes, nothing in the editor creates a bit offset, and there are no projects in the wild to break. With it gone, `lowbit` is the only sub-byte attribute in a palette file, so the two bit-numbering conventions never meet in one file. `FileColorSource.Offset` becomes a `long` byte offset, so the in-memory model cannot hold a value the format cannot save. Rejected: keeping the attribute for future color models (an additive attribute can return in a later minor version); keeping `BitAddress` in memory with the format byte-only (the writer would then need a refusal rule for a state nothing produces).
- **The arranger element keeps `bitoffset`.** Elements are bit-addressed: a sequential arranger advances by the codec's storage size, which is not a whole number of bytes for codecs such as a 1bpp flow codec at 6×6 (36 bits) or a plugin codec, and bit-wise sequential offsets are planned (backlog P1). Those addresses carry into scattered arrangers. The name stays: it is the bit position after `fileoffset` in a bit stream, the same meaning as `BitAddress.BitOffset`, while `lowbit` names a bit within one byte. Rejected: renaming it (`bit`, `fileoffsetbits`), which churns the schema, fixtures and the compression proposal's `compresseddata` for no gain once `filesource` no longer shares the name.
- **All fields read from the palette's one data file.** Reason: this is the user-facing definition (non-contiguous storage, not multi-file palettes). A palette still has one `datafile`, so relinking, read-only detection and the missing-file gate (UI-GRAPHICS-EDITOR-041) need no new rules. Rejected: a per-field or per-source data file.
- **Uncovered bits are 0 and are cleared on assignment.** Some formats store fewer bits than the model holds, for example 4-bit channels placed in the top of a 5-bit Bgr15 channel. Reading them as 0 and clearing them when a color is assigned keeps the palette equal to what Save writes. Rejected: requiring fields to cover every bit of the model (rules out real formats); keeping uncovered bits in memory (shows colors that never reach the file).
- **Runs with a per-field stride, in bits, default 8.** Reason: per-channel tables (stride one byte) and nibble-packed tables (stride 4 bits) are both common, and one element per run keeps 256-entry palettes readable by hand, like `filesource` runs. The stride is a positive number of bits. Rejected:
  - one element per entry (a 256-color palette becomes 256 elements of 3 fields);
  - a stride in bytes (cannot express packed nibbles);
  - a nibble-order attribute or negative stride for high-nibble-first tables. Those tables have no constant stride, so the writer stores them as runs of two entries: verbose but correct, and rare enough not to justify another attribute.
- **The writer chooses runs; the reader expands them.** In memory each entry is its own `ScatteredColorSource` with absolute field positions, as `FileColorSource` is. The writer merges consecutive entries into one run when the field count matches and, for each field, the width and shift match and the bit position advances by one constant stride. Reason: it mirrors file runs (LIB-PALETTES-010, LIB-PROJECT-FORMAT-011), so source-list editing, undo snapshots and the round-trip contract work unchanged.
- **The source validates its own fields; the palette checks them against its color model.** A source does not know its palette's model, and Change Color Model keeps the same fields under a new one. So the `ScatteredColorSource` constructor checks field shape and overlap, and the palette (on construction, `SetColorSources` and `SetColorModel`), the reader and the editor dialogs check that each field fits the model.
- **Within one entry, fields may not overlap in the value or in the file.** Two fields setting the same value bit, or writing the same file bit, make Save order-dependent. Fields of different entries are not cross-checked, matching file sources, which are not checked against each other either. Rejected: a palette-wide overlap check (adds a rule file sources do not have).
- **A dialog edits fields, not inline rows.** A run has an entry count plus a table of fields, which does not fit the one-line rows file and project sources use. The dialog also gives the live preview needed to find the right offsets. Rejected: expanding rows inline (the Sources section becomes a nested grid).

## Spec changes

**LIB-PALETTES**

- **-002** changed: "read at its bit address" becomes "read at its byte offset".
- **-009** changed: A scattered color source shall supply the raw value assembled from its fields: each field reads the byte at its offset, takes its width in bits starting at its low bit (bit 0 being the least significant), shifts them left by its shift and ORs them in; bits no field covers are 0. The result is interpreted in the palette's color model.
- **-010** changed: "offsets are evenly spaced by the given color size" becomes "byte offsets are evenly spaced by the given color size in bytes".
- Added beside -010: The scattered run length of a source list shall count consecutive scattered sources from a start index that have the first's field count and, per field, its width and shift, with each field's bit position advancing by one constant positive stride. It shall be 0 when the start is not a scattered source.
- **-023** changed: also "write each scattered source's foreign color, split into its fields, changing only each field's bits in its byte".
- **-025** changed: "file color" becomes "file or scattered color".
- **-049** changed: "...or when it has a file or scattered color source and its data source is read-only".
- Added (Editing): When a foreign or native color is set on a scattered entry, the palette shall clear the raw bits no field covers before storing it.
- Added (Validation): If a scattered source has no fields, a low bit outside 0–7, a width outside 1–8, a low bit plus width over 8, a negative shift, or two fields that overlap in value bits or in file bits, then constructing it shall throw `ArgumentException` naming the field.
- Added (Validation): If a palette is constructed, or given sources or a color model, where a scattered field's shift plus width exceeds the color model's size, then it shall throw `ArgumentException` naming the entry and the field.
- Open items: delete "The scattered color source is still a stub…".

**LIB-PROJECT-FORMAT**

- **-011** changed: drop "`bitoffset` only when non-zero"; read back "from that byte offset". The schema no longer declares `bitoffset` on `filesource`. Tests: drop `MapToModel_BitOffsetRun_KeepsBitAddress`, `FileSource_BitOffset_LoadsAtBitAddress` and `FileSource_BitOffset_Written`; add `XmlProjectReaderTests.FileSource_BitOffset_FailsValidation`.
- **-017** unchanged (elements keep `bitoffset`).
- Added after -011: Consecutive scattered sources that form one run shall be written as one `scatteredcolor` with an `entries` count and one `field` per field. Each field has a hex `fileoffset`, `lowbit` only when non-zero, `bits`, `shift` only when non-zero, and `stride` in bits only when it is not 8 and the run has more than one entry. They shall be read back as that many sources, entry *n*'s field at bit position `fileoffset × 8 + lowbit + n × stride`, counted from the least significant bit of each byte.
- Added: If a `scatteredcolor` field does not fit the palette's color model, overlaps another field of the run, or for some entry would cross a byte boundary, then the load shall fail naming the file, the entry index and the field.
- **-014** changed: the 256-source limit counts entries, so a `scatteredcolor` run counts as `entries` sources. The schema caps `entries` at 256 and `field` at 32 per run, and the reader enforces the total.
- **-036** changed: drop "on a scattered color source". Mapping any other color source type shall throw `NotSupportedException` naming the type. Saving palettes that have no data file is left to a separate backlog item.
- Open items: delete the Scattered color source bullet.

**UI-PALETTE-EDITOR**

- **-005** changed: drops " bit <n>" from "File offset 0x<hex>", and adds "Scattered: <K> fields at 0x<hex>, …" (each field's byte offset).
- **-060** changed: adds one row per scattered run: "Scattered · <N> colors · <K> fields", with an Edit... button.
- **-061** changed: the Add menu also offers "Scattered colors...", which opens the Scattered Colors dialog for a new run added at the end.
- **-063** changed: pending colors also carry over on scattered entries whose fields are unchanged.
- **-066** changed: also when a scattered field would read past the end of the data file. The status message is renamed from "A file source extends past the end of its data file" to "A source extends past the end of its data file".
- **-077** changed: also "keep scattered fields unchanged; if a field's shift plus width exceeds the new model's size, the preview shall fail naming the field".
- **-090**, **-091** changed: "file colors on a read-only data file" becomes "file or scattered colors on a read-only data file".
- Added (Scattered Colors dialog, new group):
  - The dialog shall show an entry count (1–256), a field grid (offset hex, low bit 0–7, bits 1–8, shift, stride in bits), Add Field and Remove buttons, and a swatch preview of the run's decoded colors that updates as fields change.
  - A new run shall start with 1 entry and one field per byte of the model's size, at consecutive offsets from 0 with shifts 0, 8, 16…, 8 bits each except the last, which takes the remainder, and stride 8: the layout of a little-endian file color.
  - If a field is invalid (does not parse, out of range, low bit plus width over 8, does not fit the model, overlaps another field, crosses a byte boundary for some entry, or reads past the end of the data file), then the dialog shall show the error on that field, clear the preview, and disable OK.
  - When OK is pressed, the editor shall replace the run (or add it) and apply the sources as one "Edit sources" step (-062).
  - Cancel shall change nothing.
- `types`: `ScatteredColorSourceModel` stays (now used), add `ScatteredColorsViewModel` and `ColorFieldModel`.
- Edge cases: delete "A palette with a scattered color source cannot be opened".
- Open items: delete "Palettes with a scattered color source throw on open", and drop "`ScatteredColorSourceModel` is unused" from the dead-code bullet.

## Tasks

1. **Library model and serializer.**
   - Code:
     - `FileColorSource.Offset` becomes a `long` byte offset; update its users (`Palette.GetFileRunLength`, `ColorSourceSerializer`, the mapper, `PaletteEditSession`, `PaletteEditorViewModel`, `ChangeColorModelViewModel`, `PaletteHistoryAction`, both FF5 sample serializers, and the tests).
     - `ColorField` (byte offset, low bit, bits, shift).
     - `ScatteredColorSource` holds `IReadOnlyList<ColorField>` and validates field shape and overlap on construction.
     - `ColorSourceSerializer.LoadColors` reads one byte per field and assembles the value; `StoreColors` splits it and read-modify-writes each field's byte. Neither goes through `ReadFileColorValue`/`WriteFileColor`.
     - `Palette` checks scattered fields against the model on construction, `SetColorSources` and `SetColorModel`. `SetForeignColor`/`SetNativeColor` clear uncovered bits on scattered entries; `IsReadOnly` and the flush in `SavePalette` count scattered sources.
     - `Palette.GetScatteredRunLength`.
     - Unknown `IColorSource` types throw `NotSupportedException` in `LoadColors`, `StoreColors` and `MapToModel`.
   - Tests:
     - `ScatteredColorSourceTests` (new, `ColorTests/`): `Load_PerChannelByteTables_Bgr15`, `Load_LowBitInsideByte`, `Load_NibblePackedStride4_LowNibbleFirst`, `Load_LowAndHighByteTables`, `Load_UncoveredBitsAreZero`, `Load_NesTableModel`, `Store_WritesOnlyFieldBits` (sentinel bits around and between fields unchanged, including two fields sharing one byte), `Store_RoundTripsEveryEntry`, `SetColor_ClearsUncoveredBits`, `Construct_InvalidFields_Throws` (theory: no fields, width 0 and 9, low bit 8, low bit plus width over 8, negative shift, value overlap, file overlap).
     - `PaletteTests`: `Construct_ScatteredFieldOverflowsModel_Throws`, `SetColorModel_ScatteredFieldOverflowsModel_Throws`, `IsReadOnly_ScatteredOnReadOnlySource`, `LoadColors_UnknownColorSource_Throws`, `StoreColors_UnknownColorSource_Throws`, `GetScatteredRunLength` theory.
2. **Project format.**
   - Code:
     - Remove `bitoffset` from `filesource` in the schema, reader, writer and `FileColorSourceModel`; remove it from the AllFeatures `Palettes/Mixed.xml` fixture (the element `bitoffset` in `Sprites/Indexed.xml` stays).
     - Schema `scatteredcolor`/`field` elements.
     - `ScatteredColorSourceModel` in `SerializationModels/ColorSources`, mapped both ways with run merging.
     - Reader validation of fields.
     - Delete the reader's `import`/`export` branches and the commented-out mapping.
   - Tests:
     - `SerializationMapperTests`: `MapToModel_ScatteredRun_MergesByStride`, `MapToModel_ScatteredMismatchedStride_SplitsRuns`, `MapToModel_HighNibbleFirst_WritesRunsOfTwo`, `MapToModel_UnknownColorSource_ThrowsNamingType` (returns, does not hang).
     - Delete `SerializationMapperTests.MapToModel_BitOffsetRun_KeepsBitAddress`, `XmlProjectReaderTests.FileSource_BitOffset_LoadsAtBitAddress` and `XmlProjectWriterTests.FileSource_BitOffset_Written`.
     - `XmlProjectReaderTests`: `FileSource_BitOffset_FailsValidation`, `ScatteredColor_ReadsRun`, `ScatteredColor_DefaultStrideShiftAndLowBit`, `ScatteredColor_FieldOverflowsModel_FailsNamingEntry`, `ScatteredColor_OverlappingFields_Fails`, `ScatteredColor_StrideCrossesByte_FailsNamingEntry`.
     - `XmlProjectRoundTripTests`: extend the AllFeatures fixture with a scattered palette (a byte-table run and a nibble run) used by an arranger; the existing round-trip assertions cover it.
     - `XmlProjectWriterTests`: `ScatteredColor_WritesMinimalAttributes`.
3. **Palette editor.**
   - Code:
     - `ScatteredColorSourceModel` (TileShop.Shared) as a source row.
     - `ScatteredColorsViewModel` plus its view, with a view registration in `ConfigureViewLocator`.
     - The Add menu entry and the source line.
     - Change Color Model handling.
     - Carry-over in `PaletteEditSession`.
   - Tests:
     - `PaletteEditSessionTests`: `ApplySources_ScatteredUnchanged_CarriesPendingColors`, `ApplySources_ScatteredChanged_RereadsFile`, `ScatteredPastEnd_NotApplied`.
     - `ScatteredColorsViewModelTests` (new, `PaletteEditorTests/`): `NewRun_DefaultsToLittleEndianLayout`, `InvalidField_DisablesOkAndClearsPreview` (theory over each invalid case), `Preview_UpdatesOnFieldChange`, `Ok_ReplacesRunAsOneStep`.
     - `ChangeColorModelViewModel`: `ScatteredFieldTooWide_FailsPreview`.
   - Verify in the running app with DevTools: in the FF2 project, add a palette, then Sources → Add → Scattered colors... (the flyout is a popup; reach the dialog through the bound command if the flyout cannot be driven). Enter three byte-table fields, check the preview swatches, OK, and check the Sources row text and the source line. Undo and redo.
   - Manual: Sources → Add flyout shows "Scattered colors..."; assign a color to a scattered entry, Save, reopen, and check the file bytes changed only in the field bits (hex view outside TileShop).
4. **Specs and backlog.**
   - Apply the spec changes above, with real test names on each `Tests:` line.
   - Delete from `docs/BACKLOG.md` the `MapToModel` loop item, the palette-editor scattered-source item, the scattered color sources feature gap, and the scattered/`import`/`export` dead-code line, and drop `ScatteredColorSourceModel` from the UI-PALETTE-EDITOR dead-code line.
   - Delete this proposal. Run `dotnet test ImageMagitek.UnitTests`.

## Open questions

None.
