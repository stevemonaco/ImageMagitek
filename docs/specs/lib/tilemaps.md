---
id: LIB-TILEMAPS
title: Read-only tilemaps
status: draft
project: ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek/Tilemaps
  - ImageMagitek/Arranger/TilemapArranger.cs
  - ImageMagitek/_schemas/MapFormatSchema.xsd
  - ImageMagitek/_mapformats
types:
  - MapFormat
  - MapField
  - MapEntry
  - MapScreenLayout
  - MapScreenLayoutExtensions
  - IMapFormatFactory
  - MapFormatFactory
  - MapFormatFileFailure
  - XmlMapFormatReader
  - TilemapArranger
tests:
  - MapFormatTests
  - TilemapArrangerTests
  - TilemapProjectTests
depends:
  - LIB-ARRANGERS
  - LIB-CODECS
  - LIB-DATASOURCE
  - LIB-COMPRESSION
  - LIB-PALETTES
---

# Read-only tilemaps

## Purpose

Shows a screen that a game stores as a tile set plus a tilemap without baking it into a scattered arranger. A `TilemapArranger` is an `Arranger` (LIB-ARRANGERS) whose elements are worked out from the map's entries, so rendering, export, element copies and cloning work on it unchanged. Entries are decoded by declarative XML map formats. The map and the tiles may read a `CompressedDataSource` (LIB-COMPRESSION). LIB-PROJECT-FORMAT stores a tilemap as a `tilemap` resource. This first phase is read-only: entries are never written.

## Requirements

### Map formats

- **LIB-TILEMAPS-001** — A map format shall be an XML `mapformat` file validated against `MapFormatSchema.xsd`, with a `name`, `entrybits` of 8, 16 or 32, an optional `endian` (`little` by default), a required `tileindex` field (`lowbit`, default 0, and `bits`), an optional `palette` field of at most 4 bits, and optional one-bit `hflip`, `vflip` and `priority` fields (`bit`).
  - Tests: `MapFormatTests.ShippedFormats_AllLoad`
- **LIB-TILEMAPS-002** — If a field does not fit in the entry, or two fields share a bit, then loading the format shall fail naming the file and the field.
  - Tests: `MapFormatTests.Load_FieldWiderThanEntry_FailsNamingFile`, `MapFormatTests.Load_OverlappingFields_FailsNamingFile`
- **LIB-TILEMAPS-003** — The shipped formats shall be SNES BG (16-bit little-endian: tile 0-9, palette 10-12, priority 13, hflip 14, vflip 15), SNES Mode 7 (8-bit index), GBA Text BG (16-bit little-endian: tile 0-9, hflip 10, vflip 11, palette 12-15), GBA Affine (8-bit index), Genesis Plane (16-bit big-endian: tile 0-10, hflip 11, vflip 12, palette 13-14, priority 15), Game Boy BG (8-bit index) and Plain 8-bit Index.
  - Tests: `MapFormatTests.ShippedFormats_AllLoad`, `MapFormatTests.Decode_ShippedFormat_ReadsEveryField`
- **LIB-TILEMAPS-004** — When the formats folder is loaded, each `*.xml` file shall be read in ordinal name order and registered under its name, case-insensitively. A file that fails to load, or whose name is already registered, shall be skipped and reported with its file name, keeping the first registration.
  - Tests: `MapFormatTests.LoadFormats_DuplicateName_KeepsFirstAndReportsFile`, `BootstrapServiceTests.CreateMapFormatFactory_BadFile_SkipsAndRecordsIssue`
- **LIB-TILEMAPS-005** — Decoding an entry shall read `entrybits / 8` bytes in the format's byte order and return the tile index, the palette field (0 without one) and the flip and priority bits. The palette slot count shall be 2^palette bits, or 1 without a palette field.
  - Tests: `MapFormatTests.Decode_ShippedFormat_ReadsEveryField`, `MapFormatTests.PaletteSlotCount_FollowsPaletteBits`

### Screen layouts

- **LIB-TILEMAPS-010** — With the `linear` layout, cell (x, y) of a map `width` cells wide shall read entry `y × width + x`.
  - Tests: `TilemapArrangerTests.LinearLayout_ReadsRowsInOrder`
- **LIB-TILEMAPS-011** — With the `snes32` layout, entries shall be stored as 32×32-entry screens, left to right then top to bottom, each screen stored linearly. A map at most 32 cells wide reads as `linear`; if a wider map's width is not a multiple of 32, then creating it shall fail.
  - Tests: `TilemapArrangerTests.Snes32Map_MatchesHandBuiltScatteredArranger`, `TilemapArrangerTests.Snes32WidthNotMultipleOf32_Throws`

### Tilemap arranger

- **LIB-TILEMAPS-020** — A tilemap shall be an indexed, tiled arranger of `width × height` cells with `ArrangerMode.Tilemap`, whose element pixel size is the tile codec's size. If the codec is unknown or not indexed, then creating the tilemap shall fail naming the codec.
  - Tests: `TilemapArrangerTests.DirectCodec_Throws`
- **LIB-TILEMAPS-021** — A cell's element shall read the tile source at bit `tileOffset × 8 + (index − first) × codec.StorageSize`, where `index` is the entry's tile index and `first` the tilemap's first tile index.
  - Tests: `TilemapArrangerTests.Snes32Map_MatchesHandBuiltScatteredArranger`, `TilemapArrangerTests.LinearLayout_ReadsRowsInOrder`
- **LIB-TILEMAPS-022** — If an entry's tile index is below `first`, or its tile does not lie wholly within the tile source, then the cell shall be empty, and `OutOfRangeCount` shall count it.
  - Tests: `TilemapArrangerTests.IndexOutsideTileSet_GivesEmptyCell`
- **LIB-TILEMAPS-023** — A cell shall use the palette in the slot its entry's palette field names; when that slot is empty it shall use slot 0, and when slot 0 is empty, the codec's default palette. Each tilemap shall own one codec instance per assigned slot, shared by no other arranger.
  - Tests: `TilemapArrangerTests.PaletteFieldNamingEmptySlot_UsesSlotZero`
- **LIB-TILEMAPS-024** — The entry's flip bits shall become the element's mirror: horizontal, vertical or both. Rotation is always none.
  - Tests: `TilemapArrangerTests.FlipBits_MapToMirror`
- **LIB-TILEMAPS-025** — The priority bit shall be decoded but shall not affect rendering.
  - Tests: `MapFormatTests.Decode_ShippedFormat_ReadsEveryField`
- **LIB-TILEMAPS-026** — If the map source ends before the entry of some cell, then those cells shall be empty and `MapError` shall say how many entries were read of how many the map needs. When the map source is a `CompressedDataSource` with a `DecompressionError`, or a missing file, `MapError` shall be that error and every cell empty.
  - Tests: `TilemapArrangerTests.ShortMap_HasErrorAndEmptyCells`, `TilemapArrangerTests.CompressedMapWithError_ReportsDecompressionError`
- **LIB-TILEMAPS-027** — When the map source or the tile source raises `DataWritten`, the tilemap shall work out its cells again on the next read of its elements.
  - Tests: `TilemapArrangerTests.CompressedMap_ReadsDecompressedBytes_IsReadOnly_AndRebuildsAfterParentWrite`
- **LIB-TILEMAPS-028** — `Resize`, `SetElement` and `ResetElement` shall throw `InvalidOperationException`.
  - Tests: `TilemapArrangerTests.ResizeSetAndResetElement_Throw`
- **LIB-TILEMAPS-029** — Cloning a tilemap, whole or in part, shall return a `ScatteredArranger` with the same elements, whose codecs are new instances with the same palettes (a bake).
  - Tests: `TilemapArrangerTests.CloneArranger_ReturnsEquivalentScatteredArrangerWithOwnCodecs`
- **LIB-TILEMAPS-030** — A tilemap's linked resources shall be its map source, its tile source and each assigned slot palette.
  - Tests: `TilemapArrangerTests.LinkedResources_AreMapTileAndSlotPalettes`
- **LIB-TILEMAPS-031** — A tilemap shall be read-only (LIB-ARRANGERS-049). An element reason (a codec that cannot encode, a compressed or read-only source) comes first; otherwise the reason shall be "is a tilemap; drawing on and importing into tilemaps is not supported yet".
  - Tests: `TilemapArrangerTests.CompressedMap_ReadsDecompressedBytes_IsReadOnly_AndRebuildsAfterParentWrite`, `TilemapArrangerTests.CompressedTileSource_ReadOnlyReasonNamesCompressedSource`

## Invariants

- The element grid always equals what the current map and tile bytes decode to; nothing else writes it.
- A tilemap never writes to its map or tile source.
- Every palette slot index below `MapFormat.PaletteSlotCount` exists in `PaletteSlots`, possibly empty.

## Edge cases

- An empty map source, or an offset past its end, gives a `MapError` and an empty grid, not an exception.
- Several cells may name the same tile; they share it, as on hardware.
- A palette field value at or above the slot count cannot occur, because slots cover every value of the field.
- A missing tile source file makes every cell empty; the editor alerts first (UI-EDITORS).

## Threading and lifetime

- The grid is worked out lazily under a lock the first time elements are read after construction or after a source raised `DataWritten`, so it does not depend on the order of `DataWritten` handlers.
- The tilemap subscribes to `DataWritten` of its map and tile sources in its constructor and unsubscribes in `Dispose`. LIB-PROJECT-SERVICE disposes it when it is removed or the project closes.

## Decisions

- **Read-only first phase.** The tilemap reads entries and never writes them: Arrange-mode entry writes, Save, an `Encode` per format, the empty entry and shared-tile import conflicts are deferred (see the tilemaps change proposal). Reason: the first use is GBA screens such as FireRed's, whose maps and tile sets are LZ77-compressed and so read-only anyway; the derived arranger, project format and editor integration can land and be checked against existing baked arrangers before entry writes add their own rules. Drawing and import are blocked by the read-only reason (LIB-TILEMAPS-031) until tile write-back for shared tiles is specified. Rejected: shipping draw-through on uncompressed tile sets now (shared-tile import conflicts would be silently resolved by the last element written).
- **A derived arranger, not a new editor.** Elements come from the entries, so every path that takes an `Arranger` works on a tilemap. Rejected: a separate tilemap editor; a scattered arranger with a "map" flag (two sources of truth for one cell).
- **Map formats are data.** Entry layouts differ only in widths and bit positions, so XML covers SNES, GBA, Genesis and Game Boy, as it covers flow and pattern codecs. Rejected: hard-coding the built-in formats in C#.
- **An empty slot falls back to slot 0.** Reason: an 8bpp GBA map ignores the palette field, and many 4bpp screens use one or two palettes, so assigning only the used slots must work. Rejected: failing creation or showing empty cells for unassigned slots.
- **The grid is rebuilt lazily on `DataWritten`.** Reason: `CompressedDataSource` invalidates on its parent's `DataWritten` and the tree raises `ResourceChanged` from the same event, so an eager rebuild could read stale bytes depending on handler order. Rejected: rebuilding inside the handler.
- **Clone is a bake.** Cloning returns a `ScatteredArranger` with its own codec instances, so "Bake to Scattered Arranger" and "Add as New Scattered Arranger" reuse the element-copy flow, and palette changes on the baked copy cannot reach the tilemap.

## Non-goals

- Writing entries, saving maps, encoding entries and the empty entry (deferred, see Open items).
- Layers and priority rendering; multi-layer screens are one tilemap per BG layer (layers proposal).
- Rotation; it has no map representation.

## Open items

- Arrange-mode entry writes (move, paste, delete, apply palette, mirror) and Save of entries and tiles: proposal Task 3.
- `MapFormat.Encode` and a format's empty entry.
- Shared-tile import conflicts and Draw/Import on tilemaps over writable tile sets: the conflict half of proposal Task 5.
- NES nametable and Game Boy Color attribute tables, which store palette and flip data outside the entry.
- A rendered preview in the Add New Tilemap dialog (UI-PROJECT-TREE); it shows a text summary only.
- Tilemap properties cannot be edited after creation except in the XML.
