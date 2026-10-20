---
id: LIB-TILEMAPS
title: Tilemaps
status: draft
project: ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek/Tilemaps
  - ImageMagitek/Arranger/TilemapArranger.cs
  - ImageMagitek/Arranger/ArrangerSaveConflictExtensions.cs
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
  - SharedTileConflict
  - SharedTileExtensions
  - TileBrush
  - TileBrushCell
  - TilePalette
  - TilemapPlotter
tests:
  - MapFormatTests
  - TilemapArrangerTests
  - TilemapEditTests
  - TilemapProjectTests
  - TilePlottingTests
depends:
  - LIB-ARRANGERS
  - LIB-CODECS
  - LIB-DATASOURCE
  - LIB-COMPRESSION
  - LIB-PALETTES
  - LIB-IMAGES
---

# Tilemaps

## Purpose

Shows a screen that a game stores as a tile set plus a tilemap without baking it into a scattered arranger. A `TilemapArranger` is an `Arranger` (LIB-ARRANGERS) whose elements are worked out from the map's entries, so rendering, export, element copies and cloning work on it unchanged. Entries are decoded by declarative XML map formats. The map and the tiles may read a `CompressedDataSource` (LIB-COMPRESSION). LIB-PROJECT-FORMAT stores a tilemap as a `tilemap` resource. Tile plotting builds brushes from a tile palette or from the map and stamps them into the edited copy, for editors that plot whole tiles. When the map source is writable, an edited copy of the tilemap writes back only the entries that differ; when the tile source is writable, drawing and import write the tiles, so every cell that uses a tile changes with it.

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
- **LIB-TILEMAPS-006** — Encoding an entry shall write its tile index, palette, flip and priority fields into the entry's existing bytes in the format's byte order, leaving every bit outside those fields unchanged, so decoding returns the encoded fields.
  - Tests: `MapFormatTests.Encode_ShippedFormat_RoundTripsEveryField`, `MapFormatTests.Encode_KeepsPriorityAndBitsOutsideFields`
- **LIB-TILEMAPS-007** — If an encoded tile index is above the format's largest tile index (2^tile bits − 1) or a palette is not below the palette slot count, then encoding shall throw `ArgumentOutOfRangeException` and leave the bytes unchanged.
  - Tests: `MapFormatTests.Encode_ValueWiderThanField_Throws`

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
  - Tests: `TilemapArrangerTests.CompressedMap_ReadsDecompressedBytes_AndRebuildsAfterParentWrite`, `TilemapEditTests.WriteEntries_MovedCell_ChangesOnlyTargetEntryBytes`
- **LIB-TILEMAPS-028** — `Resize`, `SetElement` and `ResetElement` shall throw `InvalidOperationException`; cells are edited on a clone (LIB-TILEMAPS-029) and written back with LIB-TILEMAPS-045.
  - Tests: `TilemapArrangerTests.ResizeSetAndResetElement_Throw`
- **LIB-TILEMAPS-029** — Cloning a tilemap, whole or in part, shall return a `ScatteredArranger` with the same elements, whose codecs are new instances with the same palettes (a bake).
  - Tests: `TilemapArrangerTests.CloneArranger_ReturnsEquivalentScatteredArrangerWithOwnCodecs`
- **LIB-TILEMAPS-030** — A tilemap's linked resources shall be its map source, its tile source and each assigned slot palette.
  - Tests: `TilemapArrangerTests.LinkedResources_AreMapTileAndSlotPalettes`
- **LIB-TILEMAPS-031** — A tilemap's pixels shall be read-only (LIB-ARRANGERS-049) when an element gives a reason (a codec that cannot encode, a compressed or read-only source) or, failing that, when its tile source is compressed ("reads compressed data '<name>', which is read-only") or read-only ("reads data file '<name>', which is read-only"). Its map entries shall be writable exactly when `MapReadOnlyReason`, worded the same way for the map source, is null; a compressed map with writable tiles can be drawn on, but its entries cannot be written.
  - Tests: `TilemapArrangerTests.UncompressedTilemap_IsWritable`, `TilemapArrangerTests.CompressedMapWritableTiles_DrawableButMapReadOnly`, `TilemapArrangerTests.CompressedTileSource_ReadOnlyReasonNamesCompressedSource`
- **LIB-TILEMAPS-032** — A tilemap shall have an empty tile index, 0 by default, written into the entry of a deleted cell. If it is above the format's largest tile index, then creating the tilemap shall fail naming it.
  - Tests: `TilemapEditTests.WriteEntries_DeletedCell_WritesConfiguredEmptyEntry`, `TilemapEditTests.EmptyTileWiderThanField_Throws`, `TilemapProjectTests.EmptyTile_RoundTrips`

### Writing entries

- **LIB-TILEMAPS-040** — An element shall be a tile of the tile set when it reads the tile source, uses the tile codec, is not rotated, is mirrored only in directions the format has a flip field for, and reads a whole tile of the tile source at or after the tile offset, on a tile boundary; its tile index is then `(address − tileOffset × 8) ÷ codec.StorageSize + first`.
  - Tests: `TilemapEditTests.FindTileIndex_TileOfTileSet_ReturnsIndexFromFirstTile`
- **LIB-TILEMAPS-041** — If an element is not a tile of the tile set, or its tile index is above the format's largest tile index, then the tilemap shall refuse it with a reason that completes "element at (x, y) …": "reads data file '<name>', not the tile set's data file '<name>'", "uses codec '<name>', not the tile set's codec '<name>'", "is rotated; tilemap entries cannot be rotated", "is mirrored horizontally; map format '<name>' has no horizontal flip field", "is mirrored vertically; map format '<name>' has no vertical flip field", "reads 0x<offset>, which is not on a tile boundary of the tile set", "reads 0x<offset>, which is outside the tile set" or "is tile <n>, which does not fit the <bits>-bit tile field of map format '<name>'".
  - Tests: `TilemapEditTests.FindTileIndex_OtherSource_Refuses`, `TilemapEditTests.FindTileIndex_OtherCodec_Refuses`, `TilemapEditTests.FindTileIndex_Rotated_Refuses`, `TilemapEditTests.FindTileIndex_MirroredWithoutFlipField_Refuses`, `TilemapEditTests.FindTileIndex_UnalignedAddress_Refuses`, `TilemapEditTests.FindTileIndex_OutsideTileSet_Refuses`, `TilemapEditTests.FindTileIndex_IndexWiderThanField_Refuses`
- **LIB-TILEMAPS-042** — The palette slot for a palette shall be, in order: a preferred slot (the entry's current slot) when that slot shows the palette, counting an empty slot as showing slot 0's palette; the lowest slot assigned the palette; slot 0 when it shows the palette (its codec's default palette when unassigned); otherwise none.
  - Tests: `TilemapEditTests.FindPaletteSlot_PrefersGivenSlotThenLowestAssignedThenEffectiveSlotZero`
- **LIB-TILEMAPS-043** — When an element is made a cell element for pasting, the tilemap shall refuse it as LIB-TILEMAPS-041 does, keep it when a slot holds its palette, and otherwise give it a new codec instance with the palette of the slot the destination cell uses, or slot 0's when the destination is empty.
  - Tests: `TilemapEditTests.ToCellElement_PaletteInNoSlot_UsesDestinationSlot`, `TilemapEditTests.ToCellElement_ForeignElement_RefusesWithReason`
- **LIB-TILEMAPS-044** — When entries are written from an edited arranger, a cell holding an element shall get the element's tile index, the palette slot of LIB-TILEMAPS-042 preferring the slot its codec was cloned for by the tilemap (LIB-TILEMAPS-073), otherwise its current slot, flip bits from its mirror and its current priority. An empty cell shall get the empty entry (the empty tile index, palette 0, no flips, no priority) unless the tilemap's own cell is empty too (out of range or unread), when its entry is kept. Bits outside the format's fields are kept (LIB-TILEMAPS-006).
  - Tests: `TilemapEditTests.WriteEntries_MovedCell_ChangesOnlyTargetEntryBytes`, `TilemapEditTests.WriteEntries_PastedTile_ChangesOnlyTargetEntry`, `TilemapEditTests.WriteEntries_AppliedPalette_WritesMatchingSlot_KeepsPriority`, `TilemapEditTests.WriteEntries_Mirror_WritesFlipBitsOnly`, `TilemapEditTests.WriteEntries_DeletedCell_WritesConfiguredEmptyEntry`, `TilemapEditTests.WriteEntries_UnchangedOutOfRangeCell_IsNotRewritten`
- **LIB-TILEMAPS-045** — Writing entries shall write only the entries that differ from the map, each at `mapOffset + entry index × entry bytes`, then flush the map source and raise `DataWritten` once; with no differing entry it shall write nothing and raise nothing.
  - Tests: `TilemapEditTests.WriteEntries_MovedCell_ChangesOnlyTargetEntryBytes`, `TilemapEditTests.WriteEntries_NoChanges_DoesNotRaiseDataWritten`, `TilemapEditTests.WriteEntries_SeveralChanges_RaisesDataWrittenOnce`
- **LIB-TILEMAPS-046** — If the edited arranger's size differs from the tilemap's, the map is read-only, a cell is refused (LIB-TILEMAPS-041), a cell's palette is in no slot, or a cell has an element but no entry in the map data, then writing entries shall fail naming the tilemap and the cell, and write nothing. The same check shall be available without writing.
  - Tests: `TilemapEditTests.WriteEntries_PaletteInNoSlot_FailsAndWritesNothing`, `TilemapEditTests.CanWriteEntries_RefusedCell_FailsAndWritesNothing`, `TilemapArrangerTests.CompressedMapWritableTiles_DrawableButMapReadOnly`

### Shared tiles

- **LIB-TILEMAPS-050** — When shared tiles are resolved between a current and an edited image of a whole arranger, cells whose elements read the same source and address shall be compared with mirror and rotation undone. With changed cells winning, only the cells whose edited pixels differ from the current ones must agree, and their tile shall be copied into every cell that reads it, mirrored per cell; otherwise every cell of the tile must agree.
  - Tests: `TilemapEditTests.DrawnCell_SharedTile_ChangesEveryCellAfterSave`, `TilemapEditTests.ImportIntoTilemap_ConsistentTiles_WritesThem`
- **LIB-TILEMAPS-051** — If the cells of a tile disagree, then every cell of that tile shall be reset to its current pixels and the tile reported as a `SharedTileConflict` naming its address and every cell that reads it, in row-major order.
  - Tests: `TilemapEditTests.DrawnCellsDisagree_IsConflict_TileUnchanged`, `TilemapEditTests.ImportIntoTilemap_SharedTileDiffers_ReportsConflictAndKeepsTile`

### Tile plotting

- **LIB-TILEMAPS-060** — A tilemap's tile count shall be the number of tiles that lie wholly in the tile source from the tile offset, capped at `MaxTileIndex − first + 1` and at the declared tile count when the tilemap has one, and 0 when the tile offset is past the source or the source file is missing.
  - Tests: `TilePlottingTests.TileCount_CappedByTileSetEnd`, `TilePlottingTests.TileCount_CappedByTileField`, `TilePlottingTests.TileCount_PastSource_IsZero`, `TilePlottingTests.TileCount_CappedByDeclaredCount_AndHigherTilesRenderEmpty`
- **LIB-TILEMAPS-061** — A tile element made for a tile index and cell shall read the address of LIB-TILEMAPS-021 with the given codec and mirror and no rotation. A slot codec clone shall be a new codec instance showing that slot's effective palette (LIB-TILEMAPS-023).
  - Tests: `TilePlottingTests.Generate_LaysOutTilesFromFirstTile_InSlotPalette`, `TilePlottingTests.Stamp_ThenWriteEntries_ChangesOnlyTargetEntry`
- **LIB-TILEMAPS-062** — A generated tile palette shall lay tile `first + i` at cell (i mod columns, i div columns), 16 columns by default and at least one row, every tile shown with one clone of the chosen slot's codec.
  - Tests: `TilePlottingTests.Generate_LaysOutTilesFromFirstTile_InSlotPalette`
- **LIB-TILEMAPS-063** — A curated tile palette shall keep its arranger's layout. A cell shall be a tile when the tilemap accepts its element with rotation and mirror ignored (LIB-TILEMAPS-040), shown unmirrored in the chosen slot's palette; any other element shall be refused with the LIB-TILEMAPS-041 reason and kept, when indexed, in its own colors. If the arranger's element pixel size differs from the tile size, then every element shall be refused with "is in an arranger of WxH elements, not the tile set's WxH tiles".
  - Tests: `TilePlottingTests.FromArranger_KeepsValidCells_RefusesForeignOtherCodecAndUnalignedCells`, `TilePlottingTests.FromArranger_OtherElementSize_RefusesEveryCell`
- **LIB-TILEMAPS-064** — A brush made from a tile palette rectangle shall be the rectangle clipped to the palette, with refused and empty cells transparent; a rectangle without a tile gives no brush.
  - Tests: `TilePlottingTests.CreateBrush_FromPaletteRectangle_SkipsRefusedCells`
- **LIB-TILEMAPS-065** — A brush shall be a rectangle of tile index and mirror cells (empty cells transparent) with one palette slot and horizontal and vertical flips. A flip shall mirror the layout in that direction and toggle the same direction of each cell's mirror.
  - Tests: `TilePlottingTests.FlippedBrush_MirrorsLayoutAndEachCell`
- **LIB-TILEMAPS-066** — If a brush's slot is not a slot of the format, it flips in a direction the format has no flip field for, or one of its tiles is outside the tile set (LIB-TILEMAPS-060), then plotting it shall be refused with the reason and change nothing.
  - Tests: `TilePlottingTests.FlipWithoutFormatField_IsRefused`, `TilePlottingTests.TileOutsideTileSet_IsRefused`
- **LIB-TILEMAPS-067** — Stamping a brush shall set each non-empty brush cell's element on the edited copy, clipped to the map, in the brush slot's palette, and report whether any cell changed; a cell that already shows the same tile, mirror and palette with the brush slot (LIB-TILEMAPS-073) is unchanged.
  - Tests: `TilePlottingTests.Stamp_ThenWriteEntries_ChangesOnlyTargetEntry`, `TilePlottingTests.Stamp_AtMapEdge_IsClipped`, `TilePlottingTests.Stamp_SameTileAgain_ReportsNoChange`
- **LIB-TILEMAPS-068** — A stroke stamp shall snap the cell to the stroke origin plus a whole number of brush widths and heights, so a stroke's stamps tile instead of overlapping.
  - Tests: `TilePlottingTests.StampStroke_AlignsBrushToStrokeOrigin`
- **LIB-TILEMAPS-069** — Filling a rectangle shall set cell (x, y) to the brush cell ((x − left) mod width, (y − top) mod height), clipped to the map.
  - Tests: `TilePlottingTests.Fill_TilesBrushOverRectangle`
- **LIB-TILEMAPS-070** — Picking a rectangle shall give a brush of its cells' tile indexes (LIB-TILEMAPS-040, mirror ignored) and mirrors, with cells that are empty or not tiles of the tile set transparent, and the slot of LIB-TILEMAPS-073 (slot 0 preferred when unknown) of the first cell holding a tile. A one-cell pick shall turn the cell's mirror into the brush flips; a larger pick keeps each cell's mirror with the flips off. A rectangle without a tile gives no brush.
  - Tests: `TilePlottingTests.Pick_SingleCell_GivesTileSlotAndFlips`, `TilePlottingTests.Pick_SeveralCells_KeepsCellMirrorsAndFirstCellSlot`
- **LIB-TILEMAPS-071** — Erasing cells shall empty them on the edited copy, so writing entries gives them the empty entry (LIB-TILEMAPS-044), and report whether any cell changed.
  - Tests: `TilePlottingTests.Erase_ThenWriteEntries_WritesEmptyEntry`
- **LIB-TILEMAPS-072** — A tilemap may name a scattered arranger as its tile palette. It shall not be a linked resource (LIB-TILEMAPS-030), so removing it never removes the tilemap (LIB-PROJECT-SERVICE-066).
  - Tests: `TilemapArrangerTests.LinkedResources_AreMapTileAndSlotPalettes`, `TilemapProjectTests.DeletingTilePaletteArranger_KeepsTilemap_AndClearsTilePalette`
- **LIB-TILEMAPS-073** — Each codec instance the tilemap clones (a clone's cells, a slot codec clone, a pasted cell's codec) shall remember the slot it was cloned for, and a cell's slot shall be LIB-TILEMAPS-042 with that slot preferred, or a given fallback slot for a codec the tilemap did not clone, so two slots sharing a palette stay distinct.
  - Tests: `TilePlottingTests.Stamp_SlotSharingPalette_WritesBrushSlot`
- **LIB-TILEMAPS-074** — A tilemap may declare a tile count, which shall be positive (creation throws otherwise); entries whose tile index is at or past `first + count` are out of range (LIB-TILEMAPS-060) and render as empty cells.
  - Tests: `TilePlottingTests.TileCount_CappedByDeclaredCount_AndHigherTilesRenderEmpty`, `TilePlottingTests.DeclaredTileCount_NotPositive_Throws`
- **LIB-TILEMAPS-075** — A tilemap's most-used palette slot shall be the assigned slot (slot 0 or a slot with a palette) that the most read cells use, a cell naming an empty slot counting as slot 0, and the lowest such slot on a tie.
  - Tests: `TilePlottingTests.MostUsedPaletteSlot_PicksMajoritySlot`, `TilePlottingTests.MostUsedPaletteSlot_CountsEmptySlotsAsSlotZero`

## Invariants

- The element grid always equals what the current map and tile bytes decode to; nothing else writes it.
- A tilemap writes its map source only by writing entries (LIB-TILEMAPS-045), and only the entries that differ. Tile pixels are written through an image of the tilemap or of its clone (LIB-IMAGES), never by the tilemap itself.
- Every palette slot index below `MapFormat.PaletteSlotCount` exists in `PaletteSlots`, possibly empty.

## Edge cases

- An empty map source, or an offset past its end, gives a `MapError` and an empty grid, not an exception.
- Several cells may name the same tile; they share it, as on hardware. Drawing on one cell changes every cell that uses the tile once the tiles are written.
- A cell moved or pasted onto another keeps the target entry's priority and unknown bits, not the source cell's.
- A palette field value at or above the slot count cannot occur, because slots cover every value of the field.
- A missing tile source file makes every cell empty; the editor alerts first (UI-EDITORS).

## Threading and lifetime

- The grid is worked out lazily under a lock the first time elements are read after construction or after a source raised `DataWritten`, so it does not depend on the order of `DataWritten` handlers.
- The tilemap subscribes to `DataWritten` of its map and tile sources in its constructor and unsubscribes in `Dispose`. LIB-PROJECT-SERVICE disposes it when it is removed or the project closes.

## Decisions

- **Edit a bake, write entries on Save.** Arrange-mode edits run on the tilemap's clone, a scattered arranger (LIB-TILEMAPS-029), with the scattered editing, history and undo unchanged; Save writes the entries that differ from the map (LIB-TILEMAPS-045) and the editor re-clones. The tilemap keeps its invariant that its grid is what the map decodes to. Reason: one editing pipeline for both kinds of arranger. Rejected: a mutable `TilemapArranger` with pending entries (a second source of truth for a cell until Save); a tilemap-specific edit history.
- **Target keeps priority and unknown bits.** A written entry takes its priority and every bit outside the format's fields from the entry it replaces, so moving a cell does not carry the source cell's priority. Reason: `ArrangerElement` has no priority field, and keeping bits the editor cannot show avoids clearing data it does not understand. Rejected: a priority field on `ArrangerElement` (touches every arranger for one format bit).
- **Draw: changed cells win; import: all cells must agree.** When drawing, the cells the user drew on decide a shared tile and unchanged copies follow; when importing, a PNG gives every cell's pixels, so cells of one tile that differ are a conflict and the tile is left unchanged. Reason: an untouched copy of a tile is not a vote against a drawing, but two PNG cells that differ are, and picking one silently loses work. Rejected: last element written wins (the scattered save conflict rule); copy-on-write into new tiles (needs free tiles and map rewrites; a later re-tile feature).
- **Optional declared tile count.** A tilemap may set `tilecount`; without it the tile set runs to the end of the tile source, capped by the tile field. Reason: a tile set rarely fills its data to the end (FF4's title tiles at 0x44000 are followed by the map and palettes), so the generated tile palette and the out-of-range check otherwise include non-tile data. Rejected: a tile set end offset (a count is what the user reads off the tile palette); a required count (existing tilemaps and quick setup work without one).
- **The brush starts on the most-used slot.** Reason: the brush should plot in the colors the screen already uses; FF4's title screen uses slots 1 and 2, so starting on slot 0 showed the tile palette in the wrong colors. Rejected: always slot 0; remembering the last slot per tilemap (state that is not in the project).
- **`emptytile` on the tilemap, not the format.** The tile index a deleted cell gets is a tilemap attribute, 0 by default. Reason: the blank tile belongs to the game's tile set, so two screens with one format can need different ones. Rejected: an empty entry declared in the map format.
- **Unslotted pasted palette takes the destination slot.** A pasted tile whose palette no slot holds gets the destination cell's slot palette (slot 0's for an empty cell). Reason: the proposal refuses pastes only by address and codec, and an element copied from a baked arranger with another palette still names a valid tile. Rejected: refusing the paste.
- **Compressed map, writable tiles.** A compressed map blocks only entry writes, not drawing, since games often pack the map and leave the tiles uncompressed. Rejected: one read-only flag for the whole tilemap.
- **A derived arranger, not a new editor.** Elements come from the entries, so every path that takes an `Arranger` works on a tilemap. Rejected: a separate tilemap editor; a scattered arranger with a "map" flag (two sources of truth for one cell).
- **Map formats are data.** Entry layouts differ only in widths and bit positions, so XML covers SNES, GBA, Genesis and Game Boy, as it covers flow and pattern codecs. Rejected: hard-coding the built-in formats in C#.
- **An empty slot falls back to slot 0.** Reason: an 8bpp GBA map ignores the palette field, and many 4bpp screens use one or two palettes, so assigning only the used slots must work. Rejected: failing creation or showing empty cells for unassigned slots.
- **The grid is rebuilt lazily on `DataWritten`.** Reason: `CompressedDataSource` invalidates on its parent's `DataWritten` and the tree raises `ResourceChanged` from the same event, so an eager rebuild could read stale bytes depending on handler order. Rejected: rebuilding inside the handler.
- **Plotting edits the bake.** `TilemapPlotter` sets and resets elements on the same edited clone Arrange uses, and Save writes them through `WriteEntries` (LIB-TILEMAPS-045). Reason: one write path, one history model and one Save for Arrange and Tiles. Rejected: a new write path writing entries per stamp.
- **A cell's slot rides on its codec instance.** The tilemap remembers, weakly, the slot each codec it clones was made for, so a stamp, pick or write can tell apart slots that share a palette. Reason: `ArrangerElement` has no slot field and history snapshots keep codec references, so the slot survives undo with no new state; a palette alone cannot name the slot the user chose. Rejected: a slot field on `ArrangerElement` (touches every arranger); deduplicating the slot combo by palette (cannot write the slot bits a game needs).
- **One palette slot per brush.** A brush carries one slot, and a multi-cell pick takes its first tile's slot. Reason: the slot is shown and chosen in one combo; per-cell slots would need a mixed combo state. Rejected: per-cell slots.
- **Generated tile palette, optional curated arranger.** Every tilemap has a tile palette generated from its tile set; naming a scattered arranger replaces it with a hand-made layout (meta-tiles, related tiles together). Reason: plotting works without setup, and a curated layout reuses the arranger editor instead of a new layout editor. Rejected: requiring an arranger.
- **The tile palette is not a linked resource.** Removing the curated arranger falls back to the generated tile palette. Reason: it is a picking aid, and losing it must not cascade into deleting the tilemap. Rejected: cascading deletion.
- **Strokes stamp on a brush-sized grid.** A stroke's stamps snap to multiples of the brush size from the stroke origin. Reason: dragging a multi-tile brush must lay it down tiled, not smeared by overlapping stamps at every cell. Rejected: stamping at every cell the pointer crosses.
- **Curated cells ignore mirror and rotation.** A curated cell is picked as its plain tile. Reason: the brush has its own flips, and a rotated element in a layout arranger should not stop its tile from being picked. Rejected: carrying the arranger element's mirror into the brush.
- **Clone is a bake.** Cloning returns a `ScatteredArranger` with its own codec instances, so "Bake to Scattered Arranger" and "Add as New Scattered Arranger" reuse the element-copy flow, and palette changes on the baked copy cannot reach the tilemap.

## Non-goals

- Growing or relocating the map; entries are rewritten in place.
- Layers and priority rendering; multi-layer screens are one tilemap per BG layer (layers proposal).
- Rotation; it has no map representation.

## Open items

- NES nametable and Game Boy Color attribute tables, which store palette and flip data outside the entry.
- A rendered preview in the Add New Tilemap dialog (UI-PROJECT-TREE); it shows a text summary only.
- Tilemap properties cannot be edited after creation except in the XML.
- In the editor, cells that share a drawn tile show their old pixels until Save re-clones the tilemap.
- The tile palette arranger cannot be set or changed on an existing tilemap except in the XML.
