---
id: LIB-ARRANGERS
title: Arrangers and elements
project: ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek/Arranger/Arranger.cs
  - ImageMagitek/Arranger/ArrangerElement.cs
  - ImageMagitek/Arranger/SequentialArranger.cs
  - ImageMagitek/Arranger/ScatteredArranger.cs
  - ImageMagitek/Arranger/TileLayout.cs
  - ImageMagitek/Arranger/ArrangerExtensions.cs
  - ImageMagitek/Arranger/ArrangerCopy.cs
  - ImageMagitek/Arranger/ElementCopier.cs
  - ImageMagitek/Arranger/ArrangerSaveConflictExtensions.cs
  - ImageMagitek/Builders/ArrangerBuilder.cs
  - ImageMagitek/_layouts
  - ImageMagitek.Services/ElementLayoutService.cs
  - ImageMagitek.Services/Stores/ElementStore.cs
types:
  - Arranger
  - SequentialArranger
  - ScatteredArranger
  - ArrangerElement
  - ArrangerMode
  - ElementLayout
  - PixelColorType
  - ArrangerMoveType
  - MirrorOperation
  - RotationOperation
  - TileLayout
  - ArrangerExtensions
  - ArrangerCopy
  - ElementCopy
  - ElementCopyExtensions
  - ElementCopier
  - ElementSaveStatus
  - ElementSaveState
  - ElementSaveConflicts
  - ScatteredArrangerSaveConflictExtensions
  - ArrangerBuilder
  - IElementLayoutService
  - ElementLayoutService
  - ElementStore
tests:
  - TileLayoutTests
  - ArrangerBuilderTests
  - SaveConflictTests
  - SequentialArrangerRoundTripTests
  - ReadOnlyArrangerTests
  - GraphicsEditHistoryTests
  - IndexedImageTests
  - ImageImporterTests
depends:
  - LIB-DATASOURCE
  - LIB-CODECS
  - LIB-PALETTES
---

# Arrangers and elements

## Purpose

An arranger is a 2D grid of elements, each naming a data source, a bit address, a codec (and through an indexed codec, a palette) and a display mirror/rotation. A sequential arranger lays contiguous data from one source across the grid for browsing; a scattered arranger places arbitrary elements and is the project resource. LIB-IMAGES decodes arrangers to pixels and writes them back; LIB-PROJECT-FORMAT persists scattered arrangers; missing-source and same-project checks are in LIB-PROJECT-SERVICE.

## Requirements

### Grid and elements

- **LIB-ARRANGERS-001** — The arranger shall report its size in elements, the pixel size of one element, and its pixel size as their product.
  - Tests: `ArrangerBuilderTests.Build_ScatteredArranger`
- **LIB-ARRANGERS-002** — When an element is set at a grid position, the arranger shall store it relocated to that position's pixel coordinates (scattered) and refuse an element whose codec color type differs from the arranger's.
  - Tests: untested
- **LIB-ARRANGERS-003** — When an element position is reset, the arranger shall leave that cell empty; an empty cell renders as blank and is skipped by save, export and import.
  - Tests: `ImageImporterTests.Prepare_UndefinedElement_IsSkipped`
- **LIB-ARRANGERS-004** — When asked for the element at a pixel outside the arranger, the arranger shall throw an out-of-range error.
  - Tests: untested
- **LIB-ARRANGERS-005** — The arranger shall enumerate elements and element locations row by row, left to right, for the whole grid, an element rectangle, or every element touched by a pixel rectangle; an empty pixel rectangle yields nothing.
  - Tests: untested
- **LIB-ARRANGERS-006** — The arranger shall report the distinct palettes and the distinct codecs its elements use, and as linked resources every palette and data source its elements reference.
  - Tests: untested
- **LIB-ARRANGERS-007** — When a data source is unlinked, the arranger shall empty every element that reads from it and report whether any changed; unlinking a palette shall change nothing and report false.
  - Tests: untested
- **LIB-ARRANGERS-008** — An element shall report itself within its source only when its whole encoded storage lies before the end of the source.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`
- **LIB-ARRANGERS-009** — The arranger shall be read-only when any element uses a codec that cannot encode.
  - Tests: `ReadOnlyArrangerTests.IsReadOnly_DecodeOnlyCodec_IsTrue`, `ReadOnlyArrangerTests.IsReadOnly_EncodableCodec_IsFalse`
- **LIB-ARRANGERS-010** — When a pixel point outside the arranger is converted to an element location, the conversion shall throw an out-of-range error.
  - Tests: untested

### Mirror and rotation

- **LIB-ARRANGERS-011** — When an element is mirrored, its mirror state shall compose with the requested mirror (the same axis twice cancels; horizontal plus vertical is both), and the call shall fail without change when the cell is empty.
  - Tests: `GraphicsEditHistoryTests.Mirror_UndoRedo`
- **LIB-ARRANGERS-012** — When an element is rotated, its rotation shall compose with the requested quarter turns (left then right cancels; two lefts are a turn), and the call shall fail for an empty cell, for a non-square element, or for a location past the grid.
  - Tests: `GraphicsEditHistoryTests.Rotate_UndoRedo`
- **LIB-ARRANGERS-013** — Mirroring or rotating with `None` shall succeed without change.
  - Tests: untested
- **LIB-ARRANGERS-014** — Mirror and rotation shall change only how the element is displayed and encoded; the source bytes are unchanged until the image is saved.
  - Tests: `IndexedImageTests.MirrorAndRotation_RoundTrip`

### Scattered arranger

- **LIB-ARRANGERS-015** — If a scattered arranger is created with a non-positive grid or element size, or with the Single layout and a grid other than 1×1, then creation shall fail.
  - Tests: untested
- **LIB-ARRANGERS-016** — When a scattered arranger is resized, it shall keep the elements in the overlapping top-left region and leave new cells empty; a size below 1 shall fail.
  - Tests: untested

### Sequential arranger

- **LIB-ARRANGERS-017** — When a sequential arranger is created, it shall take its name from the data source, use a clone of the given codec for every element, take its color type and Tiled/Single layout from the codec, start at address 0 with the 1×1 default tile layout, and lay out the requested grid.
  - Tests: `ArrangerBuilderTests.Build_SequentialArranger`
- **LIB-ARRANGERS-018** — The sequential arranger shall read contiguous data: each tile-layout block takes the next elements in pattern order, blocks fill the grid row by row, and each tiled element starts one codec storage size after the previous.
  - Tests: `SequentialArrangerRoundTripTests.RoundTrips_AtNonzeroOffset_AndAfterCodecResize`
- **LIB-ARRANGERS-019** — When moved to an absolute bit address, the sequential arranger shall go there, clamping to the last address that keeps the whole arranger inside the file, or to 0 when the arranger needs more bits than the file has.
  - Tests: `SequentialArrangerRoundTripTests.RoundTrips_AtNonzeroOffset_AndAfterCodecResize`
- **LIB-ARRANGERS-020** — When moved by a move type, the sequential arranger shall step by one byte, one row of layout blocks, one layout block, or half the arranger (page), or jump to the start or the end, clamped between 0 and the last whole-arranger address, and report the new address.
  - Tests: untested
- **LIB-ARRANGERS-021** — When a sequential arranger is resized, it shall re-lay the grid from its current address and re-clamp that address to the new size.
  - Tests: untested
- **LIB-ARRANGERS-022** — When the codec is changed, the sequential arranger shall adopt the codec's element size, color type and layout, keep its address, optionally resize, and re-lay the grid.
  - Tests: `SequentialArrangerRoundTripTests.RoundTrips_AtNonzeroOffset_AndAfterCodecResize`
- **LIB-ARRANGERS-023** — When the palette is changed, every indexed element of the sequential arranger shall use the new palette.
  - Tests: `SequentialArrangerRoundTripTests.RoundTrips_AtNonzeroOffset_AndAfterCodecResize`
- **LIB-ARRANGERS-024** — When the tile layout is changed, the sequential arranger shall round its grid down to a multiple of the layout size (at least one block) and re-lay from its current address.
  - Tests: untested
- **LIB-ARRANGERS-025** — While the sequential arranger has the Single layout, it shall lay out with the default 1×1 tile layout whatever tile layout is set.
  - Tests: untested
- **LIB-ARRANGERS-026** — If an element set into a sequential arranger differs from it in codec name, color type, data source, palette or element size, then the set shall fail; the element is stored without relocation.
  - Tests: untested

### Cloning and copying

- **LIB-ARRANGERS-027** — When an arranger is cloned, whole or by a pixel rectangle, the result shall be a scattered arranger with the same name, color type, layout and element size, holding the covered elements relocated to the clone's origin.
  - Tests: untested
- **LIB-ARRANGERS-028** — If a clone rectangle falls outside the arranger, or a Single-layout arranger is cloned by anything but its full size, then cloning shall fail.
  - Tests: untested
- **LIB-ARRANGERS-029** — When a sequential arranger is cloned, each cloned element shall get its own new codec instance; a scattered clone's elements shall share codec instances with the original.
  - Tests: untested
- **LIB-ARRANGERS-030** — When elements are copied, the copy shall capture the elements of an element rectangle, the source's color type, layout and element size, and the source arranger as its originating resource.
  - Tests: `GraphicsEditHistoryTests.ElementPaste_UndoRedo`
- **LIB-ARRANGERS-031** — An element copy shall be convertible to a pixel copy of the same pixels, rendered through a temporary scattered arranger.
  - Tests: untested
- **LIB-ARRANGERS-032** — If an element paste would overrun the destination or the copy, or the element pixel size or color type differ, or a non-Tiled destination receives more than one element, then the paste shall fail without change.
  - Tests: untested
- **LIB-ARRANGERS-033** — When an element paste succeeds, it shall place each non-empty copied element at its offset in the destination; empty copied cells leave the destination cell unchanged.
  - Tests: `GraphicsEditHistoryTests.ElementPaste_UndoRedo`

### Save conflicts

- **LIB-ARRANGERS-034** — When save conflicts are analyzed for a scattered arranger, each encodable element shall be Unchanged when its encoding equals the file's bits, else Modified; an element past the end of its source counts as Modified.
  - Tests: `SaveConflictTests.AnalyzeSaveConflicts_DuplicatesWithIdenticalEdits_IsNotConflict`
- **LIB-ARRANGERS-035** — When two elements on the same source overlap in bits, at least one is modified, and they do not write identical data to the identical range, both shall be Conflicting.
  - Tests: `SaveConflictTests.AnalyzeSaveConflicts_UnchangedDuplicateOfModifiedTile_IsConflict`, `SaveConflictTests.AnalyzeSaveConflicts_DuplicatesWithIdenticalEdits_IsNotConflict`
- **LIB-ARRANGERS-036** — The analysis shall report whether any element conflicts and whether any is modified or conflicting, and list each group.
  - Tests: `SaveConflictTests.AnalyzeSaveConflicts_DuplicatesWithIdenticalEdits_IsNotConflict`
- **LIB-ARRANGERS-037** — When analyzed from an edited indexed image, the analysis shall merge a partial image into the arranger's current pixels before encoding.
  - Tests: untested

### Tile layouts and presets

- **LIB-ARRANGERS-038** — The default tile layout shall be a 1×1 layout named "Default".
  - Tests: untested
- **LIB-ARRANGERS-039** — When a tile layout is created from a width and height, its pattern shall visit every cell row by row, or column by column when column-major.
  - Tests: `TileLayoutTests.Create_RowMajor_VisitsRowsFirst`, `TileLayoutTests.Create_ColumnMajor_VisitsColumnsFirst`
- **LIB-ARRANGERS-040** — Two tile layouts shall be equivalent when they have the same name, size and pattern sequence.
  - Tests: untested
- **LIB-ARRANGERS-041** — The library shall ship layout presets as JSON files in `_layouts`, copied to the build output: Default, 1x2, 2x1, 1x4, 4x1, 2x2 H, 2x2 V, 4x2 H, 2x4 H, 4x4 H, 4x4 V, 8x4 H, 4x8 H, 8x8 H; each named as its file, visiting every cell once.
  - Tests: `TileLayoutTests.ShippedLayouts_AllDeserialize`
- **LIB-ARRANGERS-042** — When a layout file is read, the layout service shall return the layout, or fail when the file does not exist or deserializes to nothing; property names match case-insensitively.
  - Tests: `TileLayoutTests.ShippedLayouts_AllDeserialize`
- **LIB-ARRANGERS-043** — At bootstrap, the element store shall hold every layout in the layouts folder by name, log a warning for each file that fails to read, and be empty when the folder is missing.
  - Tests: untested
- **LIB-ARRANGERS-044** (inherited) — The element store's default layout shall be the built-in 1×1 default, not the one read from `Default.json`.
  - Tests: untested

### Builder

- **LIB-ARRANGERS-045** — The arranger builder shall build a scattered arranger from a layout, grid size, element size, color type and name, with the Single layout fixing the grid at 1×1.
  - Tests: `ArrangerBuilderTests.Build_ScatteredArranger`
- **LIB-ARRANGERS-046** — When building a sequential arranger, the builder shall create the named codec at the element size and fail when the codec cannot be created.
  - Tests: `ArrangerBuilderTests.Build_SequentialArranger`

### Movement steps

- **LIB-ARRANGERS-047** (inherited) — For a Single-layout sequential arranger, a row step shall be the codec storage size divided by the arranger pixel height, a column step 16 × storage × height ÷ width bits, and a page half the codec storage size.
  - Tests: untested
- **LIB-ARRANGERS-048** (inherited) — For Single-layout sequential elements after the first, the next element shall start element width × color depth ÷ 4 bits later.
  - Tests: untested

## Invariants

- A cell is either empty or holds an element whose codec color type matches the arranger's.
- A sequential arranger's elements all share one codec instance, one data source and one palette.
- A sequential arranger's address stays within [0, file bits − arranger bits] after a move, or is 0 when the arranger is larger than the file; an absolute move below 0 is not clamped (see Edge cases).
- A sequential arranger's bit size = grid width × grid height × codec storage size.

## Edge cases

- Base `SetElement` accepts a column or row equal to the grid size, which then fails with an index error instead of an out-of-range error; the sequential override has the same off-by-one.
- `TryRotateElement` checks bounds with `>`, so a location equal to the grid size throws instead of failing; `TryMirrorElement` has no bounds check.
- Scattered `SetElement` does not check the element's pixel size against the arranger's.
- An absolute `Move` to a negative address is stored as negative; only `Move(ArrangerMoveType)` clamps at 0.
- A sequential grid that is not a multiple of the tile layout leaves the remainder cells empty, while the arranger's bit size still counts them.
- A sequential arranger captures the file size at construction and does not follow later length changes.
- A clone rectangle that starts inside an element rounds its element count from the width alone, so a misaligned rectangle can drop the last covered column or row.
- Malformed layout JSON throws out of the layout service and bootstrap; two layout files with the same name throw at bootstrap.

## Threading and lifetime

- Arrangers are not thread-safe; they are used on the caller's thread.
- Element copies hold element values, so they keep their data sources and codecs alive and share codec instances with the source arranger.
- Codec instances are mutable and shared: changing an indexed codec's palette changes every element holding that instance (LIB-IMAGES palette assignment).
- The element store is created once at bootstrap and is not reloaded.

## Decisions

- **Mirror and rotation are display attributes.** They change how an element is decoded and encoded, not the stored pixels, so they are free to toggle and undo. Rejected: pixel-level flip and rotate, which remains a selection-tool gap.
- **Every element is written on save.** A save encodes and writes every element, so an unchanged element that overlaps a modified one would overwrite it; the conflict analysis therefore flags overlaps where either side is modified unless both write the same bits to the same range.
- **Element layouts are not persisted.** A data file is browsed with many layouts, so storing one on the data file does not fit, and sequential arrangers are not project resources. The layout choice lasts for an editor session. Rejected: saving the full layout with the arranger, deferred until sequential arrangers become project resources.
- **Layout presets ship in the build output.** `_layouts/*.json` is copied to output so the element store is populated at runtime; before this the store was always empty. A test loads every preset from the test output.
- **Presets cover OAM shapes and column-major order.** Row- and column-major patterns come from one factory, and presets cover the GBA OAM shapes, 1x2, 2x1 and vertical-first variants.
- **Sequential addressing is byte-based only for Single-layout elements.** Tiled sequential elements and absolute moves are bit-addressed; the Single-layout per-element step is a known TODO, so round-trip tests are restricted to tiled codecs.

## Non-goals

- Persisting sequential arrangers or their view state (codec, offset, palette, layout); see LIB-PROJECT-FORMAT.
- Checking that resources belong to the same project or that sources exist; see LIB-PROJECT-SERVICE.
- Pixel-level transforms of a selection.

## Open items

- Rotating a horizontally or vertically mirrored element by a quarter turn composes rotation without regard to the mirror; because render applies rotation then mirror, the visible result turns the opposite way. Needs a check and a test.
- `SequentialArranger` constructor and `ArrangerBuilder` set `ActivePalette` from the palette argument, but the elements' codec (from `CloneCodec`/`CreateCodec`) carries the codec factory's default palette until `ChangePalette` is called. `ChangeCodec` likewise does not apply `ActivePalette` to the new codec.
- Cloning a sequential arranger gives each element a codec from `CloneCodec`, which does not copy the palette, so the clone shows the factory default palette.
- A scattered clone shares codec instances with the original; palette assignment on the clone (LIB-IMAGES) changes the original's elements too.
- `GetInitialSequentialFileAddress` indexes the grid as `[x, y]` instead of `[y, x]`; latent while every layout's first pattern cell is (0, 0).
- Single-layout movement steps (LIB-ARRANGERS-047) and element step (LIB-ARRANGERS-048) look arbitrary and are untested.
- Elements past the end of their source are reported Modified by the conflict analysis but are never written by save.
- `ElementCopierTests` contains only commented-out code; element paste validation has no direct test.
- `ArrangerBuilder` sequential path ignores `WithElementLayout` and `WithPixelColorType`; both come from the codec.
