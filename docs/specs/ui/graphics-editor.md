---
id: UI-GRAPHICS-EDITOR
title: Graphics editor
project: TileShop.UI
sources:
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.View.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Input.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorView.axaml
  - TileShop.UI/Features/Graphics/GraphicsEditorView.axaml.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorToolbarView.axaml
  - TileShop.UI/Features/Graphics/GraphicsEditorToolbarView.axaml.cs
  - TileShop.UI/Features/Graphics/ArrangerImageAdapter.cs
  - TileShop.UI/Features/Graphics/EditorPaletteList.cs
  - TileShop.UI/Features/Graphics/ArrangerSkiaBitmap.cs
  - TileShop.UI/Features/Graphics/Tools/InspectElementToolHandler.cs
  - TileShop.UI/Features/Renderer/ArrangerRenderer.cs
  - TileShop.UI/Features/Renderer/CheckerboardPaint.cs
  - TileShop.UI/Features/Dialogs/JumpToOffsetViewModel.cs
  - TileShop.UI/Features/Dialogs/JumpToOffsetView.axaml
  - TileShop.UI/Features/Dialogs/JumpToOffsetView.axaml.cs
  - TileShop.UI/Features/Dialogs/CustomElementLayoutViewModel.cs
  - TileShop.UI/Features/Dialogs/CustomElementLayoutView.axaml
  - TileShop.UI/Features/Dialogs/ModifyGridSettingsViewModel.cs
  - TileShop.UI/Features/Dialogs/ModifyGridSettingsView.axaml
  - TileShop.UI/Models/GridSettingsViewModel.cs
  - TileShop.UI/Models/GridSettingsSnapshot.cs
  - TileShop.UI/ViewExtenders/Input/InputAdapter.cs
  - TileShop.UI/ViewExtenders/Input/ToolCursors.cs
  - TileShop.UI.Controls/InfiniteCanvas
  - TileShop.Shared/Tools
  - TileShop.Shared/Input
types:
  - GraphicsEditorViewModel
  - GraphicsEditorView
  - GraphicsEditorToolbarView
  - GraphicsEditMode
  - ViewTool
  - ArrangerImageAdapter
  - EditorPaletteList
  - ArrangerSkiaBitmap
  - ArrangerRenderer
  - CheckerboardPaint
  - GridSettingsViewModel
  - GridSettingsSnapshot
  - ModifyGridSettingsViewModel
  - JumpToOffsetViewModel
  - CustomElementLayoutViewModel
  - InspectElementToolHandler
  - IToolHandler
  - ToolContext
  - ToolResult
  - ToolInputRouter
  - ToolCursor
  - InvalidationLevel
  - IStateDriver
  - InputAdapter
tests:
  - SaveConflictTests
  - ToolInputRouterTests
  - GraphicsEditorPaletteTests
  - ArrangerSkiaBitmapTests
depends:
  - LIB-ARRANGERS
  - LIB-IMAGES
  - LIB-CODECS
  - LIB-DATASOURCE
  - LIB-PROJECT-SERVICE
  - LIB-PROJECT-TREE
  - UI-EDITORS
  - UI-SHELL
  - UI-ARRANGING
  - UI-DRAWING
  - UI-EDIT-HISTORY
---

# Graphics editor

## Purpose

The graphics editor is the document tab that shows one arranger (LIB-ARRANGERS) on a zoomable canvas and hosts three modes: View (browse a data file as a sequential arranger), Arrange (compose a scattered arranger) and Draw (edit pixels). This spec owns the editor shell: modes, canvas, sequential browsing, status text, save/discard/reload, read-only and project gating, and the commands it exposes to the Edit menu. Selection and element tools are in UI-ARRANGING, pixel tools in UI-DRAWING, undo/redo in UI-EDIT-HISTORY, import/export in UI-IMAGE-IO. Opening, closing and tab management are in UI-EDITORS; the hotkey dispatcher and Edit menu are in UI-SHELL.

## Requirements

### Modes

- **UI-GRAPHICS-EDITOR-001** — When an editor opens on a sequential arranger, it shall start in View mode and offer View and (if drawable) Draw; when it opens on a scattered arranger, it shall start in Arrange mode and offer Arrange and (if drawable) Draw.
  - Tests: manual — open a data file node and a scattered arranger node; check the toolbar mode buttons.
- **UI-GRAPHICS-EDITOR-002** — While the arranger is read-only (LIB-ARRANGERS-009), the editor shall hide the Draw mode button and refuse to switch to Draw.
  - Tests: manual — set the read-only attribute on a data file, open an arranger on it in DevTools, and check that no Draw RadioButton is visible.
- **UI-GRAPHICS-EDITOR-047** — While the arranger is read-only, the editor toolbar shall show "Read only: <reason>" (LIB-ARRANGERS-049).
  - Tests: manual — DevTools: `ReadOnlyText` reads "Read only: reads data file '…', which is read-only" on an arranger over a read-only data file.
- **UI-GRAPHICS-EDITOR-003** — When the user picks another mode while the editor has unsaved changes, the editor shall prompt "Save Changes" (Yes/No/Cancel); Yes saves, No discards, Cancel keeps the current mode, and the mode changes only when the editor is no longer modified afterwards.
  - Tests: manual — draw a pixel, click Arrange/View, try each answer.
- **UI-GRAPHICS-EDITOR-004** — When the mode changes, the editor shall deactivate the outgoing tool (recording any history it returns), drop any temporary modifier tool, and clear the selection and floating paste.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-005** — When the editor enters Draw mode with a selection, it shall turn that selection into an active draw clip; entering any mode without a selection, or leaving Draw mode, shall remove the draw clip.
  - Tests: untested

### Canvas

- **UI-GRAPHICS-EDITOR-006** (inherited) — The canvas zoom shall stay between 0.25x and 32x when zoomed with Ctrl+mouse wheel, each notch multiplying or dividing the zoom by 1.25 while keeping the pan offset.
  - Tests: manual — Ctrl+wheel over the canvas at both limits.
- **UI-GRAPHICS-EDITOR-007** — While the middle mouse button is held over the canvas, dragging shall pan it.
  - Tests: manual — middle-drag in the editor canvas.
- **UI-GRAPHICS-EDITOR-008** — The editor shall center the image on Ctrl+E, fit it to the viewport on Ctrl+W (within the 0.25x–32x zoom limits), reset the zoom to 1x on Ctrl+R, and align the image to the top-left on Ctrl+Q.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-009** — The canvas shall draw a checkerboard behind the image, using the grid's spacing, origin and primary/secondary colors.
  - Tests: manual — open an arranger with transparent pixels.
- **UI-GRAPHICS-EDITOR-010** — When the user presses G or clicks the gridline toggle, the editor shall show or hide gridlines.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-046** (inherited) — A new editor shall start with gridlines hidden, grid spacing at the element size (8x8 for single-image arrangers) and origin (0, 0); only grid colors carry over from preferences.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-011** — When the user presses Ctrl+G or clicks the grid settings button, the editor shall open Grid Settings, previewing spacing, origin and colors live while valid; Cancel restores the previous settings, OK keeps them and saves the three colors to user preferences.
  - Tests: manual — Ctrl+G, edit values, cancel and accept.
- **UI-GRAPHICS-EDITOR-012** — Grid Settings shall refuse OK while width or height spacing is below 1; Reset Spacing restores the element size (8x8 for single-image arrangers) with origin (0, 0), and Reset Colors restores the default colors.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-013** (inherited) — The default grid line color shall be #C4CC8484, and the checkerboard colors #FFC0C0C0 and #FF808080.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-014** — When the user presses S or clicks the snap toggle, the editor shall switch selection snapping between element and pixel; the toggle is shown and works only for tiled arrangers while the active tool is not Element Select.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-015** — While the pointer is over the image, the cursor shall show the active tool's cursor, a "not allowed" cursor when a crosshair tool has nothing to act on, or a resize cursor over a selection handle.
  - Tests: manual — hover tools over empty and filled elements.
- **UI-GRAPHICS-EDITOR-016** — While the pointer is over the image, the editor shall outline the region the active tool would act on.
  - Tests: manual — hover Pencil, Apply Palette and Mirror over the canvas.
- **UI-GRAPHICS-EDITOR-052** — The canvas shall repaint at most once per frame, and a Pencil stroke on a 768x512 indexed arranger at fit zoom shall repaint within 16 ms in a Release build.
  - Tests: manual — with `ShowFrameTimings` on, draw on the CT BOSSX TIM arranger at fit zoom in a Release build and read the frame time.

### Sequential browsing

- **UI-GRAPHICS-EDITOR-017** — While the editor shows a sequential arranger in View mode, + and − (numeric keypad) shall move by one byte, Up/Down by one element row, Left/Right by one element column, PageUp/PageDown by one page, and Home/End to the start or end of the file; in Draw mode the navigation keys do nothing.
  - Tests: manual — DevTools: open the FF2 data file, switch to Draw, `input KeyDown` Down, PageDown and J; `FileOffset` is unchanged and no dialog opens. Switch to View and check the same keys move.
- **UI-GRAPHICS-EDITOR-018** — While in View mode, the mouse wheel shall move one page down or up.
  - Tests: manual — wheel over a sequential editor.
- **UI-GRAPHICS-EDITOR-019** — While in View mode, the editor shall show a file-offset scrollbar whose position is the current offset, whose large change is one page, and whose maximum is the file size less one page.
  - Tests: manual — drag the scrollbar in a sequential editor.
- **UI-GRAPHICS-EDITOR-020** — While the arranger is sequential, the toolbar shall show the current offset as "Offset: 0x…"; clicking it or pressing J shall open Jump to Offset while in View mode.
  - Tests: manual — covered by the UI-GRAPHICS-EDITOR-017 DevTools check.
- **UI-GRAPHICS-EDITOR-021** — Jump to Offset shall accept a non-negative hexadecimal (optional 0x prefix) or decimal offset, show an inline error for invalid text, disable Jump while the text does not parse, toggle the base with H or the switch (converting the current value), and filter typed characters to digits of the base.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-022** — When the user accepts Jump to Offset, the editor shall move to that offset (clamped by LIB-ARRANGERS to the last full page) and save the chosen base to user preferences; the dialog opens in the last saved base.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-045** (inherited) — When no base has been saved, Jump to Offset shall open in hexadecimal.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-023** — While in View mode, the toolbar shall offer a Codec combo listing every supported codec; choosing one shall switch the arranger's codec at the current offset, reset the grid spacing, keep the palette list and the chosen palette, and switch snapping to element (tiled codec) or pixel and hide gridlines (single-image codec).
  - Tests: untested
- **UI-GRAPHICS-EDITOR-024** — While in View mode with a tiled codec, the toolbar shall show element pixel width/height boxes, enabled only when the codec can resize, stepping by the codec's resize increments and snapping each value to the codec's preferred size.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-025** — While in View mode, the toolbar shall show the arranger size in elements (tiled) or pixels (single-image); a tiled width or height is rounded down to a multiple of the element layout's width or height, never below one layout.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-026** — While in View mode on a sequential arranger, / and . shall expand and shrink the width, and ; and L shall expand and shrink the height, by one layout (tiled) or one codec increment (single-image), never shrinking below one increment; the size boxes' tooltips name these keys.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-027** — While in View mode with a tiled codec, the toolbar shall offer an Element Layout combo of the shipped layouts, sorted by tiles per pattern then name, below the arranger size; choosing one shall reorder elements by that layout and set the size increments to its width and height.
  - Tests: manual — pick 2x2 and 4x4 V on a sequential editor.
- **UI-GRAPHICS-EDITOR-028** — When the user clicks the plus button beside Element Layout, the editor shall open the custom layout dialog (flow Horizontal/Vertical, width and height 1–64 elements, default 2x2 horizontal) and on Create add "Custom WxH H|V" to that editor's layout list, reusing an equivalent existing layout, and select it.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-048** — While the arranger is sequential and indexed, the palette combo shall list the global palettes in settings order, leaving out any palette with fewer entries than the codec's color depth needs. The Associate Palette button shall not show in a sequential editor.
  - Tests: `GraphicsEditorPaletteTests.Sequential_List_GlobalsInSettingsOrder`, `GraphicsEditorPaletteTests.Sequential_List_ExcludesPalettesTooSmallForCodec`
- **UI-GRAPHICS-EDITOR-049** — When a palette is chosen in a sequential editor, the editor shall recolor the whole view with it, in View or Draw mode, keeping the pixel indices and pending Draw edits, without recording history or marking the editor modified, and without recoloring elements already copied out of the editor.
  - Tests: `GraphicsEditorPaletteTests.Sequential_ChoosePalette_RecolorsKeepingIndices`, `GraphicsEditorPaletteTests.Sequential_ChoosePalette_LeavesCopiedElementsUnchanged`; manual — DevTools: open the FF2 data file, set the palette combo's `SelectedIndex` in View and in Draw mode; the canvas recolors, the tab shows no `*` and Undo stays disabled.
- **UI-GRAPHICS-EDITOR-050** — When the codec, element layout, size or offset changes, a sequential editor shall keep the chosen palette while the codec is indexed and the palette covers its color depth; otherwise it shall switch to the codec's default palette.
  - Tests: `GraphicsEditorPaletteTests.Sequential_ChangeCodec_KeepsPalette`, `GraphicsEditorPaletteTests.Sequential_ChangeCodec_FallsBackWhenPaletteTooSmall`; manual — DevTools: choose a palette, then change the Codec combo; the palette combo and the canvas colors are unchanged.
- **UI-GRAPHICS-EDITOR-051** — When a sequential editor opens, it shall start on the default palette.
  - Tests: `GraphicsEditorPaletteTests.Sequential_Opens_OnDefaultPalette`

### Status and inspection

- **UI-GRAPHICS-EDITOR-029** — While the pointer moves over the image with a selection, View/Draw tools shall show the selection's size and position in the status bar (element units for element snapping, pixels otherwise); without a selection, "<arranger>: (x, y)".
  - Tests: untested
- **UI-GRAPHICS-EDITOR-030** — While Inspect Element or Element Select (Arrange mode) hovers an element, the status bar shall show its position, codec, palette (indexed only), source file path or "Memory", and file offset as `0x` and the hex byte offset, followed by `.` and the bit offset only when it is not 0; an empty cell shows "Element (x, y): Empty".
  - Tests: manual — hover elements with the Inspect Element tool.
- **UI-GRAPHICS-EDITOR-031** — While a floating paste exists, the status bar shall show "Press [Enter] to Apply Paste or [Esc] to Cancel".
  - Tests: untested

### Save, discard and reload

- **UI-GRAPHICS-EDITOR-032** — When the user presses Ctrl+S, the editor shall write its pixels to the data sources (LIB-IMAGES) unless the arranger is read-only, then make the project arranger match the working arranger's size and every cell (LIB-ARRANGERS) and save that resource (LIB-PROJECT-SERVICE); success clears history and the modified state.
  - Tests: manual — DevTools: on "Adult Rydia Map" in Arrange mode, `ResizeArrangerButton`, grow by one column, Ctrl+S, close the tab, reload the project, reopen and check `WorkingArranger.ArrangerElementSize` with no Save Error; repeat shrinking. Library: `ScatteredArrangerTests.ReplaceElements_Larger_TakesSizeAndCells`
- **UI-GRAPHICS-EDITOR-033** — If saving a scattered arranger finds elements that share source data but hold different pixels, then the editor shall ask "Save Conflicts" (OK/Cancel) naming the count, and save only on OK.
  - Tests: manual — duplicate an element, edit one copy, Ctrl+S. Conflict analysis: `SaveConflictTests.AnalyzeSaveConflicts_UnchangedDuplicateOfModifiedTile_IsConflict`
- **UI-GRAPHICS-EDITOR-034** — While the arranger is read-only, saving shall skip the pixel write and still save element rearrangements.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-035** — If the resource save fails, then the editor shall show "Project Error" with the reason and stay modified; if saving throws, it shall show "Save Error" with the exception message, without a stack trace, and log the exception.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-036** — When the editor's resource is not in an open project (a sequential editor, or a standalone file), saving shall write the pixels and clear the modified state without a project save.
  - Tests: manual — save a sequential editor from a standalone file and from a project data file, in Draw mode.
- **UI-GRAPHICS-EDITOR-037** — When changes are discarded, a scattered editor shall return to the arranger as last saved and a sequential editor shall re-read its pixels, clearing history and the modified state.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-038** — When another editor or an import writes data this editor reads, and this editor is unmodified, it shall re-read its pixels and clear its history (routing in UI-EDITORS).
  - Tests: manual — open two editors on one file, save in one, check the other.

### Gating

- **UI-GRAPHICS-EDITOR-039** — While the editor's file is a standalone file (not in a project), "Add as New Scattered Arranger..." shall be disabled with the tooltip "Requires a project. Right-click the file in the project tree and choose Create Project from File...".
  - Tests: manual — open a ROM with Open File..., select elements, right-click the canvas.
- **UI-GRAPHICS-EDITOR-040** — While the editor's file is a standalone file, the palette association list shall offer only global palettes.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-041** — When the user associates a palette that reads from a missing data file, the editor shall alert "Associate Palette" with a hint to Relink... and not add it.
  - Tests: untested

### Edit menu and hotkeys

- **UI-GRAPHICS-EDITOR-042** — The editor shall expose to the Edit menu the same Undo, Redo, Cut, Copy, Paste, Delete and Select All command instances its hotkeys and key bindings run, so menu enablement matches hotkey enablement.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-043** — The editor shall register as window-wide hotkeys G, S, J, Ctrl+S, Ctrl+Z, Ctrl+Y, Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+G, Ctrl+E, Ctrl+W, Ctrl+R, Ctrl+Q, /, ., ; and L, and keep Delete, Escape, Enter and the navigation keys as bindings that work only while the editor has keyboard focus.
  - Tests: untested
- **UI-GRAPHICS-EDITOR-044** — The canvas context menu shall offer Add as New Scattered Arranger..., Import Image Into Selection... (scattered only), Select All, Grid Settings... and Show Symmetry Tools (checked when enabled).
  - Tests: manual — right-click the canvas in Arrange mode.

## Invariants

- `WorkingArranger` is a clone of the project arranger for scattered arrangers and the arranger itself for sequential ones; the project arranger changes only on save.
- While in View mode the editor holds no pending edits.
- `ReadOnlyReason` equals the working arranger's read-only reason and `CanDraw` equals "`ReadOnlyReason` is null" after every image rebuild.
- `IsViewMode`, `IsArrangerMode` and `IsDrawMode` are mutually exclusive and follow `EditMode`.

## Edge cases

- A file smaller than one page keeps the arranger at offset 0 (LIB-ARRANGERS); the scrollbar maximum can then be negative.
- Pointer events outside the image clear the hover outline and the last pointer position; key-driven tools then do nothing.
- A canvas repaint after the view's `DataContext` cleared is skipped.

## Threading and lifetime

- All editor work runs on the UI thread. Dialogs and prompts are awaited through `IInteractionService`.
- The view assigns the VM's `OnImageModified`, `OnCenterContent`, `OnFitToViewport`, `OnResetZoom` and `OnAlignTopLeft` callbacks when it receives the VM, and clears them when its `DataContext` moves off that VM unless another view (a floated or re-docked tab) has since installed its own.
- `EditorsViewModel` (UI-EDITORS) owns the editor's lifetime and calls `ReloadFromSource` and `InvalidateEditor` on domain change events.

## Decisions

- **Expand/Shrink are hotkeys, not buttons.** `ExpandWidth`/`ShrinkWidth`/`ExpandHeight`/`ShrinkHeight` were wired back to their pre-Avalonia keys (/ . ; L), enabled only for sequential arrangers in View mode, and the size boxes' tooltips name them. Reason: they complete the size boxes. Rejected: deleting them.
- **Element layout lasts for the session.** The layout combo and custom layout dialog change only the open editor; nothing is persisted. Reason: a data file is browsed with many layouts, so one stored layout on the data file does not fit, and sequential arrangers are not project resources. Rejected: persisting on the data file node. Persistence returns with sequential arrangers as resources.
- **Save conflicts prompt.** The former empty `SaveConflictsDetectedMessage` handler was replaced by an OK/Cancel prompt before writing. Reason: never overwrite silently. Rejected: per-element conflict resolution UI.
- **Read-only codecs still save arrangement.** A read-only arranger hides Draw and skips the pixel write, but element moves, mirror, rotate and palette changes still save. Reason: the arrangement is project XML, not ROM data.
- **Window-wide hotkeys vs. focused key bindings.** Keys that act on the focused canvas (Delete, Escape, Enter, navigation) stay as view key bindings; everything else is dispatched window-wide by the hotkey service while the editor is active. Reason: hotkeys must work without the canvas having focus, but plain navigation keys must not steal input from other panes.
- **Project-only actions are disabled, not hidden, for standalone files.** "Add as New Scattered Arranger..." stays visible with a tooltip suggesting Create Project from File...; palette association lists only global palettes instead of being disabled. Reason: discoverability of the conversion path.
- **Navigation is View-only.** The move commands, file offset changes and Jump to Offset run only for a sequential arranger in View mode, re-evaluated on every mode change like Expand/Shrink. Reason: leaving Draw mode already prompts Save/Discard, so View mode never holds pending edits and moving cannot lose them. Rejected: prompting on each navigation key (keys repeat while browsing), and allowing navigation in Draw mode while unmodified (the first Pencil stroke would then pin the offset with no visible reason).
- **A palette switch in a sequential editor is a view setting, not an edit.** A sequential arranger has one palette for every element (LIB-ARRANGERS), and the data file holds indices, so recoloring changes nothing that Save writes. The editor redraws at `InvalidationLevel.Display`, which recolors from the stored indices without re-reading the file, so pending Draw edits survive. Reason: browsing means switching often, and history steps or a modified marker would make the editor prompt to save after every look. Rejected: recording it as history (prompts on close for an unchanged file); allowing it only in View mode (Draw mode would show colors the user can't change); redrawing at `PixelData` (re-reads the file and drops pending Draw edits).
- **Global palettes in settings order.** In a sequential editor the globals follow `appsettings.json`, which is curated: the default first, then the alternates. Alphabetical sorting would put `Classic` first, and the default should be first in the list. The list and selection logic lives in `EditorPaletteList`, a plain class, because the ViewModel cannot be built in tests (ARCHITECTURE §5). Scattered editors keep their list (UI-ARRANGING). Rejected: alphabetical (the previous behavior).
- **No Associate Palette in sequential editors.** A sequential arranger uses one palette for every element, chosen from the combo, so associating a project palette per element has no meaning there. Rejected: listing associated project palettes before the globals (a sequential editor is all or nothing, and the button was only reachable from Arrange mode anyway).
- **A sequential palette switch gives the arranger a new codec instead of mutating the shared one.** Reason: elements copied out of a sequential editor (Add as New Scattered Arranger, Copy/Paste) keep the editor's codec instance, so changing its palette in place would recolor those arrangers. Rejected: cloning codecs in `ElementCopier`, which touches every copy path for a problem only this switch creates.
- **A measured target, checked by hand.** On the TIM test arranger at fit zoom in a Release build, a Pencil stroke repaints at display rate: frame time at most 16 ms with `ShowFrameTimings`, which draws in screen space at the canvas's top-left. Rejected: a BenchmarkDotNet gate on the render path (it needs a UI thread and compositor to be meaningful).
- **The editor image is an `SKBitmap` drawn directly.** The editor keeps its image as an `ArrangerSkiaBitmap`, which the renderer wraps once per frame with `SKImage.FromPixels` over `PeekPixels`, with no lock and no pixel copy. Rejected: `DrawBitmap`, which copies a mutable bitmap's pixels into a new `SKImage` on every call. A Greyscale draw clip or selection draws the image once in greyscale outside the clip (or the selection when there is no clip) and once in color inside it. Partial updates translate only the changed rows, serially below 32 rows. Rejected: a `WriteableBitmap` locked and wrapped with `FromPixels` every frame, drawn up to three times in Greyscale mode. The paste overlay is an `ArrangerSkiaBitmap` too, drawn by the same lock-free wrap. Indexed pixels are translated in runs that end at the next element boundary (measured from the image's `Left`, since pixel copies start mid-element), with one element lookup, `IsWithinSource` check and palette fetch per run instead of per pixel.
- **The canvas stays a software surface.** `InfiniteCanvas` still renders into a viewport-sized `WriteableBitmap` through a CPU `SKSurface`. Once Pencil interpolated its strokes, per-pixel overhead was gone and full redraws were cheap, and Pencil, flood fill and undo met the frame target on the 768x512 TIM. Rejected for now: a custom draw operation leasing Avalonia's Skia canvas through `ISkiaSharpApiLeaseFeature` (it changes the canvas shared with the import preview for no measured gain); rendering only dirty rectangles (zoom, pan and overlays invalidate almost everything). Revisit if a larger arranger or a higher-DPI viewport misses the target.
- **Keep `IndexedBitmapAdapter` for the welcome banner.** An Avalonia `<Image>` needs an Avalonia bitmap, and the banner renders once, so converting it buys nothing. Rejected: an `SKBitmap`-to-Avalonia-bitmap converter for one caller.
- **Sequential paths do not assume a project.** Save and palette association use the non-throwing `FindContainingProject` and skip the project save when it returns null. Reason: a sequential arranger is never in a tree.

## Non-goals

- Persisting codec, offset, palette or layout of a sequential view between sessions.
- Bookmarks, navigation history, hex view, minimap and pattern search (backlog).
- A zoom percentage display or zoom presets.

## Open items

- Ctrl+wheel zoom keeps the pan offset rather than zooming around the pointer.
- No UI test covers any requirement here; candidates are mode switching with a fake `IInteractionService` and the gating properties.
