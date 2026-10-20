---
id: LIB-METATILE-MAPS
title: Metatile maps
status: draft
project: ImageMagitek
sources:
  - ImageMagitek/Metatiles
  - ImageMagitek/_mapformats/FrlgBlock.xml
types:
  - MetatileSet
  - MetatileOrder
  - MetatileMap
  - MetatileLayerArranger
  - BlockGrid
  - BlockBrush
  - BlockPalette
  - MetatileMapPlotter
tests:
  - MetatileSetTests
  - MetatileMapTests
  - BlockPlottingTests
  - MetatileProjectTests
depends:
  - LIB-TILEMAPS
  - LIB-SCENES
  - LIB-ARRANGERS
  - LIB-DATASOURCE
  - LIB-COMPRESSION
  - LIB-PALETTES
---

# Metatile maps

## Purpose

Shows and edits maps that a game stores as a block map plus a table of metatiles, such as FireRed's overworld. A `MetatileSet` is one table of metatiles, each a fixed rectangle of tile entries in one or more layers, with the tile set (LIB-TILEMAPS `TileSet`) and palettes they draw from. A `MetatileMap` reads one block per map cell, resolves each block's metatile number through an ordered list of sets, exposes one derived arranger per metatile layer, and composites the layers with LIB-SCENES. Block plotting edits a working copy of the block numbers, and writing it back changes only the metatile-number bits of the blocks that differ. LIB-PROJECT-FORMAT stores both as resources (`metatileset`, `metatilemap`).

## Requirements

### Metatile sets

- **LIB-METATILE-MAPS-001** — A metatile set shall have a metatile table (a data source, an offset and a positive declared metatile count), a metatile shape in tiles (width and height from 1 to 8, 2×2 by default), a layer count from 1 to 4, the map format (LIB-TILEMAPS) its entries use, an entry order, a tile set (LIB-TILEMAPS `TileSet`), palette slots as on a tilemap (LIB-TILEMAPS-023), and optionally an attribute table (a data source, an offset and a size of 1 to 8 bytes per metatile). If a value is outside its range, creation shall throw naming the set and the value.
  - Tests: `MetatileSetTests.Create_ValueOutOfRange_Throws`
- **LIB-METATILE-MAPS-002** — In `interleaved` order, the entry of metatile *m*, layer *l*, tile position (*x*, *y*) shall be read at `offset + ((m × layers + l) × w × h + y × w + x) × entryBytes`, so a metatile's entries are stored together, layer by layer, row by row.
  - Tests: `MetatileSetTests.Interleaved_EntryAddress`
- **LIB-METATILE-MAPS-003** — In `planar` order, that entry shall be read at `offset + ((l × w × h + y × w + x) × count + m) × entryBytes`, so each tile position of each layer is its own table of `count` entries.
  - Tests: `MetatileSetTests.Planar_EntryAddress`
- **LIB-METATILE-MAPS-004** — An entry that lies past the end of the metatile source shall give an empty tile position.
  - Tests: `MetatileSetTests.EntryPastSource_IsEmpty`
- **LIB-METATILE-MAPS-005** — A metatile's attribute shall be the attribute table's `size` bytes at `offset + m × size`, read as an unsigned little-endian integer, or none when the set has no attribute table or the bytes lie past its source.
  - Tests: `MetatileSetTests.Attribute_ReadsLittleEndian`, `MetatileSetTests.Attribute_PastSource_IsNone`

### Metatile maps

- **LIB-METATILE-MAPS-010** — A metatile map shall have a block map (a data source, an offset, a width and height in blocks, a screen layout from LIB-TILEMAPS, and a block format), an ordered list of one or more metatile sets each with the metatile number its first metatile has (its base), and an empty block number (0 by default). If two sets' metatile ranges `[base, base + count)` overlap, two sets' tile ranges overlap, two sets assign a palette to the same slot, the sets differ in layer count or metatile shape, or the empty block number does not fit the block format's index field, then creation shall throw naming the map and the conflict.
  - Tests: `MetatileMapTests.Create_OverlappingMetatileRanges_Throws`, `MetatileMapTests.Create_OverlappingTileRanges_Throws`, `MetatileMapTests.Create_SharedPaletteSlot_Throws`, `MetatileMapTests.Create_MismatchedShape_Throws`
- **LIB-METATILE-MAPS-011** — A block format shall be a map format whose tile-index field holds the metatile number; its other fields, if any, are ignored, and every bit outside the index field is a **block bit**. The shipped "FRLG Block" format shall be 16-bit little-endian with the index in bits 0–9.
  - Tests: `MapFormatTests.ShippedFormats_AllLoad`, `MetatileMapTests.FrlgBlock_DecodesMetatileNumber`
- **LIB-METATILE-MAPS-012** — A block's metatile shall be the metatile `n − base` of the set whose metatile range holds the block's number `n`; a number no set holds, and a block past the end of the block map data, shall give an empty block (every layer empty), and the latter shall set the map's `MapError` as LIB-TILEMAPS-026 does.
  - Tests: `MetatileMapTests.Block_ResolvesAcrossSets`, `MetatileMapTests.Block_OutsideEverySet_IsEmpty`, `MetatileMapTests.TruncatedBlockMap_SetsMapError`
- **LIB-METATILE-MAPS-013** — A metatile entry's tile index shall resolve through the tile set of whichever of the map's sets holds that index in its tile range, so a metatile of one set can use tiles of another (FireRed's secondary metatiles use primary tiles); an index no tile set holds gives an empty cell.
  - Tests: `MetatileMapTests.Tile_ResolvesAcrossSets`, `MetatileMapTests.Tile_OutsideEveryTileSet_IsEmpty`
- **LIB-METATILE-MAPS-014** — A metatile entry's palette field shall select the palette slot assigned by whichever set assigns it; an unassigned slot uses slot 0, and an unassigned slot 0 the codec's default palette, as LIB-TILEMAPS-023.
  - Tests: `MetatileMapTests.PaletteSlots_CombineAcrossSets`
- **LIB-METATILE-MAPS-015** — For each metatile layer the map shall expose a derived arranger (`MetatileLayerArranger`) of `width × shape width` by `height × shape height` tile cells, whose cell (*bx* × w + *x*, *by* × h + *y*) is the tile at position (*x*, *y*) of that layer of block (*bx*, *by*)'s metatile, with the entry's mirror and palette, or empty. Its elements shall not be settable, as LIB-TILEMAPS-028.
  - Tests: `MetatileMapTests.LayerArrangers_MatchHandBuiltScatteredArrangers`, `MetatileMapTests.LayerArranger_SetElement_Throws`
- **LIB-METATILE-MAPS-016** — The map's composite shall be its layer arrangers' layer images (LIB-SCENES-001) drawn bottom layer first at the origin over a backdrop of slot 0's color 0 made opaque, with hidden layers skipped (LIB-SCENES-010 to -014). Each layer's visibility shall be a property of the map, true by default and not stored in the project.
  - Tests: `MetatileMapTests.Composite_TopLayerColorZeroShowsBottom`, `MetatileMapTests.Composite_HiddenLayerSkipped`, `MetatileMapTests.Composite_BackdropIsSlot0Color0`
- **LIB-METATILE-MAPS-017** — A block's **block bits value** shall be its value with the index field's bits removed and the higher bits shifted down to close the gap (FireRed: collision in bits 0–1 and elevation in bits 2–5 of the value).
  - Tests: `MetatileMapTests.BlockBitsValue_RemovesIndexField`
- **LIB-METATILE-MAPS-018** — The layer arrangers and the composite shall be worked out again after the block map, a metatile table, an attribute table or a tile source raises `DataWritten`, as LIB-TILEMAPS-027.
  - Tests: `MetatileMapTests.RebuildsAfterBlockMapWrite`
- **LIB-METATILE-MAPS-019** — A metatile map's linked resources shall be its block map source and its metatile sets; a metatile set's shall be its metatile, tile and attribute sources and each assigned slot palette.
  - Tests: `MetatileProjectTests.DeletingSet_RemovesMapsThatListIt`

### Editing blocks

- **LIB-METATILE-MAPS-030** — A block grid (`BlockGrid`) shall be a working copy of the map's block numbers, read from the block map, that the plotter edits; a block past the end of the data has no number and cannot be edited.
  - Tests: `BlockPlottingTests.BlockGrid_ReadsBlockNumbers`
- **LIB-METATILE-MAPS-031** — Writing a block grid shall, for each block whose number differs from the block map, encode the new number into the block's index field keeping every block bit (LIB-TILEMAPS-006), write only those blocks in place, then flush and raise `DataWritten` once if any changed. If a number fits no set's range or the index field, writing shall fail naming the block and write nothing.
  - Tests: `BlockPlottingTests.Write_ChangesOnlyTargetIndexBits_KeepsBlockBits`, `BlockPlottingTests.Write_NumberOutsideSets_FailsAndWritesNothing`, `BlockPlottingTests.Write_NoChange_WritesNothing`
- **LIB-METATILE-MAPS-032** — If the block map source is compressed or read-only, then writing shall be refused with the reason of LIB-ARRANGERS-049, and the map's `BlockMapReadOnlyReason` shall give it.
  - Tests: `BlockPlottingTests.CompressedBlockMap_IsReadOnly`
- **LIB-METATILE-MAPS-033** — A block brush shall be a rectangle of metatile numbers, with empty cells transparent. Stamping it at a block shall set each non-empty brush cell's number on the grid, clipped to the map, and report whether any block changed.
  - Tests: `BlockPlottingTests.Stamp_ClipsAtEdge`, `BlockPlottingTests.Stamp_NoChange_ReportsFalse`
- **LIB-METATILE-MAPS-034** — A pencil stroke shall stamp the brush at positions aligned to the stroke's first block in steps of the brush size, as LIB-TILEMAPS-068; filling a rectangle shall repeat the brush across it from its top-left block, clipped to the rectangle.
  - Tests: `BlockPlottingTests.Stroke_AlignsToFirstBlock`, `BlockPlottingTests.Fill_TilesBrush`
- **LIB-METATILE-MAPS-035** — Picking a rectangle of blocks shall give a brush of their numbers, skipping blocks with no number; a rectangle with none gives no brush.
  - Tests: `BlockPlottingTests.Pick_Rectangle`
- **LIB-METATILE-MAPS-036** — Erasing blocks shall set them to the map's empty block number.
  - Tests: `BlockPlottingTests.Erase_WritesEmptyBlock`
- **LIB-METATILE-MAPS-037** — A generated block palette shall lay out each set's metatiles in list order, 8 per row, each set starting on a new row, every metatile drawn composited as on the map; picking a rectangle in it gives a brush of the numbers it holds.
  - Tests: `BlockPlottingTests.BlockPalette_LaysOutSetsOnNewRows`

## Invariants

- The layer arrangers always show what the block map and metatile tables decode to; edits go through a block grid and are written back, never set on the arrangers.
- Writing a block grid changes only the index-field bits of blocks whose number changed.

## Edge cases

- A metatile set whose tile source is compressed renders normally; this spec never writes tiles.
- A block map shorter than the map needs: the readable blocks render and edit, the rest are empty and fixed (LIB-METATILE-MAPS-012, -030).
- A metatile set with an attribute size larger than the source holds reports no attribute for the metatiles past the end.

## Threading and lifetime

- As LIB-TILEMAPS: the derived arrangers rebuild lazily on access after a `DataWritten` from any source they read; a map unsubscribes from its sources when disposed.

## Decisions

- **A metatile map is a resource with layer arrangers, not an arranger.** `MetatileMap` exposes one `MetatileLayerArranger` per layer and composites them. Reason: an arranger has one element per cell, and a metatile map has one per layer per cell; making the map an arranger would show one layer to every arranger path (export, import, editors). Rejected: `MetatileMapArranger : Arranger` exposing the bottom layer.
- **Edits work on block numbers, not elements.** Blocks mode edits a `BlockGrid` of numbers and writes changed numbers back. Reason: a block is a number, and the layer arrangers follow from it; editing element clones (the tilemap approach) would need every layer kept in step. Rejected: cloning the layer arrangers as working copies.
- **The backdrop is slot 0's color 0.** Reason: GBA and SNES draw backdrop color 0 of BG palette 0 where every layer is transparent, so a map's bottom layer shows correctly where its tiles use color 0. Rejected: a transparent backdrop (shows holes the game never shows).
- **Ranges may not overlap.** Two sets of one map may not share metatile numbers, tile indexes or palette slots. Reason: the game loads them into disjoint VRAM and palette ranges, and an overlap would make one number mean two things. Rejected: first-listed set wins.
- **`emptyblock` on the map.** The number an erased block gets is a map attribute, 0 by default, as `emptytile` on tilemaps. Reason: the blank metatile is the game's choice.
- See also LIB-TILEMAPS "TileSet is shared by tilemaps and metatile maps", the tilemaps proposal's "Tile set fields live on the tilemap" (kept for tilemaps), and the metatile-maps proposal's "Two resources" decision.

## Non-goals

- Editing a metatile's tiles, palettes and flips (a later phase, through a tilemap view of the metatile table).
- Interpreting attributes or block bits (collision, elevation, behavior); they are shown as raw values only.
- Moving, copying or pasting regions of blocks; Arrange and Draw on a metatile map.
- Resizing a map, map connections, events and sprites.

## Open items

- The block-bits overlay and the editor are specified in UI-BLOCK-PLOTTING.
- Drawing a border metatile map around its map is a later view option.
