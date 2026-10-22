# Tilemap editing on an entry grid

## Why

Line numbers are at commit c7d5bfc, before roadmap steps 1-3.

`TilemapArranger` is an `Arranger` that cannot do what an arranger does:

- `Resize`, `SetElement` and `ResetElement` throw (`TilemapArranger.cs:282-291`, specified by LIB-TILEMAPS-028). The base `Arranger.UnlinkResource` calls `ResetElement` (`Arranger.cs:346`), so unlinking a tilemap would throw. It is latent: the only caller passes scattered arrangers (`ProjectService.cs:1017-1020`).
- Editing goes through a bake. The editor clones the tilemap into a `ScatteredArranger` (LIB-TILEMAPS-029, `:296-325`), edits elements, then turns elements back into entries on Save (`PlanWrites` :439-496, using `FindTileIndex` :385 and `FindCodecSlot` :372). An element has no slot field, so the tilemap remembers which slot each cloned codec was made for in a `ConditionalWeakTable<IGraphicsCodec, StrongBox<int>>` (`:23`, LIB-TILEMAPS-073). Palette slots therefore ride on object identity through history snapshots, pastes and the plotter.
- Tile plotting (`TilemapPlotter`, 205 lines) edits elements on that bake. Metatile maps chose the opposite design: `MetatileMapPlotter` (110 lines) edits a `BlockGrid` of numbers (LIB-METATILE-MAPS-030). Brushes and palettes are duplicated across the two: `TileBrush`/`BlockBrush`, `TilePalette`/`BlockPalette`.
- `TilemapArranger` exposes its edit internals publicly: `FindCodecSlot`, `FindTileIndex`, `CreateTileElement`, `CloneSlotCodec`, `ToCellElement` and `CanWriteEntries`.
- The UI branches on type. `TileShop.UI` has 13 `is TilemapArranger` patterns (12 in the `GraphicsEditorViewModel` partials, for example `GraphicsEditorViewModel.cs:123, 331, 485, 499, 502-507, 522, 575`, plus `EditorsViewModel.cs:253`) and 4 casts (`ProjectTreeViewModel.cs:394, 404`, `TilemapNodeViewModel.cs:13`, `GraphicsEditorViewModel.TilePlotting.cs:57`).
- `MetatileLayerArranger` has the same shape: read-only with throwing setters and a throwing clone (`MetatileLayerArranger.cs:47-61`), and it reuses `ArrangerMode.Tilemap` (`:39`). `GraphicsEditorViewModel` clones every `ArrangerMode.Tilemap` arranger (`GraphicsEditorViewModel.cs:312`), so opening a layer in a graphics editor would throw. That is latent too, because layers are never tree items.

None of this is a bug today. These are recorded decisions in LIB-TILEMAPS: "Edit a bake, write entries on Save", "Plotting edits the bake", "A cell's slot rides on its codec instance", "Clone is a bake". This proposal argues for reversing the first three, now that metatile maps show the grid design working.

## What

**Tilemaps edit an entry grid.** A `TilemapGrid` is a working copy of the map's entries (`MapEntry?[,]`, null where the map data ends). Like `BlockGrid`, it is read from the map, edited, and written back with `Tilemap.WriteEntries(TilemapGrid)`. The write sets only the entries that differ, keeping priority and unknown bits (LIB-TILEMAPS-045, -046). A cell's slot is a field of its entry, so the codec→slot table, `FindCodecSlot` and LIB-TILEMAPS-073 go away.

**Rendering reads a view.** A new read-only `IArrangerView` interface (element grid, sizes, color type, layout, `GetElement`) is implemented by `Arranger`. A tilemap exposes a view derived from a grid, either its own or an editor's working grid, and a metatile map exposes one view per layer. Image decode and encode (`IndexedImage`, `DirectImage`), the renderers, PNG export and Draw-mode pixel writes take `IArrangerView`. Pixel writes need only element addresses, never `SetElement`. `TilemapArranger` and `MetatileLayerArranger` stop being `Arranger`s, which removes every throwing override. `ArrangerMode.Tilemap` is no longer reused for layers.

**Arrange mode on a tilemap becomes grid operations.** Move, copy, cut, delete and Apply Palette edit entries. Pasting elements from another arranger converts each one to an entry with the tile set's index lookup and the slot rules of LIB-TILEMAPS-041 to -043, now internal to the tilemap. Bake to Scattered Arranger keeps cloning the view into a `ScatteredArranger` (LIB-TILEMAPS-029 stays for that purpose).

**One plotter, brush and palette.** `GridPlotter<TCell>` (stamp, stroke snap, fill, erase, pick), `GridBrush<TCell>` and `CellPalette<TCell>` replace the tile and block pairs. The tilemap's cell is `TileBrushCell` with brush-level slot and flips. The metatile map's cell is a block number. The behavior of LIB-TILEMAPS-064 to -070 and LIB-METATILE-MAPS-033 to -037 is unchanged.

**The editor holds one session object.** `GraphicsEditorViewModel` chooses an edit session at construction (scattered, sequential, tilemap). The 13 type checks become session members (`CanPlotTiles`, rotate and mirror disabled reasons, save, reload, `ReadsFrom`).

**Edit internals become internal.** `FindTileIndex`, `CreateTileElement`, `CloneSlotCodec`, `ToCellElement` and the slot lookups become `internal`, and `ImageMagitek` grants `InternalsVisibleTo` to the test project (it does not today).

What does not change: the project format, the editor's modes and tools, the prompts and messages, the shared-tile resolution on Draw save and import (LIB-TILEMAPS-050, -051), and the CLI import and export results.

Cost: large, about 2 weeks. The `IArrangerView` change touches the image and render paths (3 days); the grid, writes and Arrange-as-grid take about 4 days; the generic plotter, brush and palette take 2; the UI session refactor and tests take 3.

## Decisions

- **A grid of entries, not elements.** The entry is what is stored, so the slot, flips and tile index are explicit, and Save is a diff of two grids. Reason: the codec→slot table exists only because elements cannot carry a slot. Rejected: adding a slot field to `ArrangerElement`, which spreads a tilemap concept into every arranger and codec path.
- **A read-only view interface, not a read-only arranger subclass.** Reason: a subclass still inherits the mutators, so it either throws or silently ignores. Rejected: a `DerivedArranger` base that throws (today's design under a new name).
- **Arrange keeps its tools.** The user sees the same Arrange mode. Only its implementation on a tilemap changes from element edits to entry edits. Rejected: dropping Arrange on tilemaps in favor of Tiles mode only, which removes UI-ARRANGING-050 to -055.
- **Generic plotting.** One `GridPlotter<TCell>` for both map kinds. Reason: the two plotters already share stroke snapping (LIB-METATILE-MAPS-034 cites LIB-TILEMAPS-068), and brushes differ only in the cell type and transforms.
- **Reverses LIB-TILEMAPS Decisions** "Edit a bake, write entries on Save", "Plotting edits the bake" and "A cell's slot rides on its codec instance", and narrows "A derived arranger, not a new editor" to "a derived view, same editor". "Clone is a bake" stays for Bake to Scattered Arranger.

## Spec changes

- **LIB-TILEMAPS:** strike -028, because the view has no mutators. Narrow -029 to the bake command. Strike -073, because the slot is in the entry. Reword -043 and -044 as entry conversion and the grid diff, and -045 and -046 as writing a `TilemapGrid`. Reword -064 to -070 for `GridBrush<TileBrushCell>`. Add (draft wording): "A tilemap grid shall be a working copy of the map's entries, null past the end of the map data"; "Moving, copying, deleting and Apply Palette on a tilemap shall edit entries, keeping each moved entry's slot and flips". Update the Decisions as above.
- **LIB-METATILE-MAPS-015:** "a derived arranger (`MetatileLayerArranger`)" becomes "a derived view". -030 and -033 to -037: rename the types to the generic ones, with no behavior change.
- **LIB-ARRANGERS-009, -049:** replace "a tilemap arranger" with "a tilemap view". Add (draft): "`IArrangerView` shall expose the element grid, sizes, color type and layout read-only".
- **LIB-IMAGES / LIB-IMAGE-IO:** images and export take `IArrangerView`. Reword where those specs say "arranger" for read paths.
- **UI-ARRANGING-050 to -055, UI-TILE-PLOTTING-001 to -015, UI-BLOCK-PLOTTING-011 to -021, UI-GRAPHICS-EDITOR-001, -037, -038, -054, -055:** no behavior change. Update the `sources`/`types` lists and the wording that names a clone or bake.

## Tasks

1. **`IArrangerView`.** Add it, have `Arranger` implement it, and move images, renderers and export onto it. Tests: the image and export suites pass; a view-only test double renders and exports.
2. **Tilemap as view plus grid.** Add `TilemapGrid` and `WriteEntries(TilemapGrid)`, and make the tilemap a view. Remove the codec→slot table and make the internals internal. Tests (`TilemapArrangerTests`): grid round trip; a write keeps priority and unknown bits; slots that share a palette stay distinct through copy, move and undo (today's LIB-TILEMAPS-073 cases, rewritten on entries); the error cases of -046.
3. **Metatile layers as views.** `MetatileLayerArranger` implements only `IArrangerView`. Tests: `MetatileMapTests` pass.
4. **Generic plotting.** `GridPlotter<TCell>`, `GridBrush<TCell>`, `CellPalette<TCell>`, replacing the six types. Tests: `BlockPlottingTests` and the tilemap plotting tests pass, parameterized over both cell types where they overlap.
5. **Editor session.** Add the tilemap, scattered and sequential sessions in `GraphicsEditorViewModel`; Arrange on a tilemap edits the grid; remove the type checks. Tests: the tilemap editor view-model tests (paste conversion, Apply Palette refusal UI-ARRANGING-054, delete then Save UI-ARRANGING-053, the save check UI-GRAPHICS-EDITOR-055).
6. **Manual:** in the FF2 test project, Tiles and Arrange on a tilemap with two slots sharing a palette; paste from a scattered arranger; undo and redo across modes; Draw on a tile used twice and Save.
7. **Close.** Update the specs and Decisions above and the backlog.

## Open questions

- **Reverse the recorded decisions?** This reverses three LIB-TILEMAPS decisions made when tilemaps landed and narrows a fourth. It needs the user's go-ahead before any work.
- **A shared `TileSetModel`.** `TilemapModel` and `MetatileSetModel` repeat the tile set fields (`TileDataKey`, `TileOffset`, `CodecName`, `FirstTile`, `TileCount`, `PaletteSlots`), and the runtime already shares `TileSet`. A model sub-object with the same flat attributes (`tiledatafile`, `tileoffset`, `codec`, `firsttile`, `tilecount`, `paletteslot`, already spelled the same in both elements) is not a format change. A nested `<tileset>` element would be, and would need a format version (LIB-PROJECT-FORMAT-006). Proposed: the model sub-object only, after roadmap step 3 (records).
- Should the CLI importer (`Importer.cs:28` accepts `ArrangerMode.Tilemap`) keep importing into tilemaps through the view, or through a grid? Pixel import needs only the view, so the proposal is the view.
