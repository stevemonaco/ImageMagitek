# Metatile maps

**Status:** Tasks 1–3 done on `dev-experiment`. The compositor (`LayerCompositor`, `CompositorLayer` and `LayerImage` in `ImageMagitek/Scenes`, draft LIB-SCENES-001..016) is implemented, and tile index resolution is extracted into `TileSet` (LIB-TILEMAPS-080..083) with no tilemap behavior change. Next is Task 4.

## Why

[Tilemaps](tilemaps.md) cover screens the hardware draws straight from a map: title screens, menus, battle backgrounds. Most game maps are not stored that way. Towns, routes, dungeons and world maps are **metatile maps**: the map holds one block number per 16×16 (or larger) square, and a separate **metatile table** says which tiles, palettes and flips make up each block, often in two layers. FireRed's 364 map layouts, the FF4 and FF6 world and town maps, and most SNES and GBA RPG maps work like this.

A tilemap cannot show these maps, because the map's entries are not tile entries. Plotting single tiles is also the wrong gesture for them: a level designer places whole blocks, and changing a block's look is a separate job that changes every place the block is used. The tilemap work showed that a map editor needs to place whole units from a palette of units, and that the title screen, made almost entirely of unique tiles, is a poor test for it.

FireRed makes a good first target. Its block maps and metatile tables are stored uncompressed, so they can be edited and saved, and only the tile graphics are LZ77-compressed and stay read-only. That split is what Advance Map works with. The facts below were checked against the ROM (BPRE revision 0) by script; Pallet Town and Viridian City render correctly from them:

- A map layout holds width and height in blocks, a block map pointer, a border block map pointer (2×2 blocks), and a primary and a secondary tileset pointer. The scan found 364 layouts, 2 primary tilesets and 60 secondary tilesets.
- A block is a 16-bit value: metatile number in bits 0–9, collision in bits 10–11, elevation in bits 12–15. Pallet Town's layout is at `0x2DD4C0` (24×20 blocks, block map at `0x2DD100`).
- A metatile is 8 GBA Text BG entries, 16 bytes: the bottom layer's 2×2 entries, then the top layer's. Color 0 of a top-layer tile is transparent.
- A tileset holds LZ77-compressed tiles, 16 palettes, a metatile table and one 32-bit attribute per metatile (behavior in the low bits, layer type in bits 29–30). Metatiles and tiles 0–639 come from the primary tileset, and 640 and up from the secondary one. Palettes 0–6 come from the primary tileset and 7–12 from the secondary one.
- The primary tileset has exactly 640 tiles and 640 metatiles. A secondary tileset's metatile count is not stored anywhere (Pallet Town's has 89), so it must be declared.

## What

Two new project resources, a new editor mode, and one shared building block.

**Metatile set** (`metatileset`): one table of metatiles plus the tiles and palettes they draw from, like one FireRed tileset.

- Metatiles: a data source, an offset, a declared count, the metatile shape in tiles (width and height, 2×2 by default), the number of layers (1 to 4), the map format of each entry (the existing [map formats](../specs/lib/tilemaps.md)), and the entry order: `interleaved` (all entries of one metatile together, layer by layer, row by row, as FireRed stores them) or `planar` (one table per position: every metatile's top-left entry, then every top-right entry, and so on, as many SNES games store them).
- Tiles: a data source, an offset, a codec, an optional count, and the tile index the first tile has (FireRed's secondary tiles start at 640).
- Palette slots, as on a tilemap.
- Optional attributes: a data source, an offset and a byte size per metatile. They are kept and shown as raw values in the first version, and are not interpreted.

**Metatile map** (`metatilemap`): a block map read from data, drawn through one or more metatile sets.

- Block map: a data source, an offset, a width and height in blocks, a screen layout (`linear` by default), and a **block format** that names the bits holding the metatile number. Block formats are map-format XML with only a tile-index field. The encoder already keeps every other bit, so FireRed's collision and elevation survive edits. A new built-in "FRLG Block" format covers FireRed and Emerald.
- Metatile sets: an ordered list, each with the metatile number its first metatile has (FireRed: primary at 0, secondary at 640). A block's number selects the set whose range holds it. A tile index inside a metatile selects the set whose tile range holds it, so a secondary metatile can use primary tiles, as FireRed's do. Palette slots combine across the sets.

A metatile map is a project resource that exposes one derived arranger per metatile layer; it is not an arranger itself, since it has one element per layer per cell. It opens in its own editor:

- **View** shows the map with every layer composited back to front, color 0 transparent above the bottom layer, and a layer list to show or hide each metatile layer. Export writes the composite to PNG in the UI and the CLI.
- **Blocks** mode (only for a writable block map) plots whole blocks from a **block palette**, which is generated from the map's metatile sets and drawn composited, with the same tools as Tiles mode: Block Pencil, Rectangle Fill, Block Picker and Erase. A brush is one block or a rectangle of blocks. Save writes only the changed block numbers in place and keeps every other bit of each block.
- A **block-bits overlay** (View and Blocks mode) shows each block's bits outside the metatile number as a number on the block, read-only.
- **Arrange** and **Draw** are not offered on a metatile map in the first version. Pixel work happens on the tiles, through Edit Tile Set on the metatile set, under the tiles' own read-only rules, which keep FireRed's compressed tiles read-only.

**Editing a metatile** (a later phase) works through the tilemap machinery: a metatile set's table can be opened as a tilemap whose screen layout is the metatile order, so the existing Tiles mode edits a block's tiles, palettes and flips, and every place the block is used changes, as on the hardware.

**Shared building block: the layer compositor.** The [layers proposal](layers.md)'s compositing core (layer images drawn back to front over a backdrop, color 0 transparent) is built first and used by metatile maps internally, without the scene resource. Metatile maps do not depend on scenes.

What does not change: tilemaps, scattered arrangers, codecs and palettes. Tilemaps keep their tile set fields; inside the library, tile index resolution moves into one `TileSet` type, which serves a tilemap (one tile range) and a metatile map (several). The project format stays at 1.1, because tilemaps and compression have not shipped yet; a project without metatile maps writes the same version it does today.

## Decisions

- **Two resources: metatile set and metatile map.** Reason: FireRed's 2 primary tilesets are shared by all 364 layouts, so tileset fields on each map would repeat a primary tileset 300 times and let copies disagree. This reverses, for metatiles, the tilemaps decision that put the tile set fields on the tilemap; tilemaps keep it, since a screen rarely shares its tile set. Rejected: fields on the map; a single composite set resource per map (FireRed's primary and secondary sets combine differently on each map).
- **A map lists its sets with base numbers.** Metatile numbers and tile indexes resolve through the set whose range holds them. Reason: this is the FireRed and Emerald layout (primary at 0, secondary at 640, for both metatiles and tiles), and a game with one set just lists one. Rejected: hard-coding the primary/secondary split.
- **Block formats reuse the map format schema.** A block format is a map format with a tile-index field and nothing else. Reason: decoding, encoding and keeping unknown bits already exist and are tested, and FireRed's block is 10 index bits in a 16-bit value. Named attribute fields (collision, elevation) come with the attribute phase. Rejected: a new block format schema.
- **Planar and interleaved metatile orders.** Reason: FireRed stores a metatile's entries together, while many SNES games store one table per tile position. Both are a fixed index formula. Rejected: interleaved only (rules out common SNES layouts); arbitrary per-position offsets (no game seen needs them).
- **Declared counts.** A metatile set declares its metatile count, and its tile count is optional, as `tilecount` is on tilemaps. Reason: FireRed's secondary tilesets do not store their metatile count, and reading past it shows other data as metatiles. Rejected: inferring the count from the gap to the next pointer (fragile, and only works when the attributes follow the table).
- **A Blocks mode, not Tiles mode.** A metatile map gets its own mode, with the Tiles mode tools working on blocks. Reason: a brush of blocks is a different unit from a brush of tiles, and the block palette draws metatiles composited from several layers. The tool classes are shared. Rejected: reusing Tiles mode with a "unit" switch (two meanings for one mode on the same editor).
- **Layers inside a metatile map are composited internally.** A metatile map draws its own layers through the shared compositor, with a layer list for visibility. Reason: a FireRed map is unreadable with one layer, and it must not need a scene resource to look right. Rejected: requiring a scene per map; flattening the layers into one arranger (top-layer transparency and per-layer visibility are lost).
- **No Arrange or Draw on metatile maps at first.** Reason: Blocks mode covers the map edits users make, Draw on a composited multi-layer view needs the active-layer rules from the layers proposal, and FireRed's tiles are compressed anyway. Edit Tile Set covers pixel work. Rejected: building all four modes at once.
- **Names: metatile map and metatile set.** Reason: the user chose them; decompilations use "metatile", and "block" stays the word for one cell of a metatile map. Rejected: "block map" and "block set".
- **A read-only block-bits overlay first.** View and Blocks mode can overlay each block's bits outside the metatile number as a value per block (FireRed: collision and elevation as one number), with no editing. Reason: FireRed users judge a map editor by collision, and showing raw bits needs no per-game knowledge. Named fields and editing come with the attribute phase. Rejected: no overlay until attributes are interpreted.
- **A border is its own metatile map.** FireRed's 2×2 border is added as a small metatile map over the border block map, with the same sets. Reason: it then gets view, export and block editing with no new concept. Drawing the border around a map is a later view option. Rejected: border fields on the metatile map.
- **Attributes are kept, not interpreted.** Reason: attribute layouts are per game (FireRed's behavior, terrain and layer-type bits, Emerald's 16-bit attributes), and getting the map editing right does not need them. Interpreting them belongs with a per-game definition, likely through [table-driven resources](table-resources.md). Rejected: hard-coding FireRed's attribute bits.
- **Creating 364 maps is generation's job.** One map is added by hand with a dialog; a game's whole map list comes from [table-driven resources](table-resources.md) once it can emit metatile maps and sets, or from a script while testing. Reason: the map header table, layout table and tileset structs are a game-specific record table, which is exactly what table definitions describe. Rejected: a FireRed-specific importer in the core.

## Spec changes

- **New spec [`docs/specs/lib/metatile-maps.md`](../specs/lib/metatile-maps.md) (LIB-METATILE-MAPS, draft written):** `MetatileSet`, `MetatileMap` (a resource, not an arranger) and its per-layer `MetatileLayerArranger`s, `BlockGrid`, metatile orders, set ranges and base numbers, tile and metatile resolution across sets, block decode and encode (unknown bits kept), out-of-range blocks and tiles (empty), declared counts, writing changed blocks, read-only rules (a compressed block map is read-only; compressed tiles only block pixel work), linked resources.
- **New spec [`docs/specs/ui/block-plotting.md`](../specs/ui/block-plotting.md) (UI-BLOCK-PLOTTING, draft written):** the metatile map editor (its own editor, not a graphics editor mode), View and Blocks modes, the block palette, tools, brush, undo, save, export, the layer visibility list and the block-bits overlay.
- **LIB-TILEMAPS:** tile index resolution moves into `TileSet` (no behavior change); the "FRLG Block" built-in format; the metatile screen layout (later phase).
- **LIB-SCENES (from the layers proposal; [draft](../specs/lib/scenes.md) written for the compositor):** the compositor, built first and shared.
- **LIB-PROJECT-FORMAT:** the `metatileset` and `metatilemap` resources (XSD, models, reader, writer, mapper); load order (data sources, compressed data, palettes, scattered arrangers, metatile sets, tilemaps, metatile maps); still version 1.1.
- **LIB-PROJECT-SERVICE / LIB-PROJECT-TREE:** add, rename, move and delete both resources; deleting a data source or palette cascades; deleting a metatile set removes the maps that list it, listed in the deletion plan.
- **LIB-IMAGE-IO / CLI-COMMANDS:** composite export of metatile maps; import skips them and reports it.
- **UI-PROJECT-TREE / UI-EDITORS / UI-GRAPHICS-EDITOR:** "New Metatile Set..." and "New Metatile Map..." with a rendered preview; Edit Tile Set on a metatile set; the Blocks mode button.
- **ARCHITECTURE §3:** add Metatile set and Metatile map to the domain list.

## Tasks

1. **Draft specs.** Done: LIB-METATILE-MAPS, UI-BLOCK-PLOTTING and the compositor part of LIB-SCENES, with `status: draft`.
2. **Compositor.** Done: LIB-SCENES, composited into a 32-bit `LayerImage`. The layers proposal's Task 1 core (`LayerCompositor` over rendered layer images): back-to-front order, color 0 transparency, hidden layers, backdrop. Tests as listed there.
3. **`TileSet` extraction.** Done: LIB-TILEMAPS-080..083. Move tile index resolution, tile count and tile element creation out of `TilemapArranger` into `TileSet` (one range per tilemap). Tests: the whole existing tilemap suite passes unchanged.
4. **Metatile sets and maps, read-only.** `MetatileSet`, block format "FRLG Block", `MetatileMap` with one `MetatileLayerArranger` per metatile layer, composited view, layer visibility. Tests: synthetic interleaved and planar tables render the same as hand-built scattered arrangers; a two-set map resolves metatiles and tiles across the 640 boundary; out-of-range blocks and tiles render empty; top-layer color 0 is transparent; a missing or compressed source makes the map read-only with the existing reasons.
5. **Project format and service.** Both resources, load order, cascades. Tests: round trip; a map naming a missing set fails the load naming it; deleting a set lists and removes its maps.
6. **FireRed fixture.** A script (in the scratchpad, like the tilemap ones) writes metatile sets for both primary tilesets and the secondary ones in use, and metatile maps for a sample of layouts (Pallet Town, Viridian City, Route 1, a cave, an interior). Manual: each exports a PNG that matches the game; CLI `exportall` is pixel-identical to an independent decoder, as for tilemaps.
7. **View UI and export.** Editor integration, layer list, composite export in UI and CLI. DevTools: Pallet Town opens, both layers show, hiding the top layer leaves the bottom one.
8. **Blocks mode.** Block palette, Block Pencil, Rectangle Fill, Block Picker, Erase, brush, undo, save. Tests: stamping and filling write only the targeted block numbers and keep collision and elevation bits (sentinel bytes around the map); a block outside every set's range refuses with a reason. Manual: on a copy of the FireRed project, place a tree in Pallet Town, save, and check the block map bytes and the town in an emulator.
9. **Add dialogs.** New Metatile Set and New Metatile Map with a rendered preview.
10. **Later phases** (separate tasks once 1–9 land): metatile editing through a metatile-order tilemap view; attribute display (raw values, then named fields through table definitions); collision and elevation overlays; table-resource generation of metatile maps and sets; Arrange (moving and copying block regions) and Draw on the active layer.
11. **Close.** Make the specs current, update ARCHITECTURE §3, delete this proposal and its backlog line.

## Open questions

- Settled: the names are "metatile map" and "metatile set" (Decisions).
- Settled: the first version has a read-only overlay of the block bits outside the metatile number (Decisions).
- Settled: no block copy and paste in the first version; Blocks mode only.
- Settled: a border is its own small metatile map (Decisions).
- Is a scratchpad script enough to populate FireRed for testing, or should a FireRed table definition be written alongside, as the first real use of [table-driven resources](table-resources.md)?
