---
id: UI-BLOCK-PLOTTING
title: Metatile map editor and block plotting
status: draft
project: TileShop.UI, TileShop.Shared
sources:
  - TileShop.UI/Features/Metatiles
  - TileShop.UI/Features/Renderer/MetatileMapRenderer.cs
  - TileShop.UI/Features/Renderer/BlockPaletteRenderer.cs
  - TileShop.Shared/Models/History/PlotBlocksHistoryAction.cs
types:
  - MetatileMapEditorViewModel
  - MetatileEditMode
  - MetatileMapEditorView
  - LayerVisibilityViewModel
  - MetatileMapRenderer
  - BlockPaletteViewModel
  - BlockPaletteView
  - BlockPaletteRenderer
  - PlotBlocksHistoryAction
tests:
  - BlockPlottingTests
  - MetatileMapEditorViewModelTests
depends:
  - LIB-METATILE-MAPS
  - LIB-SCENES
  - UI-EDITORS
  - UI-EDIT-HISTORY
  - UI-TILE-PLOTTING
---

# Metatile map editor and block plotting

## Purpose

The editor for a metatile map (LIB-METATILE-MAPS). It shows the map composited from its metatile layers, lets the user hide layers and overlay each block's block bits, and in Blocks mode plots whole blocks from a block palette, as UI-TILE-PLOTTING does with tiles. It is its own editor rather than a graphics editor mode, because a metatile map is not an arranger. Edits go to a `BlockGrid` working copy and Save writes only the changed blocks (LIB-METATILE-MAPS-031).

## Requirements

### Opening and viewing

- **UI-BLOCK-PLOTTING-001** — When the user opens a metatile map node (Enter or double-click, as UI-EDITORS), the shell shall open a metatile map editor tab named after the map; opening it again shall activate the existing tab. If the map has a `MapError`, the shell shall alert "Map Error" with it and open nothing, as UI-EDITORS-063 does for tilemaps (UI-EDITORS-065).
  - Tests: manual — DevTools: open Pallet Town in a copy of the FireRed project; a tab with `MetatileMapEditorView` appears.
- **UI-BLOCK-PLOTTING-002** — The canvas shall show the map's composite (LIB-METATILE-MAPS-016) with the zoom, pan and grid behavior of the graphics editor (UI-GRAPHICS-EDITOR); the grid's cells shall be blocks, with an option to show tile cells. The toolbar shall offer `ShowGridlinesToggle`, `TileGridToggle`, `FitToViewButton` and `ResetZoomButton`; Ctrl+wheel zooms and middle-drag pans. The map is fitted to the view when the editor first shows, and fitted again whenever the canvas changes size until the user zooms, pans or resets the zoom, so the fit uses the canvas's settled size.
  - Tests: manual — DevTools: screenshot Pallet Town; it matches the game's town.
- **UI-BLOCK-PLOTTING-003** — The layer list (`LayerVisibilityList`) shall show one check box per metatile layer ("Bottom", "Top" for two layers, "Layer 1" to "Layer n" otherwise), all checked when the editor opens; unchecking one shall hide that layer in the composite and the block palette. Closing the editor shall make every layer visible again.
  - Tests: `MetatileMapEditorViewModelTests.HidingLayer_RedrawsWithoutIt`; manual — DevTools: uncheck "Top" on Pallet Town and screenshot.
- **UI-BLOCK-PLOTTING-004** — The block bits overlay toggle (`BlockBitsOverlayToggle`, off by default) shall draw each block's block bits value (LIB-METATILE-MAPS-017) as a number on the block, in a text style readable over any tiles; it is read-only and changes nothing.
  - Tests: manual — DevTools: toggle it on Pallet Town and screenshot; trees show 1 (collision 1, elevation 0) and paths 12 (collision 0, elevation 3).
- **UI-BLOCK-PLOTTING-005** — While the pointer is over a block, the status bar shall show its position, metatile number, set name, block bits value and the metatile's attribute in hex when the set has an attribute table, as "(x, y)  Block n  ·  set  ·  Bits v  ·  Attribute 0x…"; a number in no set shows no set name or attribute, and a block past the end of the block map data shows "(x, y)  No block data".
  - Tests: `MetatileMapEditorViewModelTests.Hover_DescribesBlock`; manual — hover a block (pointer, by hand).

### Blocks mode and block palette

- **UI-BLOCK-PLOTTING-010** — The mode bar shall offer View and Blocks (`MapViewModeButton`, `BlocksModeButton`), View when the editor opens. While the block map is read-only (LIB-METATILE-MAPS-032), Blocks shall be disabled with the reason as its tooltip.
  - Tests: `MetatileMapEditorViewModelTests.ReadOnlyBlockMap_DisablesBlocksWithReason`; manual — DevTools: `BlocksModeButton` is enabled on Pallet Town.
- **UI-BLOCK-PLOTTING-011** — While in Blocks mode, the editor shall show the block palette (`BlockPaletteView`, LIB-METATILE-MAPS-037) beside the canvas, drawn with the visible layers, and the block tools; dragging a rectangle in the palette shall make its blocks the brush and outline the selection.
  - Tests: `BlockPlottingTests.BlockPalette_LaysOutSetsOnNewRows`; manual — DevTools: click `BlocksModeButton` on Pallet Town, `BlockPaletteView` shows; uncheck "Top" and the palette redraws without it; drag in the block palette (pointer, by hand).
- **UI-BLOCK-PLOTTING-012** — The block number box (`BrushBlockNumberBox`) shall show the number of a one-block brush and be empty otherwise; entering a number held by a set shall make a one-block brush of it and select it in the palette (a number in no set leaves the brush and shows "Block n is in no metatile set" below the palette), and `BrushDescriptionText` shall read "Brush: block n", "Brush: WxH blocks" or "No brush: pick a block from the block palette".
  - Tests: manual — DevTools: set `BrushBlockNumberBox` to 641 and check the description.

### Tools

- **UI-BLOCK-PLOTTING-020** — The toolbar shall offer Block Pencil (`BlockPencilButton`), Rectangle Fill (`BlockRectangleFillButton`), Block Picker (`BlockPickerButton`) and Erase (`BlockEraseButton`), Block Pencil by default, behaving as UI-TILE-PLOTTING-008 to -012 with blocks for tiles (LIB-METATILE-MAPS-033 to -036), Ctrl giving a temporary Block Picker. A brush the plotter refuses (LIB-METATILE-MAPS-033) shows its reason once per stroke, as -021.
  - Tests: `BlockPlottingTests.Stroke_AlignsToFirstBlock`, `BlockPlottingTests.Fill_TilesBrush`, `BlockPlottingTests.Pick_Rectangle`, `BlockPlottingTests.Erase_WritesEmptyBlock`; manual — DevTools: click each tool button; plot, fill, pick and erase on Pallet Town by hand.
- **UI-BLOCK-PLOTTING-021** — If plotting starts with no brush, the editor shall change nothing and show "Pick a block from the block palette" once per stroke.
  - Tests: `MetatileMapEditorViewModelTests.PlotWithoutBrush_ShowsMessage`

### History, Save and export

- **UI-BLOCK-PLOTTING-030** — A pencil stroke, fill or erase stroke that changes a block shall mark the editor modified and record one history step ("Plot Blocks", "Fill Blocks", "Erase Blocks"), with undo and redo restoring the block grid (UI-EDIT-HISTORY).
  - Tests: `MetatileMapEditorViewModelTests.PlotBlocks_UndoRedo`
- **UI-BLOCK-PLOTTING-031** — Save (Ctrl+S) shall write the block grid (LIB-METATILE-MAPS-031), clear the modified state and redraw; a write failure shall alert "Save Error" with the reason and keep the edits. Closing a modified editor shall prompt to save, as other editors do (UI-EDITORS).
  - Tests: `MetatileMapEditorViewModelTests.Save_WritesChangedBlocksOnly`, `MetatileMapEditorViewModelTests.Save_Failure_AlertsAndKeepsEdits`; manual — on a copy of the FireRed project, set block (3, 4) of Pallet Town through the plotter, write, and compare the ROM: only the 2 bytes at 0x2DD100 + 2 × (4 × 24 + 3) change, in bits 0–9.
- **UI-BLOCK-PLOTTING-032** — Export As... on a metatile map node and in the editor (`ExportAsButton`) shall write the composite with the current layer visibility to a PNG (direct color); the editor's export draws its block grid, unsaved edits included.
  - Tests: manual — export Pallet Town and compare with the in-editor screenshot.
- **UI-BLOCK-PLOTTING-033** — When a source the map reads raises `DataWritten` while the editor is not modified, the editor shall read its block grid again, clear its history and redraw; while it is modified, it keeps its block grid and redraws the metatiles and tiles only. The shell calls `OnSourceChanged` for every open metatile map editor that reads the changed palette or source.
  - Tests: untested

### Metatile sets

- **UI-BLOCK-PLOTTING-040** — A metatile set node shall offer Edit Tile Set, opening its tile set as UI-TILE-PLOTTING-016 does for a tilemap (UI-EDITORS-064: the set's tile source, offset, codec, tile count and slot 0).
  - Tests: manual — use the context menu on FireRed's primary set (by hand).

## Invariants

- The editor's composite always shows the block grid's numbers drawn through the current metatile tables and tiles.
- Nothing is written to the data until Save.

## Edge cases

- A map with no writable block map opens in View only; `BlocksModeButton` is disabled with "The block map is read-only because it …" as its tooltip.
- A block whose number is outside every set shows as empty, and the status bar names no set for it.

## Threading and lifetime

- As UI-EDITORS: one editor per map, disposed with its tab; it unsubscribes from the map's sources when closed.

## Decisions

- **A separate editor, not a graphics editor mode.** Reason: the graphics editor is built around one arranger and its working clone, and a metatile map is neither (LIB-METATILE-MAPS "A metatile map is a resource with layer arrangers, not an arranger"). The block tools reuse the Tiles mode handler classes and the shared canvas and renderer controls. Rejected: a fifth graphics editor mode that special-cases every arranger path.
- **View and Blocks only.** Reason: the user scoped the first version to plotting blocks, without region copy and paste; pixel work goes through Edit Tile Set. Rejected: Arrange and Draw in the first version.
- **The composite is re-rendered whole, not patched.** The editor keeps one bitmap of `MetatileMap.Composite()` and replaces it on every layer toggle or data change. Reason: a FireRed town composites in milliseconds, and View mode has no per-pixel edits to track. Rejected: rendering each layer to its own canvas bitmap (more state for no visible gain in View mode).
- **Layer visibility lives on the map, reset on open and close.** The tree's Export As... uses the visibility the open editor last set, and the editor makes every layer visible when it opens and when it closes (LIB-METATILE-MAPS-016). Reason: one map object, one visibility, so the export matches what the user sees, and with no editor open nothing on screen hides a layer. Rejected: per-editor visibility passed to export (two sources of truth).
- **Read-only block-bits overlay.** Reason: the user approved a read-only overlay for the first version; raw values need no per-game definition.
- **A map with a map error opens nothing (user decision).** A metatile map whose `MapError` is set alerts "Map Error" and opens no editor, as tilemaps do (UI-EDITORS-063, -065). Reason: an editor over a partly read block map would let Blocks mode and export work on blocks that are not in the data. Rejected: opening with the readable blocks only.
- **Tool handler classes shared through `ICellPlotState`.** `TileStrokeToolHandler<TState>` and `CellRectangleToolHandler<TState>` serve Tiles mode and Blocks mode; each editor implements cell lookup, stroke begin and end, and hover status. Reason: the gestures are identical with blocks for tiles. Rejected: copying the handlers for blocks.
- **The `TileTool` enum is reused for block tools.** Reason: the four tools and their order are the same, so the tool buttons and router code match Tiles mode. Rejected: a separate `BlockTool` enum with the same members.
- **Undo stores block grid snapshots.** Each `PlotBlocksHistoryAction` holds a copy of the block grid after it (UI-EDIT-HISTORY-018). Reason: a grid of numbers is small, and snapshots make undo and redo exact. Rejected: `GraphicsEditHistory`, which is bound to one arranger and its working clone.
- **The fit repeats until the user zooms or pans.** Reason: a single fit at the first size change could run before the canvas reached its final size, leaving the map's bottom edge cut off; refitting on each size change ends on the settled size. Rejected: one fit posted at `Loaded` priority (still depends on when the dock finishes its layout).

## Non-goals

- Editing block bits, metatiles or attributes; copy and paste of block regions; drawing pixels on the map.

## Open items

- Each plot re-renders the whole composite; large maps (100×100 blocks) may need redrawing at most once per pointer move or patching per block.
- Pointer gestures (stamping, filling, picking, erasing, drag-selecting in the block palette, hover status) have no automated check; DevTools cannot drive them.
- Drawing a border map around the map (LIB-METATILE-MAPS Open items).
