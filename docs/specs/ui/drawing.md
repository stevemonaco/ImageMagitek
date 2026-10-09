---
id: UI-DRAWING
title: Drawing
project: TileShop.UI
sources:
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Drawing.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Selection.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.ArrangerTools.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Input.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorToolbarView.axaml
  - TileShop.UI/Features/Graphics/GraphicsEditorToolbarView.axaml.cs
  - TileShop.UI/Features/Graphics/ColorEditorFlyoutViewModel.cs
  - TileShop.UI/Features/Graphics/Tools/PencilToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/FloodFillToolHandler.cs
  - TileShop.UI/Features/Graphics/Tools/ColorPickerToolHandler.cs
  - TileShop.UI/Features/Graphics/GraphicsEditHistory.cs
  - TileShop.UI/Features/Dialogs/ColorRemapViewModel.cs
  - TileShop.UI/Features/Dialogs/ColorRemapView.axaml
  - TileShop.UI/Features/Dialogs/ColorRemapView.axaml.cs
  - TileShop.UI/Models/PaletteModel.cs
  - TileShop.UI/Models/PaletteEntry.cs
  - TileShop.UI/Models/RemappableColorModel.cs
  - TileShop.UI/ViewExtenders/DragDrop/RemappableColorDropHandler.cs
types:
  - DrawTool
  - ColorPriority
  - DrawClipEffect
  - PencilToolHandler
  - FloodFillToolHandler
  - ColorPickerToolHandler
  - ColorEditorFlyoutViewModel
  - ColorRemapViewModel
  - RemappableColorModel
  - RemappableColorDropHandler
  - PaletteModel
  - PaletteEntry
tests:
  - GraphicsEditHistoryTests
  - IndexedImageTests
  - ToolInputRouterTests
depends:
  - LIB-IMAGES
  - LIB-PALETTES
  - LIB-COLORS
  - LIB-ARRANGERS
  - UI-GRAPHICS-EDITOR
  - UI-ARRANGING
  - UI-EDIT-HISTORY
  - UI-PALETTE-EDITOR
---

# Drawing

## Purpose

Pixel editing in the graphics editor's Draw mode (UI-GRAPHICS-EDITOR): pencil, flood fill, color picker, pixel select with pixel copy and paste, the draw clip, primary and secondary colors with the swatch grid or direct-color pickers, the palette color flyout, and the Color Remap dialog. Pixel reads and writes, flood fill and remapping are LIB-IMAGES; selection mechanics are UI-ARRANGING; history is UI-EDIT-HISTORY. Palette color edits made from the flyout are committed by the palette editor (UI-PALETTE-EDITOR).

## Requirements

### Tools

- **UI-DRAWING-001** — While in Draw mode, the toolbar shall offer Pixel Select, Pencil, Color Picker and Flood Fill, with Pencil selected in a new editor; switching tools shall finish the outgoing tool first.
  - Tests: manual — switch tools mid-session in Draw mode.
- **UI-DRAWING-002** — When the user presses the left or right button with Pencil, the editor shall start a stroke in the primary or secondary color and paint every pixel the pointer crosses while the button is held; releasing all buttons ends the stroke.
  - Tests: manual — draw with both buttons.
- **UI-DRAWING-003** — While the arranger is indexed, Pencil shall write the chosen index of the active palette (LIB-IMAGES-032): the index itself into elements on the active palette, otherwise the first index of the element's palette with the same color; if the target element's palette lacks the color or maps it past the codec's range, then the pixel shall be left unchanged and the status bar shall show the reason.
  - Tests: `IndexedImageTests.PaintIndex_DuplicateColor_WritesChosenIndex`, `IndexedImageTests.PaintIndex_OtherPalette_WritesExactColorIndex`; manual — on an arranger whose palette repeats a color, pick the higher duplicate index, draw, save, reopen and check the stored index with Color Picker.
- **UI-DRAWING-004** — While the arranger is direct-color, Pencil shall paint the primary or secondary RGBA color.
  - Tests: `GraphicsEditHistoryTests.Pencil_Direct_UndoRedo`
- **UI-DRAWING-005** — When a stroke ends having changed at least one pixel, the editor shall record it as one history entry and mark the editor modified; a stroke that changes nothing records nothing.
  - Tests: `GraphicsEditHistoryTests.Pencil_Indexed_UndoRedo`
- **UI-DRAWING-006** — When the user clicks with Flood Fill, the editor shall fill the contiguous region of the clicked pixel's value with the primary (left) or secondary (right) color, limited to the active draw clip, and record history and mark the editor modified only when a pixel changed.
  - Tests: `GraphicsEditHistoryTests.FloodFill_IndexedWithClip_UndoRedo`, `GraphicsEditHistoryTests.FloodFill_DirectWithClip_UndoRedo`
- **UI-DRAWING-007** — When the user clicks with Color Picker, the editor shall set the primary (left) or secondary (right) color from the pixel; on an indexed arranger it shall also make the element's palette active, adding it to the palette list when it is not listed, and take its pixel index.
  - Tests: manual — copy elements from "Portraits" into "Adult Rydia Map" choosing one whose palette is not listed there, Ctrl+click a pasted pixel in Draw mode, and check the swatches show its palette and Pencil draws; DevTools: `props` on the editor shows `ActivePalette` non-null.
- **UI-DRAWING-008** — While Color Picker hovers a pixel, the status bar shall show its position, index, color model and foreign value, and RGBA (indexed), or its RGBA (direct), with a swatch of the color; an element without color data or an empty cell is named as such.
  - Tests: manual — hover with Color Picker and check the status bar swatch.
- **UI-DRAWING-009** — While in Draw mode, holding Ctrl or Shift (with the editor focused) or Alt (on pointer events) shall temporarily switch to Color Picker, and the toolbar shall show Color Picker as active while Ctrl or Shift is held; a modifier pressed while a pointer button is held takes effect only after all buttons are released.
  - Tests: manual — hold each modifier while hovering in Draw mode.
- **UI-DRAWING-010** — While in Draw mode, right-clicking over the image shall not open the canvas context menu.
  - Tests: manual — right-click the image in Draw mode.
- **UI-DRAWING-011** — The Pencil, Flood Fill and Color Picker outline and crosshair shall appear only over pixels they can act on: inside the draw clip, in an element within its source with color data, and for Pencil only where the primary color can be set.
  - Tests: manual — hover past the end of a file and over empty cells.
- **UI-DRAWING-033** — While a pointer button is held, the editor shall send every pointer move and the release to the tool that received the press.
  - Tests: `ToolInputRouterTests.ModifierDuringPress_ReleaseGoesToPressTool`, `ToolInputRouterTests.ModifierHeldAtRelease_EngagesTemporaryToolAfter`, `ToolInputRouterTests.AltOnMoveDuringPress_StaysWithPressTool`, `ToolInputRouterTests.ModifierWithoutPress_EngagesImmediately`; manual — draw a Pencil stroke, press and release Ctrl mid-stroke, release, draw a second stroke, undo twice and check both strokes are undone; repeat with Shift.

### Draw clip

- **UI-DRAWING-012** — While in Draw mode with a selection, the toolbar shall show Set Draw Clip, which makes the selection the active draw clip and clears the selection.
  - Tests: untested
- **UI-DRAWING-013** — While a draw clip exists, the toolbar shall show a toggle that activates or suspends it, a button cycling its effect between Greyscale and Hidden, and a button that removes it.
  - Tests: untested
- **UI-DRAWING-014** — While a draw clip is active, Pencil, Flood Fill, pixel paste and Color Remap shall change no pixel outside it, and selections, Select All and dragged pastes shall stay inside it.
  - Tests: `GraphicsEditHistoryTests.PixelPaste_UndoRedo`
- **UI-DRAWING-015** — While a draw clip is active, the canvas shall draw the image outside it in greyscale (Greyscale effect) or not at all, checkerboard included (Hidden effect).
  - Tests: manual — set a clip and cycle the effect.
- **UI-DRAWING-016** (inherited) — A new draw clip shall use the Greyscale effect.
  - Tests: untested

### Pixel copy and paste

- **UI-DRAWING-017** — When the user presses Ctrl+V outside the element-paste case of UI-ARRANGING, the editor shall switch to Draw mode (with the mode-switch save prompt), then float the clipboard as a pixel paste at the draw clip's top-left or (0, 0); if Draw mode cannot be entered, nothing happens.
  - Tests: untested
- **UI-DRAWING-018** — When a pixel paste is applied, the editor shall copy pixels clipped to the arranger and the draw clip: indexed into indexed by exact index, falling back to exact palette color matching; direct into indexed by exact palette color; anything into direct by color. Copied elements paste as their pixels.
  - Tests: `GraphicsEditHistoryTests.PixelPaste_UndoRedo`
- **UI-DRAWING-019** — If the target editor cannot draw, then applying a pixel paste shall fail with the status "Arranger is read-only"; any other copy failure shows its reason in the status bar and changes nothing.
  - Tests: untested

### Colors

- **UI-DRAWING-020** — While in Draw mode on an indexed arranger, the toolbar shall show primary and secondary swatches filled from the active palette and a swatch grid of the active palette's colors with the primary and secondary entries bordered.
  - Tests: manual — inspect `PaletteSwatchGrid` in Draw mode.
- **UI-DRAWING-021** — When the user left-clicks or right-clicks a swatch, the editor shall set the primary or secondary index; the swatch grid opens no context menu.
  - Tests: manual — click swatches with both buttons.
- **UI-DRAWING-022** — When the user selects a palette in the palette combo, it shall become the active palette for the swatches and Pencil.
  - Tests: untested
- **UI-DRAWING-023** (inherited) — A new editor shall start with primary index 0, secondary index 1, primary color opaque white and secondary color opaque black.
  - Tests: untested
- **UI-DRAWING-024** — While in Draw mode on a direct-color arranger, the toolbar shall show two color pickers bound to the primary and secondary colors.
  - Tests: manual — open a direct-color arranger in Draw mode.
- **UI-DRAWING-025** — While in Draw mode on an indexed arranger whose active palette is not read-only (LIB-PALETTES-049), the edit-color button shall open a flyout editing the active palette's color at the primary index; read-only palettes disable the button.
  - Tests: manual — the flyout is a popup.
- **UI-DRAWING-026** — When the user saves a color in the flyout, the app shall route it to that palette's editor, opening it in the background, so the edit is pending there and previews live in graphics editors (UI-PALETTE-EDITOR).
  - Tests: manual — edit a color from the flyout, check the palette editor tab is modified.

### Color Remap

- **UI-DRAWING-027** — While in Draw mode on an indexed arranger, the toolbar shall show Remap Colors, enabled only when the arranger is not read-only and the remap region holds at least one element, all indexed, using at most one palette.
  - Tests: manual — `RemapColorsButton` on "Adult Rydia Map" (enabled) and "Portraits" (disabled).
- **UI-DRAWING-028** — The remap region shall be the selection intersected with the active draw clip, else the draw clip, else the whole image, and the dialog shall name it ("Applies to the current selection", "…within the draw clip", "…to the entire image").
  - Tests: untested
- **UI-DRAWING-029** — The Color Remapper shall show the region's palette (the default palette when it has none), limited to the region's largest codec color count up to 256, in rows of at most 16 with index headers.
  - Tests: untested
- **UI-DRAWING-030** — In the Color Remapper, clicking a color shall open a picker of replacements, dragging a color onto another shall remap it, right-clicking shall reset it, and Reset All (enabled while anything is remapped) shall reset every color.
  - Tests: manual — the picker is a popup and remapping by drag needs a pointer.
- **UI-DRAWING-031** — When the user accepts the Color Remapper with at least one remapped color, the editor shall replace pixel indices within the region by the remap table, record history and mark the editor modified; accepting without changes does nothing.
  - Tests: `GraphicsEditHistoryTests.ColorRemap_WithBounds_UndoRedo`
- **UI-DRAWING-032** (inherited) — Color Remapper cells shall be 48 pixels for palettes of 8 colors or fewer and 30 pixels otherwise.
  - Tests: untested

## Invariants

- Every pixel change in Draw mode happens through the editor's image adapter and is either part of a recorded history action or of an in-progress Pencil stroke.
- Draw mode is reachable only while the arranger is not read-only, so no Draw tool writes to a read-only arranger.

## Edge cases

- Painting over the same pixel twice in one stroke counts it once.
- A pixel paste whose overlap with the clip is empty succeeds and changes nothing, but is still recorded.

## Threading and lifetime

- UI thread only. A Pencil stroke's history action lives on the editor until the stroke ends, the tool is switched or the mode changes.
- The color flyout VM is rebuilt each time the button is clicked and holds the palette and index it was opened for.

## Decisions

- **Read-only arrangers cannot enter Draw mode.** The read-only check (no codec that can encode) drives `CanDraw`, pixel paste, Color Remap and Import, instead of failing only on save. Reason: a control must never appear to work and then fail.
- **Pencil ends its stroke on release whatever it changed.** The stroke state is cleared on button release even when no pixel changed; it once stayed in its drawing state.
- **Color Remap needs a single palette.** A remap table maps one palette's indices, so a region spanning several palettes disables the button instead of guessing. Rejected: one table per palette.
- **The color flyout edits through the palette editor.** Flyout edits are routed to the palette's editor as a pending edit rather than written to the ROM, so a palette has one modified state wherever it is edited (decision in UI-PALETTE-EDITOR).
- **Temporary Color Picker on a modifier.** Holding Ctrl, Shift or Alt picks colors without leaving the current tool. It never interrupts a gesture in progress: the press owns the gesture, so the tool that took the press gets every move and the release, and a modifier still held at the release engages the temporary tool then. Rejected: ending the stroke when a modifier is pressed, which splits one stroke into two history entries and still drops the release.
- **Pencil writes indices; history replays what was written.** Into an element whose palette is the active palette (by reference), Pencil writes the chosen index when it fits the codec's color depth; into any other element it maps by exact color, because the same index means a different color there. The history action stores the index or color written at each pixel and replays exactly those. Reason: palettes often repeat colors, and writing by color stored the first duplicate in the ROM while history replayed the chosen index. Rejected: always writing the chosen index into every element, which changes the visible color in multi-palette arrangers.
- **Color Picker adds an unlisted palette.** The picked element's palette is added to the palette list, sized like the editor's other palettes (largest codec color count, at most 256), and made active. Rejected: keeping the previous palette (Pencil would then map the picked index through the wrong colors), and falling back to the default palette (wrong colors). Pick Palette's default fallback (UI-ARRANGING-034) is a separate decision.

## Non-goals

- Line, rectangle, ellipse, eraser, brush size and replace-color tools; single-key tool hotkeys and `[`/`]` index stepping (backlog, P2).
- Undo history panel, layers, text tool (backlog, P3).
- A color flyout for direct-color arrangers.

## Open items

- Each painted indexed pixel sends an empty status message, clearing any status text.
- Direct-color Pencil writes without checking that the pixel lies in an element with color data; only the hover outline checks it.
- `RemapColors` has no mode guard of its own; only the button's visibility keeps it to Draw mode.
- `ActiveColor`, `ActiveColorIndex`, `SetPrimaryColorIndex`/`SetSecondaryColorIndex` and `SetPrimaryColor`/`SetSecondaryColor` are unused duplicates of the swatch commands.
- No test covers the Draw-mode wiring (clip enforcement in Pencil, Color Picker palette switching, Remap enablement); `CanRemapColors` is a good first unit test.
