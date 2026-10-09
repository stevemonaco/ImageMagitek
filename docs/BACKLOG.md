# Backlog

The single list of work not done in ImageMagitek and TileShop: known bugs, gaps, checks, candidates and undecided questions. Each line names the spec it concerns and says when it is done.
Small items are taken straight from here and fixed with their spec updated; larger ones become a change proposal in `docs/changes/` first (see [docs/changes/README.md](changes/README.md)).
When an item is closed, its line is deleted.

**1.0** marks items that block the 1.0 release ([ARCHITECTURE.md](ARCHITECTURE.md) §1): they lose work, corrupt data, freeze the project format, or leave a visible control that does nothing. Out of scope for 1.0: direct-color XML codecs, compression ([proposal](changes/compression-support.md)), new platforms and color models, new drawing or selection tools, layers, tilemaps and scripting. Candidates for 1.1, roughly in order: sequential arrangers as project resources, export from sequential views and selections, CLI `--json` output and key globs, OS clipboard, tool hotkeys; then compression and direct-color XML codecs.

Every **1.0** item belongs to a change proposal, linked at the end of its line: [project-format-freeze](changes/project-format-freeze.md), [project-format-round-trip](changes/project-format-round-trip.md), [project-service-integrity](changes/project-service-integrity.md), [resource-name-validation](changes/resource-name-validation.md), [graphics-editor-edit-safety](changes/graphics-editor-edit-safety.md), [shell-editor-lifecycle](changes/shell-editor-lifecycle.md), [startup-robustness](changes/startup-robustness.md), [library-fixes-1-0](changes/library-fixes-1-0.md), [cli-1-0](changes/cli-1-0.md) and [release-1-0](changes/release-1-0.md). A **1.0** line with no link has no proposal yet.

Found by reading the code, not reproduced, unless a test is named.

## Likely bugs

### Library

- **1.0** [LIB-ARRANGERS, UI-ARRANGING] `ScatteredArranger.CloneArranger` shares codec instances with the original and `IndexedImage.TrySetPalette` sets `codec.Palette` in place, so Apply Palette in an editor changes the project arranger before Save and survives Discard; done when applying a palette and then discarding leaves the project arranger's element palettes unchanged. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [LIB-PROJECT-SERVICE] Save As resolves resource and project-file paths from the original `BaseDirectory` and project name, so saving to a new path writes no project file there while `DiskLocation` is repointed to it; done when Save As to a new directory produces a project that loads. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PROJECT-SERVICE] `PreviewResourceDeletion`'s arranger check (`All(x => removedDict.ContainsKey(linkedResource) || x.Source is null)`) is always true, so any element on a removed data file deletes the whole arranger; done when an arranger with elements on two data files survives deleting one of them. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PROJECT-SERVICE] `ApplyResourceDeletion` with LostPalette gives the default palette to every indexed element, including those on surviving palettes; done when only elements whose palette was removed change. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PALETTES, LIB-PROJECT-FORMAT] `SerializationMapperExtensions.MapToModel` never advances `i` for a `ScatteredColorSource` (or an unknown source type), so mapping such a palette loops forever; done when the scattered source is removed from the format (see Feature gaps) or the loop fails cleanly, with a test. ([proposal](changes/project-format-freeze.md))
- **1.0** [LIB-PALETTES] `Palette.SavePalette` writes file colors but nothing flushes the data source, though the `NotifyDataWritten` doc comment says palette saves flush; done when a palette save is on disk when Save returns and the comment matches. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-COLORS] `ColorConverterNes.ToForeignColor` casts the master palette's `ColorRgba32` to `ColorNes`, so every native→NES conversion throws `InvalidCastException` (breaks `SetNativeColor` on NES palettes and loading an NES-model global palette); done when native→NES returns the matching `ColorNes` index and a test sets a native color on an NES palette. ([proposal](changes/library-fixes-1-0.md))
- **1.0** [LIB-PROJECT-FORMAT] WAL recovery breaks if the crash falls after `File.Move` and before the journal is marked: the op is pending with no staging file, rollback only touches completed ops, so the target keeps new content and its `.bak` is orphaned; a failed commit also leaves `.bak` files; done when recovery restores every target that has a backup. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PROJECT-FORMAT] The reader reads a filesource's `bitoffset` from the palette element instead of the filesource, so it throws when present; done when a filesource with `bitoffset` loads with that offset. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-PROJECT-FORMAT] A project with a `root` attribute does not round-trip: the writer stores data file paths relative to the project file's directory and locates the project file under the base directory, while the reader resolves both against root; done when such a project saves and reopens unchanged. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-PROJECT-FORMAT] `ProjectForeignColorSourceModel.ResourceEquals` checks for `ProjectNativeColorSourceModel`, so palettes with foreign colors are rewritten on every save; done when an unchanged foreign-color palette is not rewritten. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-PALETTES] The project writer collapses evenly spaced file sources into one range without comparing endianness; done when mixed-endian runs stay separate and a round-trip test passes. ([proposal](changes/project-format-round-trip.md))
- [LIB-PALETTES] In a ProjectXml palette a project-native source is returned as its own foreign color (`ColorRgba32`) whatever the model, and its native color is never quantized, unlike global palettes; done when both storage kinds behave the same, or a decision records the difference.
- [LIB-PALETTES] `SetNativeColor` keeps the native color unquantized (BGR15 shows 255, not 248) until `Reload`; done when it stores the round-tripped color, or a decision records why not.
- **1.0** [LIB-PROJECT-SERVICE] Deleting a data file node never disposes its `FileDataSource`, so the file stays locked until the project closes; done when the file can be deleted or moved right after the node is deleted. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PROJECT-SERVICE] `CreateNewProject`'s duplicate check compares tree names to file paths and never matches; it also overwrites an existing project file and throws when the directory is missing; done when creating over an existing file fails with a result. ([proposal](changes/project-service-integrity.md))
- [LIB-PROJECT-SERVICE] `AreResourcesInSameProject` returns true when neither resource is in any open tree; done when it returns false.
- **1.0** [LIB-SERVICES] UI bootstrap resolves `_palettes`, `_codecs`, `appsettings.json` and the other defaults against the working directory, the CLI against the executable directory; done when the UI started from another directory loads its settings and codecs. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-ARRANGERS] Rotating a mirrored element turns it the opposite way on screen: render applies rotation then mirror, but `TryRotateElement` ignores the mirror; done when a test shows H-mirror then Rotate Left renders like rotating the mirrored pixels counter-clockwise. ([proposal](changes/project-format-freeze.md))
- [LIB-ARRANGERS] The `SequentialArranger` constructor and `ArrangerBuilder` set `ActivePalette`, but element codecs keep the factory default palette, and `ChangeCodec` ignores `ActivePalette`; cloning a sequential arranger likewise loses the palette; done when a new, re-codec'd or cloned sequential arranger renders with its palette without a `ChangePalette` call.
- [LIB-ARRANGERS] `GetInitialSequentialFileAddress` indexes `ElementGrid[X, Y]` instead of `[Y, X]` (latent while every layout starts at (0,0)); done when a layout whose first cell is not (0,0) moves correctly.
- [LIB-ARRANGERS] A clone rectangle that is not element-aligned drops the last covered column or row; done when the element span is computed from both edges.
- **1.0** [LIB-IMAGES] `ImageCopier.CanRemapByExactIndex` never checks that source indices fit the destination codec; done when it fails for a source index ≥ 2^destDepth. ([proposal](changes/library-fixes-1-0.md))
- [LIB-IMAGES] `DirectImage.SaveImage` and its save-conflict analysis assume the image starts at the arranger origin, so a sub-rectangle image saves the wrong pixels or throws; `CanSetPalette` also reads arranger coordinates from a sub-rectangle image; done when partial DirectImage saves and `CanSetPalette` with Left/Top ≠ 0 are correct and tested.
- [LIB-IMAGE-IO] Indexed export of a sub-rectangle image fails because the adapter sizes the PNG from the arranger; done when it exports the rectangle or rejects it clearly.
- [LIB-CODECS] `PatternList` accepts `mapIndex == patternSize` (`>` should be `>=`), and the chunky symbol-count check allows one occurrence too many; done when tests reject both.
- **1.0** [LIB-COLORS] `ColorParser` parses `#40`–`#FF` as NES and throws `ArgumentOutOfRangeException` instead of returning false; done when it returns false, with a test. ([proposal](changes/library-fixes-1-0.md))
- **1.0** [LIB-DATASOURCE] `BitAddress.Equals(object)` casts its argument, so comparing to a non-`BitAddress` throws; done when it returns false. ([proposal](changes/library-fixes-1-0.md))
- [LIB-DATASOURCE] A zero-bit read at a byte-aligned address throws `IndexOutOfRangeException`; done when it returns an empty array.
- [LIB-DATASOURCE] A bit-unaligned write whose last partial byte is past EOF merges into `0xFF`, so untouched bits become 1; done when they are 0, or the write is refused.
- **1.0** [LIB-DATASOURCE] A file with the read-only attribute cannot be opened, because `FileDataSource` always requests ReadWrite; done when it opens for reading and writes fail with a clear error. ([proposal](changes/library-fixes-1-0.md))

### UI

- **1.0** [UI-SHELL] `App.Desktop_ShutdownRequested` is `async void` and sets `e.Cancel` after awaiting, and the window has no `Closing` handler, so closing with the X may skip the save prompt or ignore Cancel; done when closing with a modified editor prompts and Cancel keeps the app open with edits intact. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-GRAPHICS-EDITOR] Saving after Resize Arranger copies the working elements into a project arranger that was never resized: larger sizes throw "Save Error" after the pixels are written, smaller ones keep the old size; done when resize → save → reopen shows the new size without an exception. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [UI-GRAPHICS-EDITOR] In Draw mode on a sequential arranger, navigation keys (arrows, PageUp/PageDown, Home/End, +/−) move the view and re-decode from source, wiping visible edits while the editor stays modified; undo then replays them at the new offset; done when navigation is refused or prompts while modified, or is limited to View mode. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [UI-DRAWING] Pressing Ctrl or Shift during a Pencil stroke sends the release to the temporary Color Picker, so the stroke never ends and the next stroke overwrites its history; done when every stroke that changed pixels is recorded regardless of modifier keys. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [UI-SHELL] Escape on a dialog with no cancel option (every alert, e.g. About) only animates the overlay out; the mediator stays open, hotkeys stay suspended and the awaiting command never finishes; done when Escape closes it, `HasOpenDialog` is false and editor hotkeys work again. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-EDITORS] Closing a tab with its X never updates `ActiveEditor`, so after the last tab closes the Edit menu, File → Close/Save <name> and hotkeys stay bound to the closed editor; done when closing the last tab leaves `ActiveEditor` null and closing another activates a remaining editor. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-EDITORS] `DockFactory` removes a closed editor's tab only from the main document dock, so a tab floated into its own window stays open after File → Close, a tree delete or a project close; done when the tab closes wherever it is docked. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-GRAPHICS-EDITOR] In a sequential editor's View mode the palette combo never recolors the view (the `ChangePalette` call in `GraphicsEditorViewModel.View.cs` is commented out); done when choosing a palette re-renders the view with it.
- **1.0** [UI-SHELL] Help → About reads `FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)`; `Location` is empty in the single-file published build, so About throws there; done when About shows the version in the published build. ([proposal](changes/release-1-0.md))
- **1.0** [UI-DRAWING] Indexed Pencil writes by exact color, so with duplicate palette colors it writes the first matching index, while history replays the chosen index; done when Pencil writes the chosen index or history records the written one. ([proposal](changes/graphics-editor-edit-safety.md))
- [UI-ARRANGING] Ctrl+click single-cell select and Shift+hover single-element select are shadowed by the temporary Pick Palette (Arrange) or Color Picker (Draw) modifier tools; done when each gesture does one documented thing.
- **1.0** [UI-ARRANGING] A dropped pixel paste can be applied in View or Arrange mode (Enter or Shift-drop) without the Draw-mode switch Ctrl+V does, and an element drop onto a sequential arranger is applied as pixels; done when drop-applied pixel pastes follow the Ctrl+V mode rules. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [UI-DRAWING] Color Picker on an element whose palette is not in the editor's list (e.g. pasted elements) sets the active palette to null, emptying the swatches and disabling Pencil; done when the active palette never becomes null after picking. ([proposal](changes/graphics-editor-edit-safety.md))
- **1.0** [UI-PALETTE-EDITOR] Opening a palette with a `ScatteredColorSource` throws `NotSupportedException`; done when it opens read-only or shows an alert. ([proposal](changes/project-format-freeze.md))
- **1.0** [UI-EDITORS] Renaming a data file does not retitle sequential editors opened from it (rename matches `Resource`, not `OriginatingProjectResource`); done when it does. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-PROJECT-TREE] `ResourceRemovalChangesViewModel` lists changed-but-kept resources (lost element or palette) under removed, and the "Changed Items" section never appears; done when they appear only under Changed Items. ([proposal](changes/project-service-integrity.md))
- **1.0** [UI-PROJECT-TREE] Accepting Rename... with the name unchanged fails with "already contains a node named X"; done when it closes with no alert. ([proposal](changes/resource-name-validation.md))
- **1.0** [UI-PROJECT-TREE] Add New Folder... fails when "New Folder" already exists, though the service's doc comment says the name is augmented; done when a second one creates a unique name, or asks for one. ([proposal](changes/resource-name-validation.md))
- [UI-GRAPHICS-EDITOR] Ctrl+W (fit) sets the zoom without clamping to 0.25–32x; done when it respects MinZoom/MaxZoom.
- [UI-GRAPHICS-EDITOR] Inspect Element prints "FileOffset 0x1A." with a trailing dot when the bit offset is 0; done when the dot appears only with a bit offset.
- [UI-GRAPHICS-EDITOR] A VM's `OnImageModified` callback is never cleared when a view releases it, so it can repaint a view that no longer shows it (only a null-VM paint guard covers it); done when the view clears its callbacks on DataContext change.

### CLI

- **1.0** [CLI-COMMANDS] `WithParsed(async ...)` is async-void: Main reads the exit code before the handler finishes whenever opening truly yields (a leftover WAL journal), so the run exits −1 mid-command; done when Main awaits the handler and a run with a journal returns the real code. ([proposal](changes/cli-1-0.md))
- **1.0** [CLI-COMMANDS] `Exporter` tests `Directory.Exists(path)` before `CreateDirectory` (inverted), so a missing or nested export directory throws and the run exits −2 with keys unexported; done when export into a fresh tree writes nested PNGs and exits 0. ([proposal](changes/cli-1-0.md))
- **1.0** [CLI-COMMANDS] `ExportHandler` and `ExportAllHandler` ignore `ExportArranger`'s result and always return 0, so −7 is never used, and unknown color types write nothing yet print "Completed successfully"; done when a failed or unsupported key yields −7 with a message naming it. ([proposal](changes/cli-1-0.md))
- **1.0** [CLI-COMMANDS] `--log` has no effect (the static logger is built from `DefaultLogFileName`; the computed path is never read), and the default log goes to the current directory, not the app directory as documented; done when `--log x.txt` writes there and the default matches the doc. ([proposal](changes/cli-1-0.md))
- **1.0** [CLI-COMMANDS] `--help` and `--version` exit −3 and log "Operation failed due to invalid command line options"; done when both exit 0 with no error line. ([proposal](changes/cli-1-0.md))

## Missing validation

- **1.0** [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Nothing rejects empty names, `/` or invalid file-name characters on add, create-folder, rename or new arranger from selection, and name checks are case-sensitive while Windows paths are not (adding `Data` beside `data` overwrites `data.xml`); done when each path refuses such names with a message. ([proposal](changes/resource-name-validation.md))
- **1.0** [CLI-COMMANDS] Read-only arrangers fail the import instead of being skipped and reported, and halt `importall` at the first one; done when `import`/`importall` skip them with a message and continue. ([proposal](changes/cli-1-0.md))
- [LIB-PROJECT-SERVICE] `SaveResourceAsync` has no standalone guard; called with a standalone root it would write XML over the ROM (unreachable from today's UI); done when it fails for standalone trees.
- [LIB-PROJECT-SERVICE] `OpenProjectFileAsync`'s already-open check compares raw path strings, so the same project opens twice via a relative path or other casing; done when it normalizes like `OpenDataFile`.
- [LIB-PROJECT-SERVICE] `CanMoveNode` and `GetContainingProject` throw for nodes outside every open tree; done when `CanMoveNode` returns a failure.
- **1.0** [LIB-PROJECT-FORMAT] An unresolved data file key fails the load without naming the key or arranger; an unresolved palette key silently falls back to the default palette; done when both name the key and resource. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-PROJECT-FORMAT] Unparseable `nativecolor`/`foreigncolor` values are dropped silently, shifting later indices; done when the load fails naming the entry. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-PROJECT-FORMAT] `version` is parsed with the current culture; done when it uses the invariant culture. ([proposal](changes/project-format-freeze.md))
- **1.0** [LIB-SERVICES] A partial settings file yields null settings instead of merged defaults; done when missing keys take defaults. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-CODECS] Startup stops on a malformed XML codec file (`XmlException`), a plugin DLL that fails to load (`ReflectionTypeLoadException`) or a throwing plugin constructor; done when each is logged and skipped and the rest load. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-PALETTES] `CreatePaletteStore` catches only `InvalidDataException`: a missing palette file or malformed JSON stops startup, and with no global palette `First()` throws; done when each case is logged clearly and tested. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-ARRANGERS] `ElementLayoutService.ReadLayout` lets `JsonException` escape and `CreateElementStore` throws on a duplicate layout name, crashing bootstrap; done when both are logged and skipped. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-CODECS] A C# or plugin codec named like an XML codec silently shadows it, and `GetRegisteredCodecNames` lists the name twice; done when the collision is reported and names are unique. ([proposal](changes/startup-robustness.md))
- [LIB-CODECS] A planar pattern count mismatch or empty pattern throws `ArgumentException` while other pattern errors return a failed result; done when all return failed results.
- [LIB-CODECS] The XML reader rejects a width or height of 1 while the schema accepts any positive integer; done when they agree.
- [LIB-PALETTES] `SetForeignColor` accepts a color of any model, and negative indexes throw `IndexOutOfRangeException`; done when both are validated.
- **1.0** [LIB-PALETTES] Only the UI bootstrapper checks the NES master palette has ≥ 64 entries; done when `CreatePaletteStore` rejects a short one. ([proposal](changes/startup-robustness.md))
- [LIB-COLORS] Match indices are bytes, so palettes over 256 entries wrap; done when entries are limited to 256 or indices widened.
- [LIB-ARRANGERS] `SetElement` accepts x == Width or y == Height (throws `IndexOutOfRangeException`); `TryRotateElement` checks bounds with `>` and `TryMirrorElement` not at all; scattered `SetElement` does not check the element's pixel size; `SequentialArranger.Move(BitAddress)` accepts negative addresses and `Resize` sizes below 1; done when each is refused with an argument error or failed result.
- [LIB-ARRANGERS] `TileLayout` does not validate its pattern (count vs `TilesPerPattern`, cells within width and height); done when invalid layouts are refused on load.
- [LIB-IMAGES] The IndexedImage and DirectImage sub-rectangle constructors do not bounds-check, and FloodFill clip bounds larger than the image throw; done when both produce argument errors or are clipped.
- [LIB-IMAGE-IO] `ImageImportPreview.Commit` does not check `CanCommit` (the UI and CLI do); done when it refuses, or a decision confirms it.
- [LIB-IMAGE-IO, LIB-ARRANGERS] Import reports pixels on elements past the end of their source as Changed, and save-conflict analysis reports such elements as Modified, though neither is ever written; done when they are excluded.
- **1.0** [UI-PROJECT-TREE] The Add New Palette and Add New Scattered Arranger dialogs compute `CanAdd`/`ValidationErrors` but never gate Accept on them (`AddScatteredArrangerViewModel.ValidateModel` is never called); done when Add is disabled with the error shown for empty, whitespace or duplicate names. ([proposal](changes/resource-name-validation.md))
- **1.0** [UI-PROJECT-TREE] New Project from Existing File... does not detect a file already open as a data file in a project, and creates a second project over it; done when it selects the existing node or refuses. ([proposal](changes/resource-name-validation.md))
- **1.0** [UI-ARRANGING] The Resize dialog accepts zero or negative sizes, and a same-size resize still records history and marks the editor modified; done when Resize is disabled below 1 and a same-size resize is a no-op. ([proposal](changes/graphics-editor-edit-safety.md))
- [UI-ARRANGING] Associate Palette adds duplicates, sizes the palette by entry count instead of codec color depth, and `Palettes.First()` throws on an empty list; done when duplicates are skipped and an empty list is handled.
- [UI-ARRANGING] Delete records history and marks the editor modified when every covered element is already empty; done when it is a no-op.
- **1.0** [UI-IMAGE-IO] Export asks for a file name before checking for a missing data file and does not catch PNG write failures; import `OnAccepted` does not catch commit failures; done when the missing check comes first, and failures show an alert with the import dialog staying open. ([proposal](changes/shell-editor-lifecycle.md))
- [UI-PALETTE-EDITOR] Change Color Model needs a data source even when every source is a project color; done when such a palette can change model.
- **1.0** [CLI-COMMANDS] A missing data file surfaces as an exception (−2) rather than a message naming the arranger and file; done when export/import report it per arranger and fail with −6/−7. ([proposal](changes/cli-1-0.md))

## Undecided questions

- **1.0** [LIB-PROJECT-FORMAT] Stable resource keys or path keys. Path keys mean a rename or move rewrites every referencing file, and a rename done by hand in Explorer leaves references dangling. The alternative is a project-unique, human-readable `key` assigned at creation and never changed (keys drift from display names; copied files duplicate keys). The analysis is in the spec's Decisions and Open items. Decide before the format freeze, since the migration hook can assign keys to 0.9 projects; done when one is chosen and recorded as a Decision. ([proposal](changes/project-format-freeze.md))
- **1.0** [ARCHITECTURE] Startup tolerance for shipped resource folders (`_codecs`, `_plugins`, `_palettes`, `_layouts`) is inconsistent: invalid codecs and layouts are logged and skipped, malformed codec XML, plugin load errors and missing or malformed palettes stop startup; done when a project-wide rule is recorded in ARCHITECTURE.md and the Missing validation items above follow it. ([proposal](changes/startup-robustness.md))
- **1.0** [LIB-PROJECT-SERVICE] Add, delete-apply rewrites and create-project use plain `File.WriteAllText`, outside the WAL; done when they go through a transaction or a decision records why not. ([proposal](changes/project-service-integrity.md))
- [LIB-PROJECT-SERVICE] Whether `ApplyResourceDeletion` should report a kept non-empty folder as a failure although its nodes were removed; done when decided.
- **1.0** [LIB-PROJECT-SERVICE] Concurrent saves are not serialized (each call creates a writer with its own lock); done when decided. ([proposal](changes/project-service-integrity.md))
- **1.0** [LIB-PROJECT-FORMAT] A malformed or foreign `*.xml` anywhere under the project directory fails the whole load, and other directories (such as `.git`) become folders; done when decided and specified. ([proposal](changes/project-format-freeze.md))
- [UI-PROJECT-TREE] A data file's missing state is read only on project open, so it does not update if the file disappears or reappears mid-session; done when refreshed, or the decision to leave it is recorded.
- [UI-PALETTE-EDITOR] "Duplicate to project" for global palettes (PLAN Milestone 4, marked done) does not exist; the closest is the Add Palette dialog's template option, and no UI path opens a global palette in the editor, so read-only mode looks unreachable; done when either a Duplicate command and a way to open global palettes exist, or UI-PALETTE-EDITOR-090/091 are struck.
- **1.0** [UI-IMAGE-IO] Export silently exports the saved state, ignoring unsaved editor changes (import prompts); done when export prompts or says so. ([proposal](changes/shell-editor-lifecycle.md))
- **1.0** [UI-SHELL] The File menu has no Save All, and Save Project As exists only in the project context menu; done when a decision is recorded and the items exist if wanted. ([proposal](changes/shell-editor-lifecycle.md))

## Feature gaps

### For 1.0

- **1.0** [LIB-PROJECT-FORMAT] Schema versioning and migration: `version="0.9"` is written and parsed but never compared; bump to 1.0, reject newer versions with a clear message, and add a migration hook for older ones; done when a higher-version project fails clearly and a 0.9 project loads through the hook. ([proposal](changes/project-format-freeze.md))
- **1.0** [LIB-PROJECT-FORMAT] Remove the scattered color source from the 1.0 schema (an empty class: the reader ignores it, the writer throws or loops) rather than freezing a stub into the format; it can return as an additive change; done when `ScatteredColorSource` cannot reach the reader, writer or palette editor. ([proposal](changes/project-format-freeze.md))
- **1.0** [CLI-COMMANDS] Import options `--match exact|nearest|nearestrgb`, `--max-distance` and `--transparent-index0`, mapped onto `ImageImportOptions` (today always `Default`); done when each changes the staged result. ([proposal](changes/cli-1-0.md))
- **1.0** [CLI-COMMANDS] Load plugin codecs (`CreatePluginService` is commented out in `Program.cs`; the constructor blocker is fixed); done when a project using a sample plugin codec exports. ([proposal](changes/cli-1-0.md))
- [UI-SHELL, LIB-PROJECT-SERVICE] Command-line and OS integration (optional for 1.0): `args` are passed to Avalonia but never read, and there is no window drop handler; done when a non-`.xml` path on the command line, or a file dropped on the window, opens as a standalone file (drop is a manual check).
- **1.0** [UI-SHELL] The window title is "TileShop.Avalonia"; done when the taskbar shows "TileShop". ([proposal](changes/shell-editor-lifecycle.md))
- [UI-SHELL] Status-bar Indefinite and Reset messages are handled but never sent; done when a sender exists or the values are removed.

### P1

- [LIB-CODECS] Direct-color XML codecs: the schema accepts `colortype="direct"`, `CodecFactory` throws `NotSupportedException`.
- [LIB-CODECS] Read-only compression support: [change proposal](changes/compression-support.md).
- [LIB-PROJECT-FORMAT, UI-GRAPHICS-EDITOR] Sequential arrangers as project resources ("save view as arranger"; deferred to 1.1); done when one saves and reloads. Unblocks persisting the element layout (P2, deliberately not done in Milestone 9) and per-file view state for standalone files.
- [LIB-PALETTES, UI-PALETTE-EDITOR] Remaining palette file formats: RIFF `.pal`, `.act`, `.hex`, and a JSON writer (JASC `.pal` and `.gpl` exist).

### P2

- [LIB-CODECS] Platforms: Game Boy/GBC 2bpp (explicit entry), Master System 4bpp, PC Engine/TG16, N64 CI4/CI8/IA/I, NDS, Saturn, Neo Geo sprites, Atari/Lynx, WonderSwan; GBA/NDS BGR555 direct bitmap codecs.
- [LIB-CODECS] XML format extensions: tile stride/padding, bit order and endianness, per-tile header bytes (variable-width fonts currently need a plugin).
- [LIB-ARRANGERS] Bit-wise sequential offsets: tiled elements and absolute moves are bit-addressed, but Single-layout stepping (TODO in `SequentialArranger`) and its row/column/page steps are not; done when Single-layout round-trip tests pass at a non-byte-aligned offset.
- [LIB-COLORS] Color models: RGB24, ARGB32, RGB565, TG16 GRB333, N64 IA/I, grayscale.
- [LIB-PALETTES] Palette sub-banks (a 16-color sub-palette of a 256-color palette per element).
- [UI-PALETTE-EDITOR] Reorder, duplicate and bulk hue/brightness adjustment (reorder deliberately left out of 1.0: it changes every arranger using those indices).
- [UI-DRAWING] Line, rectangle, ellipse, eraser, brush size, replace color; single-key tool hotkeys and `[`/`]` to step palette indices.
- [UI-ARRANGING, LIB-IMAGES] Magic wand, lasso, pixel-level flip and rotate of a selection (element mirror/rotate are display attributes).
- [UI-ARRANGING] OS clipboard for images (the clipboard is a private static), so pixels can be exchanged with external editors.
- [UI-GRAPHICS-EDITOR] Bookmarks and back/forward navigation in sequential browsing.
- [UI-IMAGE-IO, LIB-IMAGE-IO] Export from sequential views and selections (blocked in the library by the sub-rectangle export bug); done when a View-mode editor can export a PNG.
- [LIB-IMAGE-IO] Export formats: BMP, indexed GIF, palette sidecar file.
- [UI-IMAGE-IO] Expose AlphaThreshold and MaxDistance in the import dialog.
- [LIB-IMAGES, LIB-PROJECT-FORMAT] WAL protection for binary pixel writes (`SaveImage` writes sources in place); done when an interrupted pixel save is recovered.
- [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Duplicate resource; "New Sequential Arranger" in the tree.
- [CLI-COMMANDS] Dry-run and report output (import report without committing), `--json` for print/import/export, key filters and globs (`export "Sprites/*"`), palette export/import verbs.

### P3

- [LIB-CODECS] Public span-based bit reader/writer replacing the internal `PackedBits` (BitStream is now used only by samples and tests); codec authoring UI with live preview; codec auto-detection across codecs at an offset.
- [LIB-PALETTES] Palette generation (quantize an image, dithering on import); color-cycling preview.
- [UI-GRAPHICS-EDITOR] Hex view side panel, pattern search from an image or bytes, minimap, zoom percentage display and presets.
- [UI-DRAWING] Undo history panel, layers or reference overlay, text tool using a font arranger.
- [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Project tree search/filter; project metadata (description, ROM checksum, notes); tilemap/nametable resources; IPS/BPS patch output instead of writing in place.
- [CLI-COMMANDS] Project authoring verbs (`new`, `add-datafile`, `add-palette`, `add-arranger`), a batch manifest, a scripting host, `dotnet tool` packaging.
- [UI-SHELL] Customizable keybindings; save and restore the window and dock layout; localization; a plugin manager (decided against for 1.0: About lists plugin codecs).

## Untested behavior worth a test

- **1.0** [LIB-PROJECT-FORMAT] Project XML round-trip using `ImageMagitek/_xmlprojectsamples/*.zip`: every resource type, nested folders, mixed palette sources, mirror/rotation, legacy codec names, a WAL-recovered save; plus reader failures (schema error, unknown codec, indexed arranger with a direct codec, unresolved data file key, palette-key fallback). ([proposal](changes/project-format-round-trip.md))
- **1.0** [CLI-COMMANDS] No handler or exit code is tested: a fixture-project test covering 0/−3/−5/−6/−7, overwrite, `-f`/`-r` and nested export. ([proposal](changes/cli-1-0.md))
- **1.0** [LIB-PROJECT-SERVICE] Save As; `CreateNewProjectWithExistingFileAsync` (success and both failures); `OpenProjectFileAsync` with a leftover journal; `CloseProjects`; project-root rename; delete cascades (lost-palette fallback, arranger on a removed data file, the data file itself kept); rename rollback when the project write fails for a non-folder resource. ([proposal](changes/project-service-integrity.md))
- [LIB-DATASOURCE] Past-EOF read throws `EndOfStreamException`; overflowing a fixed-capacity `MemoryDataSource` throws `NotSupportedException`; `Flush` and `Write` never raise `DataWritten`; unbounded sources grow on write.
- [LIB-CODECS] XML schema and semantic validation failures; unknown name throws `KeyNotFoundException`; direct-color XML throws; pattern codecs ignore the requested size; each codec gets its own format clone; C# codecs win over XML on name; `AddOrUpdateCodec` rejects abstract or non-codec types; `CloneCodec`; the bootstrapper's codec and plugin paths.
- [LIB-COLORS] RGB15, BGR6, NES and RGBA32 conversion; BGR9 to-foreign rounding; hex format and parse for every model; factory `NotSupported`/`ArgumentException` paths; distance ties pick the lowest index; `GetIndexByNativeColor` throwing.
- [LIB-PALETTES] 3-byte and big-endian file colors; `ZeroIndexTransparent` raises `Changed` only on a real change; global `SavePalette` returns false; JSON `zeroIndexTransparent` default (true) and `JsonException` cases; `PaletteStore` default and NES loading.
- [LIB-ARRANGERS] `SequentialArranger.Move` per move type including clamping; `ChangeElementLayout` rounding and pattern order (2x2 H vs 2x2 V addresses); `ElementCopier` (`ElementCopierTests` is entirely commented out), including paste cropping and the cross-project refusal; `CloneArranger` (full, sub-rectangle, Single-layout restriction); `UnlinkResource`; ElementStore bootstrap with a missing folder or bad file.
- [LIB-IMAGES] `ImageCopier` (`ImageCopierTests` has one empty method without `[Fact]`): each `CopyPixels` overload, the blank-element failure, operation order; `SaveImage` raising `NotifyDataWritten` once per distinct source; `CanSetPixel`/`SetPixel(color)`/`GetPixelColor`; FloodFill not crossing palettes.
- [LIB-IMAGE-IO] RGBA fallback export (over 256 combined entries, index past its slot, empty-cell transparency), empty cells exporting as index 0, `LoadImage` and `Prepare(path)` failure results.
- [UI-SHELL] `HotkeyService` scope, suspension, text-box plain-key deferral and first-match-wins; `StatusViewModel` Short/Indefinite/Reset; recent list rules (top insert, max 8, standalone skipped, missing filtered at start); `UserPreferencesStore` load fallback and atomic save; NES override fallback in the bootstrapper. Several need logic extracted from the ViewModel first.
- [UI-EDITORS] `OpenEditor` dispatch, the codec-by-extension fallback chain, the missing-source gate, `ResourceChanged` coalescing, `ConfirmRemovalAsync` prompt selection (needs a slimmer constructor or an extracted helper).
- [UI-PROJECT-TREE] Root open/close/convert keep `Projects` in sync; Move picker filtering and `CanDropNode`.
- [UI-GRAPHICS-EDITOR] Mode-switch save prompt (Yes/No/Cancel) and the read-only/standalone gating properties, with a fake `IInteractionService`.
- [UI-EDIT-HISTORY] VM wiring: `IsModified` after undo/redo, CanUndo/CanRedo notifications, history cleared on save, discard and `ReloadFromSource`.
- [UI-DRAWING] `CanRemapColors` (single palette, read-only, empty region); clip enforcement in Pencil and flood fill at VM level.
- [UI-IMAGE-IO] `ImportImageViewModel` enablement and blocking reason (needs `ImageSharpFileAdapter` injected first).
- [UI-PALETTE-EDITOR] Invalid source rows block Save and mark the editor modified; an out-of-range file source is not applied; paste parsing (internal exact copy, OS hex tokens, error); read-only gating; `CommittedModel` set and cleared with history; a new edit clears redo and a throwing edit rolls back with no step; `ChangeColorModelViewModel` preview errors for table models.

## Dead code and cleanup

- **1.0** Delete `ImageMagitek.Services/Actions` (`MagitekActions`, `IMagitekAction`, `IActionHistory`; no references), `ImageColorAdapter` (every member throws), `PixelRemapOperation.RemapByAnyIndex` with its empty branches and the commented `CanRemapByAnyIndex` (it silently does nothing), and the private, never-called `ProjectService.FindStaleKeyResources`. ([proposal](changes/release-1-0.md))
- [UI-SHELL, UI-ARRANGING] `Styles/Arranger.axaml`: `Rectangle.selection`, `Rectangle.paste`, `Rectangle.animatedBorder` and `.arrangerDrag` have no users (only `.arrangerDrop` is used), which makes `editSelectionFillBrush` and `pasteSelectionFillBrush` dead; `gridLineBrush` and `separatorBrush` are unreferenced.
- [UI-SHELL, UI-IMAGE-IO] Remove `ShellView.LoadLayout`, `SyncDialogExtensions`, `DialogHost.ShowDialogAsync` (no caller), `IStateViewDriver` (no implementer), `MenuViewModel.ExportArrangerToImage`/`ImportArrangerFromImage` (their menu is commented out), the commented-out Arranger and Plugins menus, and the commented-out `OnUnhandledException`.
- **1.0** [UI-EDITORS] Remove the stale commented-out lines in `CloseEditor`/`OpenEditor`; File → Close <name> saves the project twice, unlike the tab-close path; done when both do one save. ([proposal](changes/shell-editor-lifecycle.md))
- [UI-PROJECT-TREE] Remove `ProjectNode_KeyDown`, `BottomUpTraversal`, `SortPriority`, the single-argument `ResourceRemovalChangesViewModel` constructor and its commented-out members, and the double KeyDown registration (XAML plus tunnel); Enter and double-click must still work.
- **1.0** [UI-GRAPHICS-EDITOR] `GraphicsEditorViewModel` redeclares `ActivityMessage`/`PendingOperationMessage`, hiding the base properties the status bar binds to; `ArrangerRenderer` takes an unused arranger; save errors show the stack trace to the user; commented-out code in `Drawing.cs`, `View.cs` and `GraphicsEditorViewModel.cs`. ([proposal](changes/shell-editor-lifecycle.md))
- [UI-DRAWING] Unused `ActiveColor`, `ActiveColorIndex`, `SetPrimaryColorIndex`/`SetSecondaryColorIndex`, the `SetPrimaryColor`/`SetSecondaryColor` commands, `SetSelectToolMode`, `SetApplyPaletteMode`, `NotifyColorTypeChanged`; each indexed Pencil pixel sends an empty status message.
- [UI-EDIT-HISTORY] Payload fields on the Mirror/Rotate/Delete/Resize history actions are unused since the snapshot model; `ApplyHistoryAction` is never called from outside.
- [UI-PALETTE-EDITOR] `PaletteEditorViewModel.ApplyHistoryAction` (throws, no callers), `EditableColorBaseViewModel.SaveColor` (no callers), `ScatteredColorSourceModel` (unused); the project rewrite in `DiscardChangesAsync` is probably redundant given `CommittedModel`, and Save rewrites the resource XML when nothing is pending; the status ActivityMessage is never cleared.
- [LIB-ARRANGERS] The commented `UnlinkPalette` in `Arranger.cs`, the duplicate codec-name check in `SequentialArranger.SetElement`, the dead `TileLayout is null` branch; `ArrangerBuilder`'s sequential path ignores `WithElementLayout` and `WithPixelColorType`.
- [LIB-IMAGES] `CopyPixelsDirect` on an indexed arranger throws `NotImplementedException` where the indexed variant throws `ArgumentException`; `ImageBase.Right`/`Bottom` doc comments say inclusive, the values are exclusive.
- [LIB-PALETTES] `Palette.HasAlpha` is never set and unused; `Palette.GetColor` is unused and swaps R and B.
- **1.0** [LIB-DATASOURCE] The stale `NotifyDataWritten` doc comment ("palette saves also flush"). ([proposal](changes/project-service-integrity.md))
- [LIB-CODECS] `PatternList._encodePattern` is a public mutable field.
- [LIB-PROJECT-TREE] `ProjectResourceBaseComparer` (internal, unused) and `IProjectResource.ShouldBeSerialized` (never read).
- **1.0** [LIB-PROJECT-FORMAT] `XmlProjectWriter.AddResourceToXmlTree`; `XmlProjectReader.LocateResourceOnDisk` and `LocatePathKey`; the reader branches for `scatteredcolor`/`import`/`export` (the schema rejects them); `ResourceModel.Parent` and `ChildResources`; unused `IProjectReader.Version`/`IProjectWriter.Version`; the commented-out `ScatteredColorSourceModel` mapping; the schema's color patterns use `^…$`, which XSD treats as literal characters. ([proposal](changes/project-format-round-trip.md))
- **1.0** [LIB-SERVICES] The `.tim` extension association uses the retired codec name `PSX 4bpp`; done when it names `PSX 4bpp Flow`. ([proposal](changes/library-fixes-1-0.md))
- **1.0** [CLI-COMMANDS] Verb help texts are wrong: `exportall` says "all project resources" (only scattered arrangers), `import`/`importall` say "skipping resources that cannot be located" (only with `-f`/`-r`). ([proposal](changes/cli-1-0.md))

## Release engineering and documentation

- **1.0** [ARCHITECTURE] CI: `.github/workflows/ci.yml` runs a Nuke `build.cmd` that no longer exists, only on manual dispatch, with no tests; done when push and PR run `dotnet build` and `dotnet test`, and a tag job runs `publish.ps1` and attaches the artifacts. ([proposal](changes/release-1-0.md))
- **1.0** [CLI-PUBLISH] The `.pubxml` profiles target net7.0 and `FolderProfile.pubxml.user` is orphaned; done when they are updated or deleted in favor of `publish.ps1`. ([proposal](changes/release-1-0.md))
- **1.0** [CLI-PUBLISH] `publish.ps1` does not stop on a failed build or test, `-ReadyToRun` is unused, the `$CliVersion` null fallback sits after its use, and a rerun fails at `Compress-Archive` without `-Force`. ([proposal](changes/release-1-0.md))
- **1.0** README: still says .NET 6 and lists Autofac, Jot and Nuke; done when the stack and dependencies are current and CLI usage (verbs, options, exit codes) is documented. ([proposal](changes/release-1-0.md))
- **1.0** Release notes for 1.0 calling out the codec behavior changes existing projects will notice (recorded in LIB-CODECS Decisions, "Codec rework behavior changes"). ([proposal](changes/release-1-0.md))
- Codec XML authoring guide and plugin authoring guide; decide whether to ship `IndexedCodecContract` as a package for plugin authors or document copying it from the test project.
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
- [UI-PALETTE-EDITOR] Shift+click and Ctrl+click on swatches; the Sources → Add flyout for all three kinds; import and export through the file pickers.
- **1.0** [UI-SHELL] The debug load button exists only in Debug builds. ([proposal](changes/release-1-0.md))
