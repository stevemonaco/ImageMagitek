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
  - PaletteFileSerializerTests
  - PaletteEditSessionTests
  - ProjectServiceTests
  - SettingsServiceTests
depends:
  - LIB-DATASOURCE
  - LIB-COLORS
---

# Palettes

## Purpose

A palette is an ordered list of colors in one color model (LIB-COLORS). Each entry comes from a color source: a value in a data file (LIB-DATASOURCE), or a native or foreign value stored in the project. Project palettes are saved in the project (LIB-PROJECT-FORMAT) and can be written back to their data file. Global palettes are read-only JSON files shipped with the app, one of which serves as the default palette for new codecs (LIB-CODECS) and one as the NES master palette. Arrangers assign palettes to elements (LIB-ARRANGERS); the palette editor in TileShop.UI edits them. This spec also covers `BootstrapService.CreatePaletteStore`.

## Requirements

### Entries and sources

- **LIB-PALETTES-001** — A palette shall have one entry per color source, in source order.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`
- **LIB-PALETTES-002** — A file color source shall supply the value of the palette model's storage size read at its bit address in the palette's data source, interpreted in the palette's color model.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **LIB-PALETTES-003** — A file color source shall read and write 2- and 4-byte values in its endianness, 1-byte values directly, and 3-byte values little-endian whatever its endianness.
  - Tests: untested
- **LIB-PALETTES-004** — If a file color source's model is wider than 4 bytes, then reading it shall throw `NotSupportedException`.
  - Tests: untested
- **LIB-PALETTES-005** — A project foreign color source shall supply its stored foreign value.
  - Tests: `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`
- **LIB-PALETTES-006** — In a project palette, a project native color source shall supply its stored native color as both the native and the foreign entry, without converting it to the palette model.
  - Tests: untested
- **LIB-PALETTES-007** — In a global palette, a native source shall supply its native color and its conversion to the palette model as the foreign entry; a foreign source shall supply its value and its native conversion.
  - Tests: `PaletteTests.SetNativeColor_Components_StoresForeignColorInPaletteModel`
- **LIB-PALETTES-008** — A palette shall mix source kinds freely in one ordered list.
  - Tests: `PaletteEditSessionTests.SetColor_ProjectNative_UpdatesSourceValueInMemoryUntilDiscard`
- **LIB-PALETTES-009** — A scattered color source shall supply no color: its entry is empty, and saving a palette that contains one throws `NotSupportedException`.
  - Tests: untested
- **LIB-PALETTES-010** — The file run length of a source list shall count consecutive file sources from a start index whose offsets are evenly spaced by the given color size and whose endianness matches the first, and be 0 when the start is not a file source.
  - Tests: untested

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
- **LIB-PALETTES-016** — When `SetNativeColor` is called, the palette shall store the native color unchanged, set the foreign entry to its conversion into the palette model, and raise `Changed`.
  - Tests: `PaletteTests.SetNativeColor_Components_StoresForeignColorInPaletteModel`
- **LIB-PALETTES-017** — If `SetForeignColor` or `SetNativeColor` is given an index at or past the entry count, then it shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **LIB-PALETTES-018** — Color edits shall stay in memory and not touch the data source or the color sources until the palette is saved.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **LIB-PALETTES-019** — When `SetColorSources` is called, the palette shall replace its sources and reload.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`
- **LIB-PALETTES-020** — When `SetColorModel` is called, the palette shall change its model, replace its sources with the ones given and reload; the caller supplies file sources respaced for the new color size.
  - Tests: `PaletteEditSessionTests.ChangeColorModel_ThenUndo_RestoresModelAndColors`
- **LIB-PALETTES-021** — When `ZeroIndexTransparent` changes value, the palette shall raise `Changed`; setting the current value raises nothing.
  - Tests: untested
- **LIB-PALETTES-022** — `ZeroIndexTransparent` shall be a flag for consumers (renderers, PNG export) to treat index 0 as transparent; it does not change the stored colors.
  - Tests: untested

### Saving

- **LIB-PALETTES-023** — When a project palette with a data source is saved, the palette shall write each file source's foreign color to its address, set each project native source to the current native color and each project foreign source to the current foreign color, and return true.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`, `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`
- **LIB-PALETTES-024** — When a global palette is saved, the palette shall write nothing and return false.
  - Tests: untested
- **LIB-PALETTES-025** — Saving a palette shall neither flush the data source nor raise `DataWritten`; the project sources reach disk only when the caller saves the project.
  - Tests: untested

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

### Palette store

- **LIB-PALETTES-034** — When the bootstrapper creates the palette store, it shall load `<palettes>/<name>.json` for each name in the settings' global palettes, in order, and log and skip any that throw `InvalidDataException`.
  - Tests: untested
- **LIB-PALETTES-035** — The store's default palette shall be the first global palette loaded; setting a default palette that is not in the global list shall append it.
  - Tests: untested
- **LIB-PALETTES-036** — When the bootstrapper creates the palette store, it shall load the NES master palette from `<palettes>/<NesPalette>.json`, and if that throws `InvalidDataException`, log it and create the store without one.
  - Tests: untested
- **LIB-PALETTES-037** — When the store has an NES master palette, the host shall set it on the color factory before any Nes color is converted (LIB-COLORS).
  - Tests: untested
- **LIB-PALETTES-038** (inherited) — When settings are absent, the global palettes shall be `DefaultRgba32` and the NES master palette `DefaultNes`.
  - Tests: `SettingsServiceTests.ReadSettings_MissingFile_ReturnsDefaults`, `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`
- **LIB-PALETTES-039** — The app shall ship `DefaultRgba32` (256 colors) and `DefaultNes` (64 colors), both with index 0 not transparent.
  - Tests: untested

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
- After a load or a color edit, each entry's native color is the conversion of its foreign color, except entries set with `SetNativeColor` and project native sources, whose native color is kept exactly.

## Edge cases

- `SetForeignColor` accepts a color of any model; nothing checks it matches the palette's model.
- Negative indexes throw `IndexOutOfRangeException` rather than `ArgumentOutOfRangeException`.
- A global palette whose model is Nes cannot load while native-to-Nes conversion throws (LIB-COLORS).
- An NES master palette with fewer than 64 entries makes Nes indices past its end throw on conversion; only the UI checks the count.
- A palette file with more than 256 colors reads fully; the caller decides how many to use.

## Threading and lifetime

- Palettes are not thread-safe. They are shared: every editor and arranger element that uses a palette holds the same instance, so pending edits show everywhere at once.
- `Changed` is raised synchronously on the calling thread. `ProjectTree` subscribes to palettes in its index (LIB-PROJECT-TREE).
- The palette store and its global palettes are created once at startup and live for the app.

## Decisions

- **Pending edits live in the shared palette until Save.** Color edits change only the in-memory palette, so every graphics editor previews them live; `SavePalette` writes the data file and updates project sources, and `Reload` discards. The project writer serializes the node's committed model while edits are pending (LIB-PROJECT-FORMAT). Reason: palette edits used to write to the ROM immediately and could not be undone. Rejected: writing through on every edit.
- **`Changed` is the palette's only change signal.** Raised from every color, source, model and transparency change and from `Reload`. Reason: the domain announces its own changes, so no caller has to remember to notify editors. Rejected: UI messages sent by whoever edited the palette.
- **`SetNativeColor` converts into the palette model.** Reason: it used to write an RGBA32 value into the foreign palette whatever the model.
- **Bad JSON palette entries fail the load.** The error names the entry. Reason: dropping an unparsable entry shifted every later index. Rejected: skipping with a warning.
- **Global palettes are read-only.** They have no data source and `SavePalette` returns false; the palette editor offers "Duplicate to project" instead.
- **JASC and GIMP only.** Palette files import into project native colors and export from native colors. Reason: the two common text formats cover the external editors users have. Rejected for now: RIFF `.pal`, `.act` and `.hex`.
- **The NES master palette is configurable.** Its name comes from `appsettings.json`, with a user preference override applied at startup by TileShop.UI.

## Non-goals

- Palette reordering, sub-banks (choosing a 16-color bank of a 256-color palette per element), palette generation from images and color cycling.
- Writing global JSON palettes.

## Open items

- `ColorSourceSerializer.LoadColors` treats a project native source as its own foreign color, so a project palette in, for example, Bgr15 reports an Rgba32 foreign color and an unquantized native color for that entry, unlike a global palette.
- `SetNativeColor` keeps the native color as given, so until `Reload` the palette shows a color its model cannot store (255 instead of 248 in Bgr15).
- `SavePalette` never flushes, although the `DataSource.NotifyDataWritten` comment says palette saves flush (LIB-DATASOURCE). The write sits in the file stream's buffer until something else flushes or the source is disposed.
- `BootstrapService.CreatePaletteStore` catches only `InvalidDataException`: a missing global or NES palette file (`FileNotFoundException`) or malformed JSON (`JsonException`) stops startup, and if no global palette loads, `First()` throws.
- The scattered color source is still a stub. The project writer's palette mapping never advances past one, so saving such a palette would loop forever (LIB-PROJECT-FORMAT). The backlog plans removing it from the 1.0 schema.
- The project writer collapses consecutive evenly spaced file sources into one range without comparing endianness, so a mixed-endian run is saved with the first source's endianness; `GetFileRunLength` does compare it.
- `Palette.HasAlpha` is never set and has no callers; `Palette.GetColor` swaps R and B and has no callers.
- No tests for 3-byte and big-endian file colors, `ZeroIndexTransparent` events, global `SavePalette`, JSON defaults and errors, or the palette store.
