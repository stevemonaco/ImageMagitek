---
id: UI-ARRANGING
title: Arranging and selection
project: TileShop.UI
sources:
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Selection.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.ArrangerTools.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Input.cs
  - TileShop.UI/Features/Graphics/Tools/ElementSelectToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/PixelSelectToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/ApplyPaletteToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/PickPaletteToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/MirrorToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/RotateToolHandler.cs
  - TileShop.UI/Features/Dialogs/ResizeTiledScatteredArrangerViewModel.cs
  - TileShop.UI/Features/Dialogs/ResizeTiledScatteredArrangerView.axaml
  - TileShop.UI/Features/Dialogs/AssociatePaletteViewModel.cs
  - TileShop.UI/Features/Dialogs/AssociatePaletteView.axaml
  - TileShop.UI/Features/Project/ProjectTreeViewModel.cs
  - TileShop.UI/Models/ArrangerPaste.cs
  - TileShop.UI/ViewExtenders/DragDrop/ArrangerDragHandler.cs
  - TileShop.UI/ViewExtenders/DragDrop/ArrangerDropHandler.cs
  - TileShop.Shared/Models/ArrangerSelection.cs
  - TileShop.Shared/Models/SnappedRectangle.cs
  - TileShop.Shared/Models/SelectionHandle.cs
  - TileShop.Shared/Models/AssociatePaletteModel.cs
types:
  - ArrangeTool
  - ElementSelectToolHandler
  - PixelSelectToolHandler
  - ApplyPaletteToolHandler
  - PickPaletteToolHandler
  - MirrorToolHandler
  - RotateToolHandler
  - ArrangerSelection
  - SnappedRectangle
  - SnapMode
  - SelectionHandle
  - ArrangerPaste
  - ArrangerDragHandler
  - ArrangerDropHandler
  - ResizeTiledScatteredArrangerViewModel
  - AssociatePaletteViewModel
  - AssociatePaletteModel
tests:
  - GraphicsEditHistoryTests
depends:
  - LIB-ARRANGERS
  - LIB-IMAGES
  - LIB-PALETTES
  - LIB-PROJECT-SERVICE
  - UI-GRAPHICS-EDITOR
  - UI-EDIT-HISTORY
  - UI-DRAWING
  - UI-PROJECT-TREE
---

# Arranging and selection

## Purpose

Element-level work in the graphics editor (UI-GRAPHICS-EDITOR): selecting, copying, pasting and dragging elements, deleting and cutting them, resizing a scattered arranger, creating a scattered arranger from a selection, applying and picking palettes, and the opt-in mirror and rotate tools. The element operations themselves (copy, reset, mirror, rotate, set palette) are LIB-ARRANGERS and LIB-IMAGES; pixel-level paste is in UI-DRAWING; history recording is in UI-EDIT-HISTORY. The receiver of "Add as New Scattered Arranger..." lives in `ProjectTreeViewModel` (UI-PROJECT-TREE) and is specified here.

## Requirements

### Selection

- **UI-ARRANGING-001** — When the user left-drags with Element Select (View or Arrange mode), the editor shall select the element-snapped rectangle covering the drag, expanding to whole elements.
  - Tests: manual — drag across elements in Arrange mode.
- **UI-ARRANGING-002** — When the user left-drags with Pixel Select, the editor shall select a rectangle snapped by the current snap mode.
  - Tests: manual — drag with Pixel Select in each snap mode.
- **UI-ARRANGING-003** — When the user Ctrl+clicks with a select tool, the editor shall select the single cell under the pointer without dragging.
  - Tests: manual — Ctrl+click an element with Element Select.
- **UI-ARRANGING-004** — While in Arrange mode with no floating paste and no temporary tool, moving the pointer with Shift held shall select the single element under the pointer.
  - Tests: manual — hold Shift and hover elements in Arrange mode.
- **UI-ARRANGING-005** — When the user drags one of the eight selection handles, the editor shall move that edge or corner; handles are drawn in Arrange and Draw modes.
  - Tests: manual — drag each handle.
- **UI-ARRANGING-006** — The editor shall clamp selections and handle drags to the arranger bounds, or to the active draw clip in Draw mode.
  - Tests: untested
- **UI-ARRANGING-007** — If a selection or handle drag ends with zero width or height, then the editor shall clear the selection.
  - Tests: untested
- **UI-ARRANGING-008** — When the user presses Ctrl+A or chooses Select All, the editor shall select the whole arranger, or the active draw clip in Draw mode, replacing any floating paste.
  - Tests: untested
- **UI-ARRANGING-009** — When the user presses Escape, the editor shall clear the selection and any floating paste.
  - Tests: untested
- **UI-ARRANGING-010** — While a selection exists, the canvas shall draw the image outside the selection in greyscale and the selection in color with a green outline.
  - Tests: manual — select elements and inspect the canvas.
- **UI-ARRANGING-011** — When the user switches to an Arrange tool other than Element Select, Pixel Select or Apply Palette, the editor shall clear the selection and floating paste; switching to Element Select shall set element snapping.
  - Tests: untested

### Copy and paste

- **UI-ARRANGING-012** — When the user presses Ctrl+C with a selection, the editor shall copy elements under element snapping, or indexed or direct pixels under pixel snapping, into a clipboard shared by every editor in the app; Copy is disabled without a selection.
  - Tests: untested
- **UI-ARRANGING-013** — When the user presses Ctrl+V holding copied elements in Arrange mode on a tiled scattered arranger, the editor shall show the copy as a floating element paste at (0, 0); any other paste goes through Draw mode (UI-DRAWING). Paste is disabled while the clipboard is empty.
  - Tests: untested
- **UI-ARRANGING-014** — While a floating paste exists, Enter or a left click outside it shall apply it, and Escape shall cancel it.
  - Tests: untested
- **UI-ARRANGING-015** — When an element paste is applied, the editor shall copy the elements to the cells under the paste, cropping whatever falls outside the arranger, record history, mark the editor modified and show "Paste successfully applied".
  - Tests: `GraphicsEditHistoryTests.ElementPaste_UndoRedo`
- **UI-ARRANGING-016** — If pasted elements came from a different project than the target editor's resource, then the paste shall fail with the status "Copying arranger elements across projects is not permitted" and change nothing.
  - Tests: untested
- **UI-ARRANGING-017** — The canvas shall draw a floating element paste with a magenta overlay and a floating pixel paste with a blue overlay, both clipped to the arranger.
  - Tests: manual — paste elements and pixels.

### Drag and drop

- **UI-ARRANGING-018** — When the user drags a selection past the drag threshold, the editor shall lift it into a floating paste of the selection (elements or pixels by snap mode) and clear the selection; the source arranger is unchanged.
  - Tests: manual — drag a selection within one editor.
- **UI-ARRANGING-019** — While a drag is over an editor, that editor shall show the paste under the pointer, snapped to elements in Arrange mode and to pixels otherwise, kept inside the draw clip when one is active.
  - Tests: manual — drag a selection from one editor tab into another.
- **UI-ARRANGING-020** — If dragged pixels are dropped on an editor that cannot draw, then the drop shall be ignored.
  - Tests: manual — drag pixels onto a read-only arranger.
- **UI-ARRANGING-021** — When the user drops with Shift held, the target shall apply the paste immediately; otherwise it shall keep it floating with "Press [Enter] to Apply Element|Pixel Paste or [Esc] to Cancel" and activate the target's tab.
  - Tests: manual — drop with and without Shift.
- **UI-ARRANGING-022** — When a drag leaves an editor, that editor shall clear its selection and floating paste.
  - Tests: manual — drag out of an editor.
- **UI-ARRANGING-023** — When the user drags a floating paste, the editor shall move it with the pointer.
  - Tests: manual — drag a floating paste.

### Delete and Cut

- **UI-ARRANGING-024** — When the user presses Delete or chooses Edit → Delete in Arrange mode on a scattered arranger with an element-snapped selection, the editor shall reset every covered element to empty, record history and mark the editor modified; Delete is disabled in any other state.
  - Tests: `GraphicsEditHistoryTests.DeleteNonSquareElements_UndoRedo`
- **UI-ARRANGING-025** — When the user chooses Edit → Cut, the editor shall copy the selection and then delete it, enabled exactly when Delete is; Cut has no hotkey.
  - Tests: untested

### Resize

- **UI-ARRANGING-026** — While in Arrange mode on a tiled arranger, the toolbar shall show Resize Arranger, which opens a dialog prefilled with the width and height in elements.
  - Tests: manual — click `ResizeArrangerButton`.
- **UI-ARRANGING-027** — If the requested size is smaller in either dimension, then the dialog shall ask "Elements outside of the new arranger dimensions will be lost. Continue?" and resize only on Yes.
  - Tests: untested
- **UI-ARRANGING-028** — When the resize is accepted, the editor shall resize the working arranger, rebuild the image, clear the selection, record history and mark the editor modified.
  - Tests: `GraphicsEditHistoryTests.Resize_UndoRedo`

### New scattered arranger from selection

- **UI-ARRANGING-029** — While a selection exists and the editor's resource is in a project, "Add as New Scattered Arranger..." shall be enabled (standalone gating in UI-GRAPHICS-EDITOR).
  - Tests: untested
- **UI-ARRANGING-030** — When the user chooses it, the app shall ask for a name, create a scattered arranger holding the selected whole elements, add it at the project root, select its node and open it in an editor; a failed copy or add shows "Error" with the reason.
  - Tests: manual — select elements, right-click, Add as New Scattered Arranger....

### Palettes

- **UI-ARRANGING-031** — While Apply Palette is active on an indexed arranger, left-clicking or dragging shall assign the selected palette to each element under the pointer, or to every element in the selection when the click is inside it, and mark the editor modified.
  - Tests: `GraphicsEditHistoryTests.ApplyPalette_UndoRedo`
- **UI-ARRANGING-032** — If an element is empty, not indexed, already uses the palette, or holds a pixel index beyond the palette's size, then Apply Palette shall skip it and outline nothing there.
  - Tests: untested
- **UI-ARRANGING-033** — When an Apply Palette press-and-drag changes at least one element, the editor shall record it as one history entry when the button is released or the tool is switched.
  - Tests: untested
- **UI-ARRANGING-034** — When the user clicks an indexed element with Pick Palette, the editor shall select that element's palette in the palette combo, or the default palette if the element's palette is not listed.
  - Tests: untested
- **UI-ARRANGING-035** — While Pick Palette hovers an element, the status bar shall show the palette's name, color count, color model and source (file path, Memory, Global or None).
  - Tests: untested
- **UI-ARRANGING-036** — While in Arrange mode, holding Ctrl or Shift (with the editor focused) or Alt (on pointer events) shall temporarily switch to Pick Palette, and the toolbar shall show Pick Palette as active while Ctrl or Shift is held.
  - Tests: manual — hold each modifier over the canvas.
- **UI-ARRANGING-037** — The editor's palette combo shall list the arranger's referenced project palettes by name, then the global palettes by name, each limited to the arranger's largest codec color count (at most 256).
  - Tests: untested
- **UI-ARRANGING-038** — When the user clicks Associate Palette (Arrange mode, indexed), the editor shall list the containing project's palettes by path and the global palettes by name, and on Associate add the chosen palette to the palette combo without changing any element.
  - Tests: untested

### Symmetry tools

- **UI-ARRANGING-039** — When the user toggles "Show Symmetry Tools" in the canvas context menu, the editor shall show or hide Rotate Left, Rotate Right, Mirror Horizontal and Mirror Vertical and save the choice to user preferences; new editors start from the saved choice.
  - Tests: untested
- **UI-ARRANGING-040** — When the user clicks an element with a mirror tool, the editor shall flip how that element is displayed, record history and mark the editor modified.
  - Tests: `GraphicsEditHistoryTests.Mirror_UndoRedo`
- **UI-ARRANGING-041** — When the user clicks a square element with a rotate tool, the editor shall rotate how it is displayed, record history and mark the editor modified; a non-square element is left unchanged and not outlined.
  - Tests: `GraphicsEditHistoryTests.Rotate_UndoRedo`

### Defaults

- **UI-ARRANGING-042** (inherited) — Selection handles shall be 8 screen pixels square at any zoom.
  - Tests: untested
- **UI-ARRANGING-043** (inherited) — A selection drag shall start once the pointer moves more than 3 pixels from the press.
  - Tests: untested
- **UI-ARRANGING-044** (inherited) — When no preference is saved, the symmetry tools shall be hidden.
  - Tests: untested

## Invariants

- At most one floating paste exists per editor; a new paste, selection reset, mode change or apply clears the previous one.
- The clipboard holds one copy for the whole app and is never written to the OS clipboard.
- Element copies carry the project resource they came from; pixel copies do not.

## Edge cases

- A paste dragged partly above or left of the arranger is cropped, not shifted.
- Dragging a 1x1 element copy anchors the paste's top-left at the pointer; larger copies keep the grab offset.
- Pixel-snapped selections give "Add as New Scattered Arranger..." only the whole elements their snapped bounds divide into.

## Threading and lifetime

- UI thread only. The clipboard is a static field and lives for the process.
- Drag payloads go through `DragPayloadStore` keyed by a string in the drag data and are removed when the drag ends.

## Decisions

- **Delete is mode-guarded.** Delete runs only in Arrange mode on a scattered arranger with an element-snapped selection; it once fired in Draw and View and on sequential arrangers. Reason: tools act only in the mode they belong to.
- **Cut is a menu item only.** Cut is Copy plus element Delete, so it is enabled only where Delete is, and it has no Ctrl+X hotkey or displayed gesture. Reason: avoid a hotkey that silently does nothing outside Arrange mode. Rejected: pixel cut.
- **Delete resets by element width and height.** Delete divides the selection by element width for x and element height for y; the old code swapped them, breaking non-square elements.
- **Symmetry tools are opt-in.** Mirror and rotate change only how an element is displayed, so they stay hidden until enabled from the context menu. Rejected: pixel-level flip and rotate (a P2 backlog item).
- **No cross-project element paste.** Elements reference a project's data files and palettes, so pasting them into another project is refused.
- **Private clipboard.** The clipboard is app-internal. Rejected for 1.0: OS clipboard exchange (a P2 backlog item, planned after 1.0).

## Non-goals

- Magic wand, lasso and pixel-level flip or rotate of a selection.
- Copying elements across projects.

## Open items

- Ctrl+click single-cell select and Shift+hover single-element select are shadowed while the editor has keyboard focus: pressing Ctrl or Shift engages the temporary Pick Palette tool (Color Picker in Draw mode), which then receives the click, and Shift+hover is skipped whenever a temporary tool is engaged.
- A dropped pixel paste can be applied in View or Arrange mode (Enter or Shift-drop), writing pixels without the Draw-mode switch that Ctrl+V performs; an element drop onto a sequential arranger is applied as pixels.
- Apply Palette assigns the palette on the codec instance the working arranger shares with the project arranger, so the change reaches the project arranger before save and survives Discard. Found by reading; not reproduced.
- The resize dialog does not validate its text boxes (zero or negative sizes are passed to `Resize`), and accepting an unchanged size still records history and marks the editor modified.
- Delete records history and marks the editor modified even when every covered element is already empty.
- Associate Palette adds the palette again if it is already listed, sizes it by the palette's entry count rather than the codec's color depth, and throws if no palette is available.
- Selection handles are hit-tested in View mode although they are not drawn there.
- Hiding the symmetry tools leaves a selected symmetry tool active.
- "Add as New Scattered Arranger..." always adds at the project root, not beside the source resource.
- `ElementCopierTests` is entirely commented out; no test covers element paste cropping or the cross-project refusal.
