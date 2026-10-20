---
id: UI-TILE-PLOTTING
title: Tile plotting
status: draft
project: TileShop.UI, TileShop.Shared
sources:
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.TilePlotting.cs
  - TileShop.UI/Features/Graphics/TilePaletteViewModel.cs
  - TileShop.UI/Features/Graphics/TilePaletteView.axaml
  - TileShop.UI/Features/Graphics/TilePaletteView.axaml.cs
  - TileShop.UI/Features/Graphics/Tools/CellRectangleToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/TileStrokeToolHandler.cs
  - TileShop.UI/Features/Renderer/TilePaletteRenderer.cs
  - TileShop.Shared/Models/History/PlotTilesHistoryAction.cs
types:
  - TileTool
  - TilePaletteViewModel
  - TileSlotOption
  - TilePaletteView
  - TilePaletteRenderer
  - CellRectangleToolHandler
  - TileStrokeToolHandler
  - PlotTilesHistoryAction
  - EditTileSetMessage
tests:
  - TilePlottingTests
  - GraphicsEditHistoryTests
depends:
  - LIB-TILEMAPS
  - UI-GRAPHICS-EDITOR
  - UI-ARRANGING
  - UI-EDIT-HISTORY
  - UI-EDITORS
---

# Tile plotting

## Purpose

Tiles mode is the graphics editor's (UI-GRAPHICS-EDITOR) fourth mode on a tilemap (LIB-TILEMAPS): it plots whole tiles into the map, as tilemap editors do, from a tile palette shown beside the canvas. The brush, tile palette and stamping logic are LIB-TILEMAPS "Tile plotting"; edits go to the editor's working copy and are written by the tilemap Save (UI-GRAPHICS-EDITOR-055), with undo from UI-EDIT-HISTORY. Arrange keeps rearranging entries (UI-ARRANGING) and Draw keeps drawing pixels on the shared tiles (UI-DRAWING). This spec also owns Edit Tile Set, which opens the tile set for pixel work.

## Requirements

### Mode and tile palette

- **UI-TILE-PLOTTING-001** — While an editor is open on a tilemap whose map is writable (LIB-TILEMAPS-031), the mode bar shall offer Tiles between Arrange and Draw (View, Arrange, Tiles, Draw); on a read-only map, a scattered arranger or a sequential arranger Tiles shall be hidden and refused, as Arrange is.
  - Tests: manual — DevTools: open "Title Map" in a copy of the FF2 demo project; `ViewModeButton`, `ArrangeModeButton`, `TilesModeButton`, `DrawModeButton` are in that tree order; open a scattered arranger and check `TilesModeButton.IsVisible` is false.
- **UI-TILE-PLOTTING-002** — While in Tiles mode, the editor shall show the tile palette (`TilePaletteView`) beside the canvas and the Tiles tools and brush controls in the toolbar, and hide the palette combo; in any other mode the tile palette is hidden.
  - Tests: manual — DevTools: click `TilesModeButton`; `TilePaletteView.IsVisible` is true and `TilePaletteStatusText` is empty.
- **UI-TILE-PLOTTING-003** — The tile palette shall be the tilemap's curated arranger when it names one, otherwise generated from its tile set (LIB-TILEMAPS-062, -063), drawn over the checkerboard in the brush slot's colors, with refused cells dimmed; while the pointer is over a refused cell, `TilePaletteStatusText` shall read "Element at (x, y) <reason>", and while the tile set has no tiles, "The tile set has no tiles".
  - Tests: `TilePlottingTests.FromArranger_KeepsValidCells_RefusesForeignOtherCodecAndUnalignedCells`; manual — add `tilepalette` naming a scattered arranger with unaligned elements to the copy's "Title Map.xml", open it in Tiles mode and check those cells are dimmed; hover one for the reason (pointer, by hand).
- **UI-TILE-PLOTTING-004** — When the user drag-selects in the tile palette, the brush shall become the selected rectangle's tiles with the current slot and flips (LIB-TILEMAPS-064) and the selection shall be outlined; a press on a refused or empty cell shall leave the brush unchanged and show the cell's reason.
  - Tests: `TilePlottingTests.CreateBrush_FromPaletteRectangle_SkipsRefusedCells`; manual — drag across the tile palette (pointer, by hand).
- **UI-TILE-PLOTTING-005** — The tile index box (`BrushTileIndexBox`, from the first tile to the last tile of the tile set) shall show the tile of a one-tile brush and be empty otherwise; entering a tile shall make a one-tile brush of it and select its tile palette cell, and `BrushDescriptionText` shall describe the brush ("Brush: tile n", "Brush: WxH tiles", or "No brush: pick a tile from the tile palette").
  - Tests: manual — DevTools: set `BrushTileIndexBox`'s text to 37 and check `BrushDescriptionText` reads "Brush: tile 37".
- **UI-TILE-PLOTTING-006** — The brush palette slot combo (`BrushPaletteSlotComboBox`) shall list slot 0 and each assigned slot as "Slot n: <palette>", starting on the tilemap's most-used slot (LIB-TILEMAPS-075); choosing one shall recolor the tile palette and set the brush's slot. The flip toggles (`BrushHFlipToggle`, `BrushVFlipToggle`) shall flip the brush (LIB-TILEMAPS-065) and be disabled with the tooltip "Map format '<name>' has no horizontal (vertical) flip field" when the format has no such field.
  - Tests: `TilePlottingTests.FlippedBrush_MirrorsLayoutAndEachCell`; manual — DevTools: on "Title Map" the combo has 8 items, starts on "Slot 1", and toggling `BrushHFlipToggle` leaves it checked.

### Tools

- **UI-TILE-PLOTTING-007** — The toolbar shall offer Tile Pencil (`TilePencilButton`), Rectangle Fill (`TileRectangleFillButton`), Tile Picker (`TilePickerButton`) and Erase (`TileEraseButton`), Tile Pencil by default; the checked button follows the displayed tool.
  - Tests: manual — DevTools: click each button and check it becomes the only checked one.
- **UI-TILE-PLOTTING-008** — When the user left-drags with Tile Pencil, the editor shall stamp the brush at the pressed cell and at every cell the pointer passes, snapped to the stroke's brush-sized grid (LIB-TILEMAPS-068), and outline the brush footprint under the pointer; a pointer leaving and re-entering the map shall not plot across the gap.
  - Tests: `TilePlottingTests.StampStroke_AlignsBrushToStrokeOrigin`; manual — drag a 2x2 brush across the FF4 title screen.
- **UI-TILE-PLOTTING-009** — When the user drags a rectangle with Rectangle Fill and releases, the editor shall fill it with the brush (LIB-TILEMAPS-069); the rectangle is outlined while dragging.
  - Tests: `TilePlottingTests.Fill_TilesBrushOverRectangle`; manual — drag a fill rectangle.
- **UI-TILE-PLOTTING-010** — When the user clicks or drags a rectangle with Tile Picker, the editor shall make the picked cells the brush (LIB-TILEMAPS-070) and show its slot and flips in the brush controls; a rectangle without a tile shows "No tile of the tile set to pick there". While Ctrl is held with another tile tool, Tile Picker shall act temporarily and be shown as the active tool, as Color Picker does in Draw mode.
  - Tests: `TilePlottingTests.Pick_SingleCell_GivesTileSlotAndFlips`, `TilePlottingTests.Pick_SeveralCells_KeepsCellMirrorsAndFirstCellSlot`; manual — Ctrl+click a mirrored cell with Tile Pencil and check the flip toggles.
- **UI-TILE-PLOTTING-011** — When the user left-drags with Erase, the editor shall empty every cell the pointer passes (LIB-TILEMAPS-071).
  - Tests: `TilePlottingTests.Erase_ThenWriteEntries_WritesEmptyEntry`; manual — erase a cell and save.
- **UI-TILE-PLOTTING-012** — If plotting starts with no brush, or a brush refused by LIB-TILEMAPS-066, then the editor shall change nothing and show "Pick a tile from the tile palette" or the refusal reason once per stroke.
  - Tests: `TilePlottingTests.FlipWithoutFormatField_IsRefused`, `TilePlottingTests.TileOutsideTileSet_IsRefused`

### History and Save

- **UI-TILE-PLOTTING-013** — A Tile Pencil stroke, a fill and an Erase stroke that change at least one cell shall each mark the editor modified and record one history step ("Plot Tiles", "Fill Tiles", "Erase Tiles") when the stroke ends by release, tool switch or mode change; one that changes nothing records nothing. Picking records nothing. Undo and redo restore the working copy (UI-EDIT-HISTORY-010, -012).
  - Tests: `GraphicsEditHistoryTests.PlotTiles_UndoRedo`, `TilePlottingTests.Stamp_SameTileAgain_ReportsNoChange`; manual — plot, undo, redo.
- **UI-TILE-PLOTTING-014** — Saving after plotting shall write only the entries that differ (UI-GRAPHICS-EDITOR-055, LIB-TILEMAPS-045), and switching modes with unsaved plots shall prompt as UI-GRAPHICS-EDITOR-003.
  - Tests: `TilePlottingTests.Stamp_ThenWriteEntries_ChangesOnlyTargetEntry`; manual — on a copy of the FF2 demo, plot, fill and erase on "Title Map", Ctrl+S, and compare the map bytes at 0x46000 with the original.
- **UI-TILE-PLOTTING-015** — When the working copy is replaced (save, discard, undo or redo of an arranger action, reload) or a palette's colors change, the tile palette shall be redrawn from the current tile pixels and palettes, keeping the brush.
  - Tests: manual — draw a tile in Draw mode, save, switch to Tiles and check the tile palette shows the new pixels.

### Edit Tile Set

- **UI-TILE-PLOTTING-016** — On a tilemap, the toolbar's Edit Tile Set button (`EditTileSetButton`, any mode) and the tree's Edit Tile Set command shall open the tile set in a new sequential editor (UI-EDITORS-064).
  - Tests: manual — DevTools: click `EditTileSetButton` on "Title Map"; a new tab shows `Offset: 0x44000`, codec SNES 4bpp, 16x32 elements and palette TitlePal0. The tree's context menu cannot be opened through DevTools; check it by hand.

## Invariants

- The tile palette exists exactly for editors on a tilemap with a writable map, and the plotter always edits the current working copy.
- Every Tiles mode edit goes through the working copy; nothing in Tiles mode writes the map or tiles before Save.

## Edge cases

- A brush larger than the map is clipped at the map edge; a stamp at a negative snapped cell plots only its visible part.
- A curated arranger whose element size differs from the tile size shows every cell dimmed; direct-color elements in it are not drawn.
- A tile set with no tiles shows an empty one-row tile palette and the status "The tile set has no tiles".

## Threading and lifetime

- UI thread only. The tile palette's bitmap is disposed when it is rebuilt; the view releases its callbacks when its `DataContext` changes.

## Decisions

- **A fourth mode, Tiles.** Plotting tiles is a mode of its own between Arrange and Draw, shown only for tilemaps with a writable map. Reason (user decision): tilemap editors plot tiles from a tile palette and do not draw pixels; Draw must stay pixel drawing on the shared tiles, and plotting tools clash with Arrange's selection and drag. Rejected: making Draw plot tiles on tilemaps; folding plotting into Arrange.
- **The tile palette is a side view.** It sits beside the canvas in the editor and shows only in Tiles mode. Reason: the brush is picked and used constantly, so it must stay visible next to the map. Rejected: a floating window (popups cannot be driven by DevTools and get lost behind the shell); a dockable pane (one more pane to manage for one mode).
- **Strokes tile the brush.** Tile Pencil snaps each stamp to the stroke's brush-sized grid (LIB-TILEMAPS, "Strokes stamp on a brush-sized grid"). Rejected: overlapping stamps at every cell.
- **One palette slot per brush** (LIB-TILEMAPS, "One palette slot per brush"), chosen in one combo that lists only assigned slots and slot 0.
- **Ctrl picks, as in Draw.** Ctrl engages Tile Picker temporarily with any tile tool, through the same router override as Color Picker in Draw mode. Reason: one picker habit across modes. Rejected: right-click to pick (right-click opens the canvas context menu).
- **Logic in the library, thin handlers.** Brush construction, stamping, fills, picks and tile palette layout are LIB-TILEMAPS code with unit tests; the tool handlers only map pointer events to cells. Reason: DevTools cannot drive canvas pointer gestures, so the behavior must be testable without them.
- **Edit Tile Set opens a sequential editor.** It reuses the data file editor path at the tile offset with the tilemap's codec and slot 0's palette. Reason: drawing tiles in a plain tile sheet is the established way, and a new view would duplicate the graphics editor. Rejected: a dedicated tile set editor.

## Non-goals

- Per-cell palette slots in a brush, priority painting, and pattern or random fills.
- Editing the curated tile palette from the tile palette view; it is edited as a scattered arranger.

## Open items

- Mode switches between Arrange and Tiles prompt Save Changes (UI-GRAPHICS-EDITOR-003) even though both edit the same working copy.
- Each plot re-renders the whole working image; large maps may need partial invalidation.
- Each history step stores a full snapshot of the working copy (UI-EDIT-HISTORY-009).
- Pointer gestures (stamping, filling, picking, erasing, drag-selecting in the tile palette, hover status) have no automated check.
