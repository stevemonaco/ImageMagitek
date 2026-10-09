---
id: UI-IMAGE-IO
title: Image import and export
project: TileShop.UI
sources:
  - TileShop.UI/Features/Dialogs/ImportImageViewModel.cs
  - TileShop.UI/Features/Dialogs/ImportImageView.axaml
  - TileShop.UI/Features/Dialogs/ImportImageView.axaml.cs
  - TileShop.UI/Features/Dialogs/ImportColorEntryViewModel.cs
  - TileShop.UI/Features/Renderer/ImportPreviewRenderer.cs
  - TileShop.UI/Features/Project/ProjectTreeViewModel.cs
  - TileShop.UI/Features/Graphics/GraphicsEditorViewModel.Selection.cs
  - TileShop.Shared/Messages/Messages.cs
  - TileShop.Shared/Models/UserPreferences.cs
types:
  - ImportImageViewModel
  - ImportImageView
  - ImportColorEntryViewModel
  - ImportColorEntryKind
  - ImportPreviewRenderer
  - ImportImagePreferences
  - ImportImageIntoArrangerMessage
tests:
  - ImageImporterTests
  - ReadOnlyArrangerTests
depends:
  - LIB-IMAGE-IO
  - LIB-IMAGES
  - LIB-ARRANGERS
  - LIB-DATASOURCE
  - UI-EDITORS
  - UI-PROJECT-TREE
  - UI-GRAPHICS-EDITOR
---

# Image import and export

## Purpose

The UI around PNG export and import for scattered arrangers: the export file prompt, the import entry points and their refusals, and the import dialog that stages an image, previews the result and blocks a commit that cannot be matched. Decoding, color matching, the import report, partial and offset import and indexed PNG writing are LIB-IMAGE-IO; the tree context menu that hosts Export As... and Import From... is UI-PROJECT-TREE.

## Requirements

### Export

- **UI-IMAGE-IO-001** — When the user chooses Export As... on a scattered arranger node, the app shall ask for a file name defaulting to "<arranger>.png" and write the arranger there as a PNG (LIB-IMAGE-IO).
  - Tests: manual — export an arranger from the project tree and open the PNG.
- **UI-IMAGE-IO-002** — If the arranger reads from a missing data file, then export shall alert with the missing file before asking for a file name, and write nothing.
  - Tests: manual — rename the ROM, reopen the project, Export As... (needs a fake `IAsyncFileRequestService` on `ProjectTreeViewModel` to automate).
- ~~**UI-IMAGE-IO-003**~~ — Removed: replaced by the export prompt (UI-IMAGE-IO-026).
- **UI-IMAGE-IO-026** — If an open editor on the arranger has unsaved changes, then export shall ask "Save Changes" (Yes/No/Cancel) before asking for a file name; Yes saves and stops if the save failed, No exports the arranger as last saved and keeps the editor's changes, Cancel stops.
  - Tests: manual — modify an arranger, Export As... on its node, choose each option.
- **UI-IMAGE-IO-027** — If writing the exported image fails, then export shall alert "Export Error" with the reason and log the exception.
  - Tests: manual — export over a read-only PNG.

### Import entry points

- **UI-IMAGE-IO-004** — The app shall offer import as Import From... on a scattered arranger node and as "Import Image Into Selection..." in a scattered editor's canvas context menu, the latter enabled only with a selection on an arranger that is not read-only.
  - Tests: manual — right-click a node and the canvas.
- **UI-IMAGE-IO-005** — If the arranger is read-only, then import shall alert "Import" with "'<name>' is read-only because it <reason>" (LIB-ARRANGERS-049) and stop.
  - Tests: `ReadOnlyArrangerTests.Prepare_ReadOnly_Fails`
- **UI-IMAGE-IO-006** — If the arranger reads from a missing data file, then import shall alert with the missing file and stop.
  - Tests: untested
- **UI-IMAGE-IO-007** — If an open editor on the arranger has unsaved changes, then import shall ask "Save Changes" (Yes/No/Cancel) first; Yes saves and stops if the save failed, No discards, Cancel stops.
  - Tests: manual — modify an arranger, then Import From... on its node.
- **UI-IMAGE-IO-008** — Import shall then ask for an image file and stop if none is chosen.
  - Tests: untested

### Import dialog

- **UI-IMAGE-IO-009** — The dialog shall be titled "Import Image Into '<name>'", or "Import Image Into '<name>' Selection (W×H at X, Y)" when started from a selection, and fill the window.
  - Tests: untested
- **UI-IMAGE-IO-010** — The dialog shall show the image's path and its size, adding "at (X, Y)" when the image is offset or differs in size from the arranger; Browse… stages another file.
  - Tests: untested
- **UI-IMAGE-IO-011** — If the image cannot be loaded or staged, then the dialog shall show the reason as the blocking message, show "No image staged", and disable Import.
  - Tests: untested
- **UI-IMAGE-IO-012** — While the arranger is indexed, the dialog shall offer Exact, Nearest and Nearest RGB matching and "Transparent → index 0"; changing any of them restages the import. Direct-color arrangers show neither.
  - Tests: `ImageImporterTests.Prepare_Nearest_SubstitutesNearestEntryAndReports`, `ImageImporterTests.Prepare_MapTransparentToIndexZero_SkipsColorMatching`
- **UI-IMAGE-IO-013** — When the user changes Offset X or Y, the dialog shall restage the image with its top-left at that arranger pixel, leaving uncovered pixels unchanged and cropping what falls outside.
  - Tests: `ImageImporterTests.Prepare_Offset_PlacesImageInArrangerCoordinates`, `ImageImporterTests.Prepare_SmallerImage_LeavesUncoveredPixelsUnchanged`, `ImageImporterTests.Prepare_LargerImage_IsCropped`
- **UI-IMAGE-IO-014** — When started from a selection, the dialog shall seed the offset at the selection's top-left and change no pixel outside the selection.
  - Tests: `ImageImporterTests.Prepare_Bounds_ClipsChanges`
- **UI-IMAGE-IO-015** — The preview shall blend from the current arranger to the result by the slider; 1 shows current, 2 shows imported, holding Space flips to the far end, and 3 toggles a diff view during which the blend controls are disabled.
  - Tests: manual — press 1, 2, 3 and hold Space in the dialog.
- **UI-IMAGE-IO-016** — The diff view shall show changed pixels in their imported colors, pixels becoming transparent in magenta and unmatched pixels in red over a dimmed current image.
  - Tests: manual — import an image with an unmatched color and toggle diff.
- **UI-IMAGE-IO-017** — The preview shall zoom around the pointer with the wheel, zoom with + and −, fit with Ctrl+W (a little smaller than the viewport, whole-number zoom when magnified), reset to actual size centered with Ctrl+R, pan with left or middle drag, and show the zoom as a percentage.
  - Tests: manual — the preview canvas needs pointer gestures.
- **UI-IMAGE-IO-018** — While the pointer is over the preview, the dialog shall describe the pixel as "(x, y)  #source → index n|#result" with "· unmatched", "· substituted" or "· changed", or "(x, y)  outside the image".
  - Tests: untested
- **UI-IMAGE-IO-019** — The report shall show the summary (LIB-IMAGE-IO) and list unmatched colors first, then substitutions, each with source and target swatches, target index, distance, first location and count; with no entries it shall say "Every color matched a palette entry exactly".
  - Tests: `ImageImporterTests.Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports`
- **UI-IMAGE-IO-020** — When the user selects a report entry, the preview shall highlight that color's pixels; double-clicking it shall center the preview on its first pixel.
  - Tests: manual — select and double-click report entries.
- **UI-IMAGE-IO-021** — While any color is unmatched, the dialog shall disable Import and show "N color(s) could not be matched exactly — choose Nearest matching or fix the image".
  - Tests: untested
- **UI-IMAGE-IO-022** — When the user clicks Import, the dialog shall write the staged result into the arranger's data sources and save the match strategy, transparency mapping, diff toggle and blend to user preferences; Cancel writes nothing. Preferences are saved only when the write succeeds.
  - Tests: `ImageImporterTests.Commit_WritesResultIntoArranger`
- **UI-IMAGE-IO-028** — If writing the import result fails, then the dialog shall alert "Import Error" with the reason, log the exception and stay open with its staged image and options.
  - Tests: manual — mark the ROM read-only, import, check the alert stacks above the dialog and the dialog stays open after OK.
- **UI-IMAGE-IO-023** — After an import writes data, open unmodified editors that read it shall reload (UI-EDITORS).
  - Tests: manual — import into an arranger shown in an open editor.

### Defaults

- **UI-IMAGE-IO-024** (inherited) — With no saved preferences, the dialog shall use Exact matching, the diff view off and the blend at imported.
  - Tests: untested
- **UI-IMAGE-IO-025** (inherited) — With no saved transparency choice, "Transparent → index 0" shall be on when any element's palette has a transparent index 0.
  - Tests: untested

## Invariants

- Nothing is written to a data source until Import is accepted with a committable preview.
- The preview is always computed against the arranger's saved data, because unsaved editor changes are resolved before the dialog opens.

## Edge cases

- Negative offsets are allowed and crop the image's top or left.
- Choosing a new file with Browse… resets the report selection.

## Threading and lifetime

- UI thread only. The view assigns the VM's preview callbacks on `DataContext` change and disposes its renderer on unload.
- Import Into Selection travels from the editor to `ProjectTreeViewModel` as `ImportImageIntoArrangerMessage` through the default messenger.

## Decisions

- **Partial and offset import.** Images no longer need to match the arranger size: the dialog takes an X/Y offset (which may be negative), uncovered pixels stay unchanged, and "Import Image Into Selection..." seeds the offset at the selection and clips to it. Rejected: requiring an exact size.
- **Resolve unsaved changes before importing or exporting.** Import asks to save or discard an open editor's changes first, so the preview and the write are against the saved data. Export asks the same question, but its No exports the saved state and keeps the edits, because export does not touch them. Reason: a user exporting to check their edits learns what the PNG holds. Rejected: silently exporting the saved state (the earlier surprise); exporting the editor's working pixels (needs an in-memory library export and would differ from the CLI); a status-bar note.
- **Import failures keep the dialog open.** `ImportImageViewModel` takes `IInteractionService`; a failed commit is logged, alerted as "Import Error" above the dialog (UI-SHELL-090), and the accept returns false, so the staged preview and options survive for a retry. Reason: the file may become writable. Rejected: showing the failure as the dialog's blocking message (that area means "fix the image", and it would disable Import).
- **Error alerts show the message; the log keeps the stack.** Applies to "Export Error" and "Import Error" as to "Save Error" (UI-GRAPHICS-EDITOR).
- **Missing sources are refused.** Export and import, like opening an editor, refuse a resource that reads from a missing data file and point to Relink... instead of failing on the first read.
- **Read-only arrangers refuse import up front.** The read-only check runs before the file picker rather than failing at commit.
- **Commit is blocked while colors are unmatched.** An exact match that leaves colors unmatched cannot be imported; the user switches to Nearest matching or fixes the image. Rejected: silently keeping the current pixel.

## Non-goals

- Export or import for sequential views and selections (backlog, P2).
- BMP, GIF or palette sidecar formats.

## Open items

- `ImageImportOptions` supports both but the dialog exposes neither (both stay at their defaults).
- `MenuViewModel.ExportArrangerToImage` and `ImportArrangerFromImage` are unreachable: their Arranger menu is commented out.
- No UI test covers the dialog; `ImportImageViewModel` creates its own `ImageSharpFileAdapter`, which would need injecting to test it without files (its constructor also takes `IInteractionService`, which a fake can supply).
- A commit that fails mid-way can leave some elements written; the library has no transactional data write.
