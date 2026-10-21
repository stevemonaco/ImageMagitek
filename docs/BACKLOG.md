# Backlog

The single list of work not done in ImageMagitek and TileShop: known bugs, gaps, checks, candidates and undecided questions. Each line names the spec it concerns and says when it is done.
Small items are taken straight from here and fixed with their spec updated; larger ones become a change proposal in `docs/changes/` first (see [docs/changes/README.md](changes/README.md)).
When an item is closed, its line is deleted.

**1.0** marks items that block the 1.0 release ([ARCHITECTURE.md](ARCHITECTURE.md) §1): they lose work, corrupt data, freeze the project format, or leave a visible control that does nothing. Out of scope for 1.0: direct-color XML codecs, compression ([proposal](changes/compression-support.md)), container scanners ([proposal](changes/container-scanners.md)), new platforms and color models, new drawing or selection tools, layers, tilemaps and scripting. Candidates for 1.1, roughly in order: sequential arrangers as project resources, export from sequential views and selections, CLI `--json` output and key globs, OS clipboard, tool hotkeys; then compression and direct-color XML codecs.

Every **1.0** item belongs to a change proposal, linked at the end of its line: [docs-1-0](changes/docs-1-0.md) and [release-1-0](changes/release-1-0.md). A **1.0** line with no link has no proposal yet.

Found by reading the code, not reproduced, unless a test is named.

## Likely bugs

### Library


### UI

- [UI-PROJECT-TREE-135] The Add New Tilemap dialog lists the "FRLG Block" block format (LIB-METATILE-MAPS-011) among tilemap map formats, since both come from one map format factory; a tilemap built with it has no palette or flip fields.

## Missing validation

- [LIB-PROJECT-SERVICE] `CanMoveNode` and `GetContainingProject` throw for nodes outside every open tree; done when `CanMoveNode` returns a failure.
- [LIB-CODECS] A planar pattern count mismatch or empty pattern throws `ArgumentException` while other pattern errors return a failed result; done when all return failed results.
- [LIB-PALETTES] `SetForeignColor` accepts a color of any model, and negative indexes throw `IndexOutOfRangeException`; done when both are validated.
- [LIB-COLORS] Match indices are bytes, so palettes over 256 entries wrap; done when entries are limited to 256 or indices widened.
- [LIB-ARRANGERS] `SetElement` accepts x == Width or y == Height (throws `IndexOutOfRangeException`); `TryRotateElement` checks bounds with `>` and `TryMirrorElement` not at all; scattered `SetElement` does not check the element's pixel size; `SequentialArranger.Move(BitAddress)` accepts negative addresses and `Resize` sizes below 1; done when each is refused with an argument error or failed result.
- [LIB-IMAGES] The IndexedImage and DirectImage sub-rectangle constructors do not bounds-check, and FloodFill clip bounds larger than the image throw; done when both produce argument errors or are clipped.
- [LIB-IMAGE-IO] `ImageImportPreview.Commit` does not check `CanCommit` (the UI and CLI do); done when it refuses, or a decision confirms it.
- [LIB-IMAGE-IO, LIB-ARRANGERS] Import reports pixels on elements past the end of their source as Changed, and save-conflict analysis reports such elements as Modified, though neither is ever written; done when they are excluded.
- [UI-ARRANGING] Delete records history and marks the editor modified when every covered element is already empty; done when it is a no-op.
- [UI-PALETTE-EDITOR] Change Color Model needs a data source even when every source is a project color; done when such a palette can change model.

## Undecided questions

- [LIB-PROJECT-SERVICE] Whether `ApplyResourceDeletionAsync` should report a kept non-empty folder as a failure although its nodes were removed; done when decided.
- [UI-PROJECT-TREE] The generation dialog shows no thumbnail of the selected metatile map, tilemap or palette output (UI-PROJECT-TREE Open items).
- [UI-PROJECT-TREE] A data file's missing state is read only on project open, so it does not update if the file disappears or reappears mid-session; done when refreshed, or the decision to leave it is recorded.
- **1.0** [UI-PALETTE-EDITOR] "Duplicate to project" for global palettes (PLAN Milestone 4, marked done) does not exist; the closest is the Add Palette dialog's template option, and no UI path opens a global palette in the editor, so read-only mode looks unreachable; done when either a Duplicate command and a way to open global palettes exist, or UI-PALETTE-EDITOR-090/091 are struck.

## Feature gaps

### For 1.0

- [UI-SHELL, LIB-PROJECT-SERVICE] Command-line and OS integration (optional for 1.0): `args` are passed to Avalonia but never read, and there is no window drop handler; done when a non-`.xml` path on the command line, or a file dropped on the window, opens as a standalone file (drop is a manual check).
- [UI-SHELL] Status-bar Indefinite and Reset messages are handled but never sent; done when a sender exists or the values are removed.

### P1

- [LIB-CODECS] Direct-color XML codecs: the schema accepts `colortype="direct"`, `CodecFactory` throws `NotSupportedException`.
- [LIB-COMPRESSION] Read-only compression support: core, UI and the Square LZSS sample are in (draft spec); remaining tasks are in the [change proposal](changes/compression-support.md).
- [LIB-PROJECT-FORMAT, UI-GRAPHICS-EDITOR] Sequential arrangers as project resources ("save view as arranger"; deferred to 1.1); done when one saves and reloads. Unblocks persisting the element layout (P2, deliberately not done in Milestone 9) and per-file view state for standalone files.
- [LIB-PALETTES, LIB-PROJECT-FORMAT, UI-PALETTE-EDITOR] Palettes of only project colors, saved in the palette XML with no data file (intended, never built): the writer refuses them (LIB-PROJECT-FORMAT-036), `ProjectTreeBuilder.AddPalette` fails "has no data file", `Palette.LoadForeignPalette` guards on a data source, and the editor cannot change model or apply sources without one; done when such a palette is created, edited, saved and reloaded.
- [LIB-PALETTES, UI-PALETTE-EDITOR] Remaining palette file formats: RIFF `.pal`, `.act`, `.hex`, and a JSON writer (JASC `.pal` and `.gpl` exist).

### P2

- [LIB-CODECS] Platforms: Game Boy/GBC 2bpp (explicit entry), Master System 4bpp, PC Engine/TG16, N64 CI4/CI8/IA/I, NDS, Saturn, Neo Geo sprites, Atari/Lynx, WonderSwan; GBA/NDS BGR555 direct bitmap codecs.
- [LIB-SCANNING] Container scanners: find self-describing graphics (PSX TIM first) in a data file and add them as arrangers and palettes, and browse archive entries through container-reader plugins: [change proposal](changes/container-scanners.md).
- [LIB-TABLES] Table-driven resources: generate arrangers and palettes from a game's record tables through a declarative definition, regenerate them after the table changes, and relocate data by rewriting its pointer: [change proposal](changes/table-resources.md).
- [LIB-TILEMAPS] Tilemap resources: a map read from data, decoded by a map format, edited in Arrange mode, plotted in Tiles mode and written back in place: [change proposal](changes/tilemaps.md).
- [LIB-SCENES] Layered scenes: stack arrangers back to front with transparency into one composite view, export it, and draw on an active layer: [change proposal](changes/layers.md).
- [LIB-METATILE-MAPS] Metatile maps: block maps drawn through shared metatile sets with layered metatiles, viewed composited and edited by plotting blocks, as in FireRed's overworld: [change proposal](changes/metatile-maps.md).
- [LIB-CODECS] XML format extensions: tile stride/padding, bit order and endianness, per-tile header bytes (variable-width fonts currently need a plugin).
- [LIB-ARRANGERS] Bit-wise sequential offsets: tiled elements and absolute moves are bit-addressed, but Single-layout stepping (TODO in `SequentialArranger`) and its row/column/page steps are not; done when Single-layout round-trip tests pass at a non-byte-aligned offset.
- [LIB-COLORS] Color models: RGB24, ARGB32, RGB565, TG16 GRB333, N64 IA/I, grayscale.
- [UI-SHELL, LIB-SERVICES] Reload codecs and palettes without restarting: a new default palette, NES master palette, or codec XML edited while exploring needs a restart today (UI-SHELL-055); done when choosing a new default in Preferences takes effect for newly opened editors.
- [LIB-PALETTES] Palette sub-banks (a 16-color sub-palette of a 256-color palette per element).
- [UI-PALETTE-EDITOR] Reorder, duplicate and bulk hue/brightness adjustment (reorder deliberately left out of 1.0: it changes every arranger using those indices).
- [UI-DRAWING] Line, rectangle, ellipse, eraser, brush size, replace color; single-key tool hotkeys and `[`/`]` to step palette indices.
- [UI-ARRANGING, LIB-IMAGES] Magic wand, lasso, pixel-level flip and rotate of a selection (element mirror/rotate are display attributes).
- [UI-ARRANGING] OS clipboard for images (the clipboard is a private static), so pixels can be exchanged with external editors.
- [LIB-IMAGES, UI-ARRANGING] Nearest-color pixel paste: pasting between arrangers whose palettes differ maps each pixel to the nearest destination palette color (the intent of the deleted `RemapByAnyIndex` stub), reusing `PaletteColorMatcher` with `ColorMatchStrategy.Nearest`; also needed to paste external image content from the OS clipboard; done when a paste that fails exact matching can apply with nearest colors.
- [UI-GRAPHICS-EDITOR] Bookmarks and back/forward navigation in sequential browsing.
- [UI-IMAGE-IO, LIB-IMAGE-IO] Export from sequential views and selections (the library exports any subsection); done when a View-mode editor can export a PNG.
- [LIB-IMAGE-IO] Export formats: BMP, indexed GIF, palette sidecar file.
- [UI-IMAGE-IO] Expose AlphaThreshold and MaxDistance in the import dialog.
- [LIB-IMAGES, LIB-PROJECT-FORMAT] WAL protection for binary pixel writes (`SaveImage` writes sources in place); done when an interrupted pixel save is recovered.
- [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Duplicate resource; "New Sequential Arranger" in the tree.
- [CLI-COMMANDS] Dry-run and report output (import report without committing), `--json` for print/import/export, key filters and globs (`export "Sprites/*"`), palette export/import verbs.

### P3

- [LIB-CODECS] Public span-based bit reader/writer replacing the internal `PackedBits` (BitStream is now used only by samples and tests); codec authoring UI with live preview; codec auto-detection across codecs at an offset.
- [LIB-PALETTES] Palette generation (quantize an image, dithering on import); color-cycling preview.
- [UI-GRAPHICS-EDITOR] Hex view side panel, pattern search from an image or bytes, minimap, zoom percentage display and presets.
- [UI-GRAPHICS-EDITOR] Marching-ants selection border: an animated dashed outline for selections drawn by `ArrangerRenderer` (the deleted `Rectangle.animatedBorder` style was the only trace).
- [UI-DRAWING] Undo history panel, reference overlay, text tool using a font arranger.
- [UI-EDIT-HISTORY] Pixel history actions store the values they replaced, so undo restores them directly instead of re-decoding the arranger from the file and replaying every pixel action since the last arranger action; done when undo cost no longer grows with history length.
- [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Project tree search/filter; project metadata (description, ROM checksum, notes); IPS/BPS patch output instead of writing in place.
- [CLI-COMMANDS] Project authoring verbs (`new`, `add-datafile`, `add-palette`, `add-arranger`), a batch manifest, a scripting host, `dotnet tool` packaging.
- [UI-SHELL] Customizable keybindings; save and restore the window and dock layout; localization; a plugin manager (decided against for 1.0: About lists plugin codecs).

## Untested behavior worth a test

- [LIB-DATASOURCE] Past-EOF read throws `EndOfStreamException`; overflowing a fixed-capacity `MemoryDataSource` throws `NotSupportedException`; `Flush` and `Write` never raise `DataWritten`.
- [LIB-CODECS] XML schema and semantic validation failures; unknown name throws `KeyNotFoundException`; direct-color XML throws; pattern codecs ignore the requested size; each codec gets its own format clone; `CloneCodec`.
- [LIB-COLORS] RGB15, BGR6, NES and RGBA32 conversion; BGR9 to-foreign rounding; hex format and parse for every model; factory `NotSupported`/`ArgumentException` paths; distance ties pick the lowest index; `GetIndexByNativeColor` throwing.
- [LIB-PALETTES] 3-byte and big-endian file colors; `ZeroIndexTransparent` raises `Changed` only on a real change; global `SavePalette` returns false; JSON `zeroIndexTransparent` default (true) and `JsonException` cases; `PaletteStore` default and NES loading.
- [LIB-ARRANGERS] `SequentialArranger.Move` per move type including clamping; `ChangeElementLayout` rounding and pattern order (2x2 H vs 2x2 V addresses); `CloneArranger` (full, sub-rectangle, Single-layout restriction); `UnlinkResource`.
- **1.0** [LIB-ARRANGERS] `ElementCopier` (`ElementCopierTests` is entirely commented out), including paste cropping and the cross-project refusal.
- **1.0** [LIB-IMAGES] `ImageCopier` (`ImageCopierTests` has one empty method without `[Fact]`): each `CopyPixels` overload, the blank-element failure, operation order; `SaveImage` raising `NotifyDataWritten` once per distinct source; `CanSetPixel`/`SetPixel(color)`/`GetPixelColor`; FloodFill not crossing palettes.
- [LIB-IMAGE-IO] RGBA fallback export (over 256 combined entries, index past its slot, empty-cell transparency), empty cells exporting as index 0, `LoadImage` and `Prepare(path)` failure results.
- [UI-SHELL] `HotkeyService` scope, suspension, text-box plain-key deferral and first-match-wins; `StatusViewModel` Short/Indefinite/Reset; recent list rules (top insert, max 8, standalone skipped, missing filtered at start); `UserPreferencesStore` load fallback and atomic save. Several need logic extracted from the ViewModel first.
- [UI-EDITORS] `OpenEditor` dispatch, the codec-by-extension fallback chain, the missing-source gate, `ResourceChanged` coalescing, `ConfirmRemovalAsync` prompt selection (needs a slimmer constructor or an extracted helper).
- [UI-PROJECT-TREE] Root open/close/convert keep `Projects` in sync; Move picker filtering and `CanDropNode`.
- **1.0** [UI-GRAPHICS-EDITOR] Mode-switch save prompt (Yes/No/Cancel) and the read-only/standalone gating properties, with a fake `IInteractionService`.
- **1.0** [UI-EDIT-HISTORY] VM wiring: `IsModified` after undo/redo, CanUndo/CanRedo notifications, history cleared on save, discard and `ReloadFromSource`.
- [UI-DRAWING] `CanRemapColors` (single palette, read-only, empty region); clip enforcement in Pencil and flood fill at VM level.
- [UI-IMAGE-IO] `ImportImageViewModel` enablement and blocking reason (needs `ImageSharpFileAdapter` injected first).
- [UI-PALETTE-EDITOR] Invalid source rows block Save and mark the editor modified; an out-of-range file source is not applied; paste parsing (internal exact copy, OS hex tokens, error); read-only gating; `CommittedModel` set and cleared with history; a new edit clears redo and a throwing edit rolls back with no step; `ChangeColorModelViewModel` preview errors for table models.

## Dead code and cleanup

- [UI-PALETTE-EDITOR] The project rewrite in `DiscardChangesAsync` is probably redundant given `CommittedModel`, and Save rewrites the resource XML when nothing is pending; the status ActivityMessage is never cleared.
- [LIB-ARRANGERS] `ArrangerBuilder`'s sequential path ignores `WithElementLayout` and `WithPixelColorType`.
- [LIB-IMAGES] `CopyPixelsDirect` on an indexed arranger throws `NotImplementedException` where the indexed variant throws `ArgumentException`; `ImageBase.Right`/`Bottom` doc comments say inclusive, the values are exclusive.
- [LIB-PROJECT-TREE] `ProjectResourceBaseComparer` (internal, unused) and `IProjectResource.ShouldBeSerialized` (never read).

## Release engineering and documentation

- **1.0** [CLI-PUBLISH] Whether release zips use ReadyToRun: measure zip size and cold/warm startup (TileShop to the main window, `TileShopCLI --version`) on win-x64 with and without `-ReadyToRun`, record it in CLI-PUBLISH's ReadyToRun decision, and settle whether the release workflow passes it.
- **1.0** [CLI-PUBLISH] Manual checks of CLI-PUBLISH-003/004/006/015/016 are pending: two consecutive `publish.ps1` runs, a run with a failing test (non-zero exit, no zip), a `-ReadyToRun` run, a pushed branch going green and red in CI, and a release-workflow dry run producing a draft with six zips.
- **1.0** README: still says .NET 6 and lists Autofac and Jot; done when the stack and dependencies are current and CLI usage (verbs, options, exit codes) is documented in `docs/cli.md`. ([proposal](changes/docs-1-0.md))
- **1.0** Release notes for 1.0 calling out the codec behavior changes existing projects will notice (recorded in LIB-CODECS Decisions, "Codec rework behavior changes"). ([proposal](changes/docs-1-0.md))
- Codec XML authoring guide and plugin authoring guide (after 1.0, per [docs-1-0](changes/docs-1-0.md)); decide whether to ship the plugin contract tests as a package for plugin authors or document copying them from the test project.
- **1.0** [LIB-PLUGINS] A plugin with one resizable dimension (one increment 0) reports `CanResize` and a 0 step for the fixed axis, which the UI resize steps and `IndexedCodecContract.ResizeIncrements_AreHonored` do not expect; done when the UI handles a per-axis fixed size or registration refuses it. Settle before the contracts package baseline freezes at 1.0.0.
- [LIB-PLUGINS] After 1.0 ships, set `PackageValidationBaselineVersion` to 1.0.0 on `ImageMagitek.Plugins.Contracts` so CI rejects breaking contract changes in 1.x.
- [ARCHITECTURE] The spec format (front matter, ids, cited tests and paths exist, type ownership) is not enforced; done when a test in `ImageMagitek.UnitTests` checks it, as Monaco.Presto's `SpecTests` does.

## Manual checks

DevTools cannot drive pointer gestures, drag and drop, or popups, so these need a person in the running Debug app. They are run as part of the 1.0 release QA pass ([proposal](changes/release-1-0.md)). Each spec's `manual —` requirements are the full list; these are the ones still pending from earlier milestones.

- [UI-EDITORS] Rename, move and delete in the project tree with editors open; palette edits appear live in graphics editors; saving or importing pixels refreshes other unmodified editors on the same data.
- [UI-EDITORS] `PaletteColorAssignedMessage` is meant to open the palette editor in the background, but adding an editor activates its tab: check whether focus jumps to the palette tab after a color-flyout edit.
- [UI-PROJECT-TREE] Drop onto a folder and onto an invalid target; rename a ROM, reopen the project, relink.
- [UI-PROJECT-TREE] Standalone files: Open File on a ROM shows a root with no expander and a file icon; double-click or Enter opens a new editor each time; reopening the file, or opening one already in a project, selects the existing node; two editors on one standalone file refresh each other after a save; Close and Create Project from File... work, with the data file node selected afterwards; the recent list is unchanged; "Open File..." sits above "Open Project..."; Close All Projects closes a project data file's sequential editor; "Add as New Scattered Arranger..." is disabled with its hint in a standalone editor.
- [UI-GRAPHICS-EDITOR] Saving a sequential editor in Draw mode, both standalone and for a project data file.
- [UI-ARRANGING] Drag a selection within an editor and between editors; drop with and without Shift; drop pixels onto a read-only arranger.
- [UI-DRAWING] A color-flyout edit marks the palette editor tab modified and lands as a pending, undoable edit; Color Remap is enabled on "Adult Rydia Map" and disabled on "Portraits"; picking and dragging in the Color Remapper.
- [UI-IMAGE-IO] Preview pan, zoom and peek; selecting and double-clicking report entries; import into an arranger open in an editor.
- [UI-PALETTE-EDITOR] Shift+click and Ctrl+click on swatches; the Sources → Add flyout for all four kinds, including "Scattered colors..."; assign a color to a scattered entry, Save, and check in a hex editor that only the field bits changed; import and export through the file pickers.
- [UI-DRAWING] Gesture capture: draw a Pencil stroke, press and release Ctrl mid-stroke, release, draw a second stroke, undo twice and check both strokes are undone; repeat with Shift, and with an Apply Palette drag in Arrange mode. Also check a Shift+drag selection, Shift-hover single select and selection handle drags still work, and that releasing outside the image ends a stroke.
- [UI-DRAWING] Color Picker on an unlisted palette: copy elements from "Portraits" into "Adult Rydia Map" choosing one whose palette is not listed there, Ctrl+click a pasted pixel in Draw mode, and check the swatches show its palette and Pencil draws.
- [UI-DRAWING] Pencil with duplicate palette colors: pick the higher duplicate index, draw, save, reopen, and check with Color Picker that the stored index is the chosen one.
- [UI-ARRANGING] Drops follow Ctrl+V: drag pixels from a sequential editor in View mode onto a scattered editor in Arrange mode and check it switches to Draw before floating; with the target modified, check the Save Changes prompt and that Cancel discards the drop; Shift-drop applies in Draw mode; drag elements onto a sequential editor and check it ends in Draw mode with a pixel paste; drag a selection within a sequential editor in View mode and check that Enter does not write pixels in View mode.
- [UI-SHELL] Unhandled exceptions (UI-SHELL-140): temporarily throw from a menu command in a Debug build, modify an editor, invoke the command, and check the "Unexpected Error" alert, the log entry, and that after OK the app keeps working and the edit is still unsaved and savable.
- **1.0** [UI-SHELL] The debug load button exists only in Debug builds. ([proposal](changes/release-1-0.md))
- [UI-SHELL] Exit: modify an editor, then close with the title-bar X, Alt+F4 and File → Exit; choose Cancel (app, edits and projects remain), then Yes (saves and exits). Float a tab and exit (the app shuts down with the floating window open).
- [UI-EDITORS] Float a tab, then File → Close <name>, and separately delete its resource in the tree: the floating tab closes each time. After closing tabs with their X, the shown tab and the File menu's Close <name> agree.
- [UI-IMAGE-IO] Export with a missing ROM (alert before the file picker), over a read-only PNG ("Export Error"), and with a modified editor (Yes saves then exports, No exports the saved state and keeps the edits, Cancel stops). Import into a read-only ROM: "Import Error" stacks above the dialog, and the dialog stays open and usable after OK.
- [UI-ARRANGING] Pastes on `ArrangerSkiaBitmap`: copy and paste elements (Ctrl+C, Ctrl+V) and pixels in Draw mode, drag one, and check the overlay renders and applies as before, including over a Greyscale draw clip.
- [UI-SHELL] Save All becomes enabled after a pixel edit is applied (DevTools cannot apply a pending paste), and saves every modified editor without prompting.
- [CLI-COMMANDS] `TileShopCLI print` with `_palettes/Classic.json` renamed in the build output exits -4 (the malformed-codec warning is covered by `CliApplicationTests.BootstrapIssue_PrintsWarningExits0`).
- [CLI-PUBLISH] In the trimmed single-file publish, `--help`, `export --help` and `--version` exit 0, and with the sample plugin under `_plugins` an arranger using "Last Armageddon Font" exports and `importall` skips it as read-only.
