---
id: UI-PALETTE-EDITOR
title: Palette editor
project: TileShop.UI, TileShop.Shared
sources:
  - TileShop.UI/Features/Palettes
  - TileShop.UI/Features/Dialogs/ChangeColorModelViewModel.cs
  - TileShop.UI/Features/Dialogs/ChangeColorModelView.axaml
  - TileShop.UI/Models/PaletteSwatchModel.cs
  - TileShop.Shared/Models/History/PaletteHistoryAction.cs
  - TileShop.Shared/Models/ColorSources
types:
  - PaletteEditorViewModel
  - PaletteEditSession
  - PaletteSelection
  - PaletteHistoryAction
  - PaletteSnapshot
  - EditableColorBaseViewModel
  - Color32ViewModel
  - TableColorViewModel
  - ChangeColorModelViewModel
  - ColorModelChange
  - PaletteSwatchModel
  - ColorSourceModel
  - FileColorSourceModel (TileShop.Shared.Models)
  - NativeColorSourceModel
  - ForeignColorSourceModel
  - ScatteredColorSourceModel
tests:
  - PaletteEditSessionTests
  - PaletteSelectionTests
  - PaletteFileSerializerTests
depends:
  - LIB-PALETTES
  - LIB-COLORS
  - LIB-PROJECT-SERVICE
  - LIB-PROJECT-FORMAT
  - UI-SHELL
  - UI-EDITORS
  - UI-EDIT-HISTORY
  - UI-DRAWING
---

# Palette editor

## Purpose

The document editor for one palette: a swatch grid beside a color editor, a collapsible list of color sources, and palette-wide operations (copy/paste, swap, gradient, color model change, `.pal`/`.gpl` files). Edits stay pending in the shared `Palette` (LIB-PALETTES) until Save, so graphics editors preview them live. Opening, tab lifetime and the save-on-close prompt are UI-EDITORS; hotkey dispatch and the clipboard are UI-SHELL; the graphics editor's color flyout (UI-DRAWING) reuses this spec's color editors and routes its edits here.

## Requirements

### Layout

- **UI-PALETTE-EDITOR-001** — When a palette opens, the editor shall show a header of "<data file name> · <color model> · <N> colors", an "Index 0 is transparent" checkbox, a toolbar, the swatch grid, the color editor and a Sources section.
  - Tests: untested
- **UI-PALETTE-EDITOR-002** — When a palette with at least one color opens, the editor shall select color 0.
  - Tests: untested
- **UI-PALETTE-EDITOR-003** — The swatch grid shall show column index headers, and row headers holding each row's first index only when there is more than one row.
  - Tests: untested
- **UI-PALETTE-EDITOR-004** — The color editor shall sit beside the swatch grid and show the selection summary ("Color N", or "Color N · K selected" for several), the active color's editor, and where the active color comes from.
  - Tests: untested
- **UI-PALETTE-EDITOR-005** — The active color's source line shall read "File offset 0x<hex>" (plus " bit <n>" when not byte-aligned), "Native color stored in the project", or "<model> color stored in the project".
  - Tests: untested
- **UI-PALETTE-EDITOR-006** — While colors are selected, every source row that supplies at least one selected color shall be highlighted.
  - Tests: untested
- **UI-PALETTE-EDITOR-007** — The Sources section shall be a collapsible section, collapsed when the editor opens.
  - Tests: untested

### Color editor

- **UI-PALETTE-EDITOR-010** — The active color shall be the most recently clicked or navigated selected color, or else the lowest selected color.
  - Tests: untested
- **UI-PALETTE-EDITOR-011** — For a component color model, the color editor shall offer R, G and B sliders and number boxes ranged to the model's channel maximums, and A only when the model has alpha.
  - Tests: untested
- **UI-PALETTE-EDITOR-012** — For a table color model (such as NES), the color editor shall show every table color in a grid with hex headers, and clicking one shall make it the working color.
  - Tests: untested
- **UI-PALETTE-EDITOR-013** — The Native box shall show the working color as `#RRGGBB` (`#RRGGBBAA` when the model has alpha); when hex text with or without `#` is committed, the editor shall approximate it in the palette's color model.
  - Tests: untested
- **UI-PALETTE-EDITOR-014** — The foreign box shall show the working color's raw value in the palette's color model, and accept a raw value.
  - Tests: untested
- **UI-PALETTE-EDITOR-015** — If committed hex text does not parse, then the box shall show "Expected #RRGGBB or #RRGGBBAA" (Native) or "Not a valid <model> color" (foreign) and keep the working color.
  - Tests: untested
- **UI-PALETTE-EDITOR-016** — The color editor shall show the stored color beside the working color.
  - Tests: untested
- **UI-PALETTE-EDITOR-017** — The Assign button shall be enabled only while the working color differs from the stored color and the palette is editable; Assign shall record the working color as a pending edit of the active index.
  - Tests: untested
- **UI-PALETTE-EDITOR-018** — While the active index and its stored color are unchanged, unassigned working edits shall survive palette refreshes; when either changes, the color editor shall reload from the palette and drop them.
  - Tests: untested

### Pending edits and Save

- **UI-PALETTE-EDITOR-020** — When an edit is made, the editor shall apply it to the shared palette at once, so every open view shows it, without writing the data file.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **UI-PALETTE-EDITOR-021** — When a project-stored color (native or foreign) is edited, the editor shall update that source's value in memory, where it stays until Save or discard.
  - Tests: `PaletteEditSessionTests.SetColor_ProjectNative_UpdatesSourceValueInMemoryUntilDiscard`, `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`
- **UI-PALETTE-EDITOR-022** — When a color of another color model is assigned to a file or foreign entry, the editor shall convert it to the palette's color model before storing it.
  - Tests: untested
- **UI-PALETTE-EDITOR-023** — While the undo history is not empty, a project save is still owed, or a source is invalid, the editor shall be marked modified.
  - Tests: `PaletteEditSessionTests.IsModified_TracksHistoryAcrossSaveUndoAndRedo`
- **UI-PALETTE-EDITOR-024** — When Save runs on an editable palette with valid sources, the editor shall write pending colors to the data file and project-stored sources, then write the palette's resource file to the project, even when no color edit was pending.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`, `PaletteEditSessionTests.SetColor_ProjectForeign_CommitKeepsValue`
- **UI-PALETTE-EDITOR-025** — If the data file cannot be written, then Save shall alert "Save Error" ("'<name>' cannot be written to its data source") and leave the editor modified.
  - Tests: untested
- **UI-PALETTE-EDITOR-026** — If writing the palette's resource file fails, then Save shall alert "Project Error" with the reason and keep the editor modified until a later Save succeeds.
  - Tests: untested
- **UI-PALETTE-EDITOR-027** — If Save throws, then the editor shall alert "Save Error" ("Could not save the palette" and the message).
  - Tests: untested
- **UI-PALETTE-EDITOR-028** — While the palette has pending edits, a whole-project save shall write the palette's last-saved state, not its pending state.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`; the editor setting the committed model is untested.
- **UI-PALETTE-EDITOR-029** — When a color is confirmed in a graphics editor's color flyout, the palette's editor (opened in the background when not open) shall record it as a pending edit, as Assign does.
  - Tests: manual — confirm a color in the Draw-mode color flyout, then check the palette editor tab is modified and Ctrl+Z reverts it.

### Undo and redo

- **UI-PALETTE-EDITOR-030** — Each edit (assign, paste, swap, gradient, source edit, import, color model change, transparency toggle) shall be one undo step, however many colors it changes.
  - Tests: `PaletteEditSessionTests.SwapColors_IsOneUndoStep`, `PaletteEditSessionTests.FillGradient_InterpolatesBetweenEnds_AsOneUndoStep`, `PaletteEditSessionTests.SetColors_PastesFromStartAndClampsToPalette_AsOneUndoStep`
- **UI-PALETTE-EDITOR-031** — When an edit is undone or redone, the editor shall restore the colors, sources, color model and index-0 transparency of the resulting step.
  - Tests: `PaletteEditSessionTests.ChangeColorModel_ThenUndo_RestoresModelAndColors`, `PaletteEditSessionTests.IsModified_TracksHistoryAcrossSaveUndoAndRedo`
- **UI-PALETTE-EDITOR-032** — When a new edit is recorded, the editor shall clear the redo history.
  - Tests: untested
- **UI-PALETTE-EDITOR-033** — When Save succeeds, the editor shall clear the undo and redo history.
  - Tests: `PaletteEditSessionTests.SetColor_UndoRedoCommit_WritesFileOnlyOnCommit`
- **UI-PALETTE-EDITOR-034** — If an edit throws part-way, then the editor shall return the palette to its state before the edit and record no step.
  - Tests: untested
- **UI-PALETTE-EDITOR-035** (inherited) — History steps shall be named "Edit color N", "Paste colors", "Swap colors A and B", "Fill gradient", "Edit sources", "Import palette", "Change color model to M", and "Make index 0 transparent" or "Make index 0 opaque".
  - Tests: untested

### Selection

- **UI-PALETTE-EDITOR-040** — When a swatch is clicked, the editor shall select only it and make it the anchor.
  - Tests: `PaletteSelectionTests.Click_SelectsOnlyThatIndex`
- **UI-PALETTE-EDITOR-041** — When a swatch is Shift+clicked, the editor shall select the range from the anchor to it, in either direction, replacing the selection.
  - Tests: `PaletteSelectionTests.ShiftClick_SelectsRangeFromAnchorInEitherDirection`; manual — Shift+click a swatch in the palette editor.
- **UI-PALETTE-EDITOR-042** — When a swatch is Ctrl+clicked (or Cmd+clicked), the editor shall toggle it in the selection and make it the anchor.
  - Tests: `PaletteSelectionTests.CtrlClick_TogglesIndices`; manual — Ctrl+click swatches in the palette editor.
- **UI-PALETTE-EDITOR-043** — When an arrow key is pressed, the editor shall select the single color one column or row away from the focus, clamped to the first and last colors.
  - Tests: `PaletteSelectionTests.Move_ClampsToPalette`
- **UI-PALETTE-EDITOR-044** — When Shift+arrow is pressed, the editor shall select the range from the anchor to the moved focus.
  - Tests: `PaletteSelectionTests.Move_Extend_SelectsRangeFromAnchor`
- **UI-PALETTE-EDITOR-045** — When Select All runs (Ctrl+A or the Edit menu), the editor shall select every color.
  - Tests: `PaletteSelectionTests.ClickFirstThenShiftClickLast_SelectsAllAndReplacesScatteredSelection`
- **UI-PALETTE-EDITOR-046** — When the palette shrinks, the selection shall drop indices past the end; if none remain, the editor shall select the color nearest the old focus.
  - Tests: `PaletteSelectionTests.Clamp_DropsIndicesPastCount`
- **UI-PALETTE-EDITOR-047** — When a swatch is activated without a pointer (keyboard, automation), the editor shall treat it as a plain click.
  - Tests: untested

### Copy, paste, swap and gradient

- **UI-PALETTE-EDITOR-050** — While a color is selected, Copy shall put the selected colors on the OS clipboard in index order, one per line, as `#RRGGBB` (`#RRGGBBAA` when not opaque), read-only palettes included.
  - Tests: untested
- **UI-PALETTE-EDITOR-051** — While the palette is editable and a color is selected, Paste shall write the clipboard's colors into consecutive indices from the lowest selected one, stopping at the last color, whatever the selection's size.
  - Tests: `PaletteEditSessionTests.SetColors_PastesFromStartAndClampsToPalette_AsOneUndoStep`
- **UI-PALETTE-EDITOR-052** — When the clipboard holds the text this editor last copied (line endings aside), or no text, Paste shall use the exact copied colors instead of reparsing the hex.
  - Tests: untested
- **UI-PALETTE-EDITOR-053** — When the clipboard holds other text, Paste shall read it as hex colors separated by whitespace or commas, `#` optional, each converted to the palette's color model.
  - Tests: untested
- **UI-PALETTE-EDITOR-054** — If the clipboard holds no colors or any token is not a color, then Paste shall change nothing and show "The clipboard does not contain colors as #RRGGBB or #RRGGBBAA lines" in the status bar.
  - Tests: untested
- **UI-PALETTE-EDITOR-055** — While exactly two colors are selected and the palette is editable, Swap shall exchange them.
  - Tests: `PaletteEditSessionTests.SwapColors_IsOneUndoStep`
- **UI-PALETTE-EDITOR-056** — While three or more colors are selected and the palette is editable, Gradient shall set each selected color strictly between the lowest and highest selected to the RGBA interpolation of those two ends, weighted by index, and leave unselected colors unchanged.
  - Tests: `PaletteEditSessionTests.FillGradient_InterpolatesBetweenEnds_AsOneUndoStep`

### Sources

- **UI-PALETTE-EDITOR-060** — The Sources section shall list one row per contiguous run of file entries (hex byte offset, color count of at least 1, little/big endian), and one row per native color (`#RRGGBBAA`) and per foreign color (raw value).
  - Tests: untested
- **UI-PALETTE-EDITOR-061** — The Add menu shall offer a file range (1 color, starting just after the last file range with its endian, or at 0 little-endian when there is none), a native color (opaque black) and a "<model> color" (raw 0), each added at the end; each row shall have a remove button.
  - Tests: manual — open Sources → Add in the palette editor and add each kind.
- **UI-PALETTE-EDITOR-062** — When a source edit leaves every row valid and every file entry inside the data file, the editor shall apply the new sources to the palette as one undoable step.
  - Tests: untested
- **UI-PALETTE-EDITOR-063** — When sources are applied, pending colors on file entries whose offset and endian are unchanged shall carry over; other file entries shall show the data file's colors.
  - Tests: `PaletteEditSessionTests.SetSources_OffsetEdit_KeepsPendingColorsOnUnchangedFileEntries`
- **UI-PALETTE-EDITOR-064** — When the applied sources equal the current ones, the editor shall add no history step.
  - Tests: `PaletteEditSessionTests.SetSources_Unchanged_AddsNoHistory`
- **UI-PALETTE-EDITOR-065** — If a row is invalid (negative offset, count below 1, or hex that does not parse in its model), then the row shall show its error, the palette shall keep its previous sources, and the editor shall be marked modified.
  - Tests: untested
- **UI-PALETTE-EDITOR-066** — If a file entry would extend past the end of the data file, or the palette has no data file, then the sources shall not be applied, the status bar shall show "A file source extends past the end of its data file", and the editor shall be marked modified.
  - Tests: untested
- **UI-PALETTE-EDITOR-067** — While any source is invalid, Save shall alert "Invalid Sources" ("'<name>' has sources marked as invalid. Fix them before saving.") and write nothing.
  - Tests: untested
- **UI-PALETTE-EDITOR-068** — The header's color count shall follow the source rows as edited, even while they are invalid.
  - Tests: untested
- **UI-PALETTE-EDITOR-069** — When an edit is undone, redone or discarded, the editor shall rebuild the source rows from the palette, dropping invalid row text.
  - Tests: untested
- **UI-PALETTE-EDITOR-070** — When "Index 0 is transparent" is toggled, the editor shall record it as a pending edit.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`

### Change color model

- **UI-PALETTE-EDITOR-075** — If any source is invalid, then Change Color Model shall alert "Invalid Sources" ("Fix the sources marked as invalid before changing the color model.") and open no dialog.
  - Tests: untested
- **UI-PALETTE-EDITOR-076** — The Change Color Model dialog shall list every color model, start on the current one, and show the palette's swatches before and after reinterpretation under the chosen model.
  - Tests: untested
- **UI-PALETTE-EDITOR-077** — The reinterpretation shall keep each contiguous file run's start offset and count with offsets respaced by the new color size, keep native colors, and keep foreign colors' raw bits.
  - Tests: `PaletteEditSessionTests.ReinterpretSources_ToLargerColor_RespacesFileOffsets`
- **UI-PALETTE-EDITOR-078** — If the chosen model is table-based and an entry's stored value is past the table, then the dialog shall show "Entry i holds 0xVV, which is not a valid <model> color (0x00-0xMM)" and disable Change.
  - Tests: untested
- **UI-PALETTE-EDITOR-079** — If the preview cannot be built (including a palette with no data file), then the dialog shall show "Cannot preview this color model: <reason>" and disable Change.
  - Tests: untested
- **UI-PALETTE-EDITOR-080** — The Change button shall be enabled only for a model other than the current one with a successful preview.
  - Tests: untested
- **UI-PALETTE-EDITOR-081** — When Change is accepted, the palette shall take the new model and sources as one undoable step, re-reading file colors so pending file color edits are dropped, as the dialog warns.
  - Tests: `PaletteEditSessionTests.ChangeColorModel_ThenUndo_RestoresModelAndColors`

### Palette files

- **UI-PALETTE-EDITOR-085** — When a `.pal` (JASC) or `.gpl` (GIMP) file is imported, the editor shall replace every source with one project-native color per file entry, as one undoable step.
  - Tests: `PaletteFileSerializerTests.Jasc_RoundTrips`, `PaletteFileSerializerTests.Gpl_RoundTrips`; manual — import through the file picker.
- **UI-PALETTE-EDITOR-086** — If the imported file cannot be read or parsed, then the editor shall alert "Import Error" naming the file and the reason, including the bad line, and change nothing.
  - Tests: `PaletteFileSerializerTests.Read_UnknownHeader_Fails`, `PaletteFileSerializerTests.Read_JascBadEntry_NamesLine`, `PaletteFileSerializerTests.Read_JascTooFewEntries_Fails`, `PaletteFileSerializerTests.Read_GplBadEntry_NamesLine`
- **UI-PALETTE-EDITOR-087** — When colors are exported, the editor shall suggest "<palette>.pal" and write the current colors, pending edits included, as a GIMP palette named after the palette when the path ends in `.gpl` and as JASC otherwise.
  - Tests: `PaletteFileSerializerTests.Jasc_RoundTrips`, `PaletteFileSerializerTests.Gpl_RoundTrips`; manual — export through the save picker.
- **UI-PALETTE-EDITOR-088** — If export fails, then the editor shall alert "Export Error" with the message.
  - Tests: untested

### Read-only palettes

- **UI-PALETTE-EDITOR-090** — While the palette is a built-in global palette, the editor shall show a "Read Only" badge, hide the Sources section and Assign, and disable the color controls, the transparency checkbox, Import, Change Color Model, Paste, Swap and Gradient.
  - Tests: untested
- **UI-PALETTE-EDITOR-091** — While the palette is a built-in global palette, Save and assigned colors (including from the flyout) shall change nothing; Copy and Export stay available.
  - Tests: untested

### Hotkeys and Edit menu

- **UI-PALETTE-EDITOR-095** — While the editor is active, Ctrl+S shall save, Ctrl+Z undo, Ctrl+Y redo, Ctrl+C copy, Ctrl+V paste, Ctrl+A select all, arrows move and Shift+arrows extend the selection.
  - Tests: untested
- **UI-PALETTE-EDITOR-096** — While the editor is active, the Edit menu's Undo, Redo, Copy, Paste and Select All shall run the editor's commands, and Cut and Delete shall be disabled.
  - Tests: untested

### Discard

- **UI-PALETTE-EDITOR-097** — When the user declines to save at the close prompt (UI-EDITORS), the editor shall restore the last-saved colors, sources, color model and transparency, clear the history, and rewrite the palette's resource file.
  - Tests: `PaletteEditSessionTests.Discard_RestoresColorsSourcesAndTransparency`
- **UI-PALETTE-EDITOR-098** — If that rewrite fails, then the editor shall alert "Project Error" ("Could not restore the saved palette: <reason>").
  - Tests: untested
- **UI-PALETTE-EDITOR-099** — When the editor is discarded because resources it uses are removed (UI-EDITORS), it shall restore the last-saved state without rewriting the resource file.
  - Tests: untested

### Defaults

- **UI-PALETTE-EDITOR-100** (inherited) — The swatch grid shall have one column per color up to 16 columns.
  - Tests: untested
- **UI-PALETTE-EDITOR-101** (inherited) — Swatch cells shall be 40 px for up to 16 colors, 32 px for up to 64, and 24 px beyond.
  - Tests: untested
- **UI-PALETTE-EDITOR-102** (inherited) — The table color picker shall have 16 columns.
  - Tests: untested

## Invariants

- The palette in memory equals the saved snapshot with the newest undo step applied; the data file holds the saved snapshot.
- Project-native and project-foreign source values always equal the working colors of their entries.
- The data file and the palette's resource file are written only by Save and discard (UI-PALETTE-EDITOR-024, -097).
- While the editor has pending edits, the palette node carries a committed model of the saved state; with none, it carries none.

## Edge cases

- A palette with no colors opens with nothing selected and no color editor.
- A palette with a scattered color source cannot be opened: building its source rows throws.
- With no data file, the palette cannot take file sources (UI-PALETTE-EDITOR-066) or change color model (UI-PALETTE-EDITOR-079), even when all its sources are project colors.
- Without a main window clipboard, Copy does nothing on the OS side and Paste falls back to the editor's last copy, if any.
- After a Save whose project write failed, declining to save on close keeps the colors already written to the data file, since they became the saved state.
- A status message (paste, out-of-range source) stays in the status bar until another message replaces it.
- Paste after a color model change converts the previously copied colors to the new model.

## Threading and lifetime

- UI thread only. The edit session raises `Changed` synchronously after every edit, undo, redo, Save and discard; the editor rebuilds swatches, source rows (except during its own source edit), command states and the committed model from it.
- Edits raise the palette's own change event; UI-EDITORS coalesces those into one graphics editor refresh per palette.
- The editor and its session live as long as the editor tab; nothing unsubscribes because both share the tab's lifetime. The committed model on the palette node is cleared when history empties (Save or discard).

## Decisions

- **One edit model: pending in the shared palette, written on Save.** Edits apply to the shared `Palette` so graphics editors preview them, and reach the data file and project XML only on Save; discard restores the saved state. Reason: palette edits must behave like graphics edits (pending, undoable, prompted before discard); before the rework each color's Save wrote the ROM and project immediately with no undo. Rejected: write-through per color.
- **One modified state per palette.** The graphics editor's color flyout routes its edits through the palette editor, opening it in the background. Reason: a palette edited in two places would otherwise have two pending states.
- **Snapshot history.** Each step stores a full snapshot of colors, sources, model and transparency. Reason: a palette is at most 256 colors plus a short source list, so snapshots are cheap and every operation undoes the same way.
- **Invalid sources block Save instead of disappearing.** Reason: the old editor silently skipped unparsable native and foreign entries, shifting every later index. One Save covers colors and sources; the separate "Save Sources" button was dropped.
- **Pending file colors follow their offset.** A source edit keeps pending colors only on file entries whose offset and endian are unchanged. Reason: a moved entry reads different data, so its old pending color no longer belongs to it.
- **No reordering or moving colors.** Reason: it changes how every arranger using those indices looks; left out of 1.0.
- **Layout: grid beside the editor, sources collapsed.** Reason: editing should not scroll the grid away, and sources are set up once and rarely touched. Selecting a swatch highlights its source and shows its file offset, so finding a color's location needs no counting.
- **Change color model reinterprets, it does not convert.** File runs keep their start and count and are re-read at the new size; foreign values keep their bits. Reason: fixes a palette created with the wrong model without deleting and recreating it.
- **Built-in palettes stay read-only; projects start from them as templates.** The Add Palette dialog (UI-PROJECT-TREE) copies a built-in palette's colors and model into a new project palette. A "Duplicate to project" command was planned; the template option is what exists.
- **Palette files: JASC `.pal` and `.gpl` only, imported as project-native colors.** Reason: the common formats first; RIFF, ACT and HEX later. Imported colors have no location in the data file, so they become project colors.
- **Copies inside the editor paste exactly.** The editor keeps its last copy as raw colors and uses it when the clipboard still holds that copy's text. Reason: raw values survive, where a round trip through RGBA hex could approximate them.
- **Editing state in plain classes.** `PaletteEditSession` and `PaletteSelection` hold history, pending colors and selection without Avalonia, so edit → undo → redo → save → reload is unit-tested against a `MemoryDataSource`.
- **Swatches are Buttons; modifiers come from the pointer release.** Reason: Buttons stay automatable and keyboard-accessible (docs/ARCHITECTURE.md); the release is tunneled so Shift/Ctrl are known before Click.

## Non-goals

- Reordering, moving, duplicating or bulk-adjusting (hue, brightness) colors.
- RIFF `.pal`, `.act`, `.hex` or JSON palette files.
- Editing built-in global palettes.
- Sub-palette (bank) selection; that belongs to arranger elements.

## Open items

- No UI path opens a built-in global palette in the editor (editors open from project-tree palettes only), so the read-only mode (UI-PALETTE-EDITOR-090, -091) looks unreachable. A planned "Duplicate to project" command does not exist; the Add Palette template is the closest feature. Decide whether read-only mode is still needed.
- `DiscardChangesAsync` rewrites the resource file because "a whole-project save may have written" pending edits, but the committed model (UI-PALETTE-EDITOR-028) already prevents that. The rewrite looks redundant.
- Save writes the resource file even when nothing is pending (UI-PALETTE-EDITOR-024).
- Status messages are never cleared by the palette editor.
- `PaletteEditorViewModel.ApplyHistoryAction` throws `NotImplementedException` and has no callers; `EditableColorBaseViewModel.SaveColor` has no callers; `ScatteredColorSourceModel` is unused.
- Palettes with a scattered color source throw on open instead of reporting the problem.
- The ViewModel wiring (validation blocking Save, paste parsing, read-only gating, committed model) has no tests; only the session and selection classes do.
