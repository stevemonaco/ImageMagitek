---
id: UI-EDIT-HISTORY
title: Graphics edit history
project: TileShop.UI
sources:
  - TileShop.UI/Features/Graphics/GraphicsEditHistory.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.History.cs
  - TileShop.UI/Features/ResourceEditorBaseViewModel.cs
  - TileShop.UI/Models/History/ColorRemapHistoryAction.cs
  - TileShop.UI/Models/History/PasteArrangerHistoryAction.cs
  - TileShop.Shared/Models/History/HistoryAction.cs
  - TileShop.Shared/Models/History/ArrangerHistoryAction.cs
  - TileShop.Shared/Models/History/ArrangerSnapshot.cs
  - TileShop.Shared/Models/History/PencilHistoryAction.cs
  - TileShop.Shared/Models/History/FloodFillAction.cs
  - TileShop.Shared/Models/History/ElementPasteHistoryAction.cs
  - TileShop.Shared/Models/History/ApplyPaletteHistoryAction.cs
  - TileShop.Shared/Models/History/MirrorElementHistoryAction.cs
  - TileShop.Shared/Models/History/RotateElementHistoryAction.cs
  - TileShop.Shared/Models/History/DeleteElementSelectionHistoryAction.cs
  - TileShop.Shared/Models/History/ResizeArrangerHistoryAction.cs
types:
  - GraphicsEditHistory
  - HistoryAction
  - ArrangerHistoryAction
  - ArrangerSnapshot
  - PencilHistoryAction
  - FloodFillAction
  - ColorRemapHistoryAction
  - PasteArrangerHistoryAction
  - ElementPasteHistoryAction
  - ApplyPaletteHistoryAction
  - MirrorElementHistoryAction
  - RotateElementHistoryAction
  - DeleteElementSelectionHistoryAction
  - ResizeArrangerHistoryAction
tests:
  - GraphicsEditHistoryTests
depends:
  - LIB-ARRANGERS
  - LIB-IMAGES
  - LIB-CODECS
  - UI-GRAPHICS-EDITOR
  - UI-ARRANGING
  - UI-DRAWING
---

# Graphics edit history

## Purpose

Undo and redo for the graphics editor (UI-GRAPHICS-EDITOR). Arranger-level actions (UI-ARRANGING) restore snapshots of the arranger; pixel actions (UI-DRAWING) re-decode from the data source and replay. The palette editor's history (`PaletteHistoryAction`, `PaletteSnapshot`) is UI-PALETTE-EDITOR.

## Requirements

### Commands

- **UI-EDIT-HISTORY-001** — When the user presses Ctrl+Z or chooses Edit → Undo, the editor shall undo the most recent action; Ctrl+Y or Edit → Redo shall redo the most recently undone one.
  - Tests: untested
- **UI-EDIT-HISTORY-002** — Undo shall be enabled only while the undo list is non-empty and Redo only while the redo list is non-empty, and every add, undo, redo and clear shall re-notify both commands and the `CanUndo`/`CanRedo` properties.
  - Tests: untested
- **UI-EDIT-HISTORY-003** — When an action is recorded, the editor shall clear the redo list.
  - Tests: `GraphicsEditHistoryTests.Add_ClearsRedo`
- **UI-EDIT-HISTORY-004** — After an undo, the editor shall be modified exactly when the undo list is non-empty; after a redo, it shall be modified.
  - Tests: untested

### What is recorded

- **UI-EDIT-HISTORY-005** — The editor shall record a Pencil stroke, Flood Fill, Color Remap and pixel paste as pixel actions.
  - Tests: `GraphicsEditHistoryTests.Pencil_Indexed_UndoRedo`, `GraphicsEditHistoryTests.Pencil_Direct_UndoRedo`, `GraphicsEditHistoryTests.FloodFill_IndexedWithClip_UndoRedo`, `GraphicsEditHistoryTests.FloodFill_DirectWithClip_UndoRedo`, `GraphicsEditHistoryTests.ColorRemap_WithBounds_UndoRedo`, `GraphicsEditHistoryTests.PixelPaste_UndoRedo`
- **UI-EDIT-HISTORY-006** — The editor shall record element paste, Apply Palette, Mirror, Rotate, Delete and Resize Arranger as arranger actions.
  - Tests: `GraphicsEditHistoryTests.ElementPaste_UndoRedo`, `GraphicsEditHistoryTests.ApplyPalette_UndoRedo`, `GraphicsEditHistoryTests.Mirror_UndoRedo`, `GraphicsEditHistoryTests.Rotate_UndoRedo`, `GraphicsEditHistoryTests.DeleteNonSquareElements_UndoRedo`, `GraphicsEditHistoryTests.Resize_UndoRedo`
- **UI-EDIT-HISTORY-007** — The editor shall not record sequential navigation, codec, layout or size changes in View mode, grid changes, palette association, mode changes or image import.
  - Tests: untested
- **UI-EDIT-HISTORY-008** — When an in-progress Pencil stroke or Apply Palette drag is ended by a button release, a tool switch, a mode change or the pointer leaving the canvas, the editor shall record it once if it changed anything.
  - Tests: untested

### Undo and redo semantics

- **UI-EDIT-HISTORY-009** — When an arranger action is recorded, the history shall store a snapshot of the arranger's elements and each element's palette as they are after the action.
  - Tests: `GraphicsEditHistoryTests.MirrorThenRotate_UndoUndoRedoRedo_RestoresEachState`
- **UI-EDIT-HISTORY-010** — When an arranger action is undone, the editor shall restore the snapshot of the latest earlier arranger action, or the state at the last open, save or discard, then replay the pixel actions recorded after that point.
  - Tests: `GraphicsEditHistoryTests.MirrorThenRotate_UndoUndoRedoRedo_RestoresEachState`, `GraphicsEditHistoryTests.ApplyPaletteAfterPixelPaste_UndoRedo_KeepsPaste`
- **UI-EDIT-HISTORY-011** — When a pixel action is undone, the editor shall re-decode the pixels from the data sources and replay the remaining pixel actions recorded since the latest arranger action other than Apply Palette.
  - Tests: `GraphicsEditHistoryTests.PixelEditAfterArrangerAction_Undo_ReplaysFromSnapshot`, `GraphicsEditHistoryTests.ApplyPaletteAfterPixelPaste_UndoRedo_KeepsPaste`
- **UI-EDIT-HISTORY-012** — When an arranger action is redone, the editor shall restore its snapshot and replay the pixel actions after it; when a pixel action is redone, the editor shall re-apply it to the current image.
  - Tests: `GraphicsEditHistoryTests.MirrorThenRotate_UndoUndoRedoRedo_RestoresEachState`
- **UI-EDIT-HISTORY-013** — Restoring a snapshot shall never change a codec instance shared with another arranger; elements whose palette differs from the snapshot get a cloned codec.
  - Tests: `GraphicsEditHistoryTests.ApplyPalette_Undo_DoesNotModifySharedCodec`
- **UI-EDIT-HISTORY-014** — When undo or redo replaces the working arranger, the editor shall rebuild the image and gridlines and clear the selection and floating paste.
  - Tests: untested
- **UI-EDIT-HISTORY-015** — When the editor saves, discards, or reloads from source, it shall clear both lists and make the current arranger the state that undoing everything returns to.
  - Tests: untested

### Limits

- **UI-EDIT-HISTORY-016** (inherited) — The undo and redo lists shall have no size limit.
  - Tests: untested

## Invariants

- The snapshot of the most recent arranger action in the undo list matches the working arranger's elements and palettes.
- A sequential editor has no base snapshot and never records arranger actions.

## Edge cases

- Undoing the first arranger action of a sequential editor cannot happen, since sequential editors have no Arrange mode.
- A pixel paste that changed nothing is still recorded and replays as a no-op.

## Threading and lifetime

- UI thread only. The undo and redo lists are the editor's `UndoHistory`/`RedoHistory` collections; `GraphicsEditHistory` holds them and the base snapshot for the editor's lifetime.

## Decisions

- **Arranger actions snapshot the arranger.** Every arranger action stores a snapshot of the arranger taken after it ran; undo restores the previous snapshot. Reason: simpler than making each action invertible, and arrangers are small. Rejected: invertible actions. This replaced a model where Mirror, Rotate, Apply Palette, Delete and Resize were recorded but never redone, and undo re-cloned the arranger only when an element paste was in history.
- **Pixel actions replay from source.** Undoing a pixel action re-decodes from the data source and replays the remaining actions instead of storing pixel snapshots. Reason: pixel edits are small to replay and the source holds the saved state. Consequence: undo depends on the source being unchanged since the last save (see Open items).
- **Snapshots store palettes separately.** Cloned arrangers share codec instances and Apply Palette changes the codec in place, so a snapshot keeps each element's palette and restore clones the codec when it differs.
- **Apply Palette does not reset pixel replay.** It changes only which palette decodes the pixels, so pixel actions before it are replayed after undoing past it.
- **Every action goes through `AddHistoryAction`.** Color Remap once added to the undo list directly, skipping the redo clear and the CanUndo/CanRedo notifications.

## Non-goals

- An undo history panel (backlog, P3).
- Undo across editors or for project tree operations.

## Open items

- Pixel undo re-decodes from the data source: if the source changes while the editor is modified (the reload skips modified editors) or, in Draw mode on a sequential arranger, the view is moved with the navigation keys, replay lands on the new data.
- The fields kept by `MirrorElementHistoryAction`, `RotateElementHistoryAction`, `DeleteElementSelectionHistoryAction` and `ResizeArrangerHistoryAction` (positions, rect, size) are unused since the snapshot model; `ApplyHistoryAction` is never called from outside.
- A stroke ended by a button release while a temporary Color Picker is engaged is never recorded (UI-DRAWING Open items).
- Each arranger action clones the whole arranger with no bound on list length.
- No test covers the ViewModel wiring: modified state after undo/redo, command notification, and the clear on save/discard/reload.
