# TileShop 1.0 Plan

Goal: take the current feature set to a dependable 1.0. Finishing and hardening what exists comes before adding anything new. A 1.0 user should never lose work, never corrupt a ROM, and never hit a control that silently does nothing.

This plan draws on [FeatureGaps.md](docs/FeatureGaps.md), which remains the long-term backlog. Every defect listed below was re-checked against the code at `c018195`. Line numbers are as of that commit.

**Out of scope for 1.0:** direct-color XML codecs, [compression support](docs/CompressionSupport.md), new platforms and color models, new drawing or selection tools, layers, tilemaps, and scripting. They stay in FeatureGaps.md.

**Order:** Milestones 1–3 come first, because they protect user data and fix the project format. Milestones 4–7 can run in parallel after that. Milestone 4 reuses the history model from Milestone 2.

---

## Milestone 1: Data safety

Exit criteria: no known path loses unsaved edits, writes through a codec that can't encode, or crashes on ordinary input.

| Item | Location |
|---|---|
| Removing a project node clears every open editor without a save prompt. Close only the affected editors, and prompt for any that are modified (`modifiedEditors` is already computed but unused). | `ProjectTreeViewModel.cs:315-317` |
| Folder rename computes `newLocation` before `node.Rename`, so the move does nothing. File rename ignores the `WriteProjectAsync` result and deletes the old file anyway, and calls `Rename` twice. | `ProjectService.cs:405-411`, `:433-449` |
| `MoveNodeAsync` rollbacks move files back even when the first move threw. Folder delete is non-recursive. | `ProjectService.cs:561`, `:590-595`, `:653` |
| Read-only gate: nothing checks `IGraphicsCodec.CanEncode`. Add an arranger-level `IsReadOnly` check, refuse writes in `IndexedImage.SaveImage`, `DirectImage.SaveImage` and `ImageImporter`, and drive `CanDraw`, pixel paste, Color Remap and Import from it. This is the "Making read-only enforceable" section of CompressionSupport.md without the `DataSource` part. | `GraphicsEditorViewModel.cs:322` |
| BGR9 crash: `ColorBgr9` unpacks 4-bit nibbles, but `ColorConverterBgr9` indexes an 8-entry table with `& 0xFE`. Values of 8 or more throw `IndexOutOfRangeException` during palette load, and 7 maps to 182 instead of 255. Pick 3-bit channels (Genesis) and make both sides agree. | `ColorBgr9.cs:17-19`, `ColorConverterBgr9.cs:23-25` |
| Reads past end of file aren't handled. An arranger near EOF or over a truncated file should render the missing area as empty, not throw or show garbage. | `IndexedImage.cs:78`, `DirectImage.cs:72` |
| `SetNativeColor(int, r, g, b, a)` writes an RGBA32 value into the foreign palette whatever its color model. | `Palette.cs:325-330` |
| Palette JSON colors that fail to parse are dropped, which shifts every later index. Fail the load with the bad entry named. | `PaletteJsonModel.cs:19-20` |
| The save-conflict handler is an empty TODO. At minimum, tell the user and don't overwrite silently. | `GraphicsEditorViewModel.cs:574` |
| Duplicate XML codec names silently overwrite each other, because the `formats` dictionary is never populated. Log the duplicate and keep the first. | `XmlCodecService.cs:32-49` |

## Milestone 2: Editing correctness

Exit criteria: every action that appears in history can be undone and redone, and tools act only in the modes they belong to.

- [x] **Undo/redo for arranger actions.** `ApplyHistoryAction` only replays Pencil, FloodFill, ColorRemap and Paste. Mirror, Rotate, ApplyPalette, DeleteElementSelection and ResizeArranger are recorded but never redone, and undo only re-clones the arranger when an ElementCopy paste is in history (`History.cs:21-49`, `:81-89`, `:111`). Pick one model for arranger-level actions: either each action stores enough to invert itself, or every arranger action snapshots the arranger. Snapshots are simpler and arrangers are small.
- [x] Color Remap calls `UndoHistory.Add` directly, so redo isn't cleared and CanUndo/CanRedo aren't notified (`ArrangerTools.cs:591`).
- [x] Delete swaps element width and height for non-square elements (`ArrangerTools.cs:387-388`).
- [x] Delete isn't mode-guarded. It fires in Draw and View and on sequential arrangers (`GraphicsEditorView.axaml:13`, `ArrangerTools.cs:371-380`).
- [x] Pencil stays in its drawing state after a stroke that changes no pixels (`Drawing.cs:117-128`).
- [x] Tests: a history test per action type (do → undo → redo → compare against the arranger and pixels after "do"). Most of this logic is ViewModel code, so extracting it from `GraphicsEditorViewModel` into a testable class may come first.

## Milestone 3: Project format freeze

Exit criteria: the 1.0 project format is versioned, round-trip tested, and loads older projects or refuses them clearly.

- [ ] **Version check and migration.** `XmlProjectWriter` writes `version="0.9"`, and `XmlProjectReader` parses it but never compares it. Bump to `1.0`, reject newer versions with a clear message, and add a migration hook for older ones (0.9 → 1.0 may be a no-op, but the hook needs to exist before 1.0 ships).
- [ ] **Relink missing data files.** A missing data file fails the whole load (`ProjectTreeBuilder.cs:73-74`). Load the project with the data file marked missing, and offer to locate it. Arrangers that reference it open as unavailable rather than crashing.
- [ ] **Project XML round-trip tests.** None exist. Cover every resource type, nested folders, palettes with mixed color sources, element mirror/rotation, legacy codec aliases, and a WAL-recovered save. Use `_xmlprojectsamples/*.zip` as fixtures.
- [ ] **Scattered color source.** It's an empty class, the reader ignores it, and the writer throws (`ColorSourceSerializer.cs:45-48`, `:134-137`). Remove it from the 1.0 schema rather than freezing a stub into the format. It can come back as an additive change.
- [ ] Decide whether 1.0 saves sequential arrangers as project resources (`ProjectService.cs:260` maps only `ScatteredArranger`). It's additive to the schema, so it can wait until 1.1 without breaking the format. **Recommendation:** defer, unless users ask for it.

## Milestone 4: Palette editor rework

Exit criteria: palette edits behave like graphics edits. They stay pending until saved, can be undone, prompt before being discarded, and every open view of a palette stays in sync.

### Problems today

- **Color edits write to the ROM immediately.** Each color's Save button calls `SaveActiveColor`, which writes the color through `SavePalette` and re-saves the project XML. Nothing is pending and nothing can be undone. The graphics editor's color flyout does the same (`GraphicsEditorViewModel.Drawing.cs:39-54`).
- **Source edits aren't tracked.** Adding, removing or editing a source never sets `IsModified`; only the index-0 transparency checkbox does. Closing the editor discards source changes without a prompt.
- **Invalid sources are dropped silently.** `CreateColorSources` skips Native and Foreign entries whose hex doesn't parse, which shifts every later index (`PaletteEditorViewModel.cs:326-335`).
- **Discard leaves the view stale.** `DiscardChanges` reloads the palette but doesn't rebuild the swatches or the active color.
- **Open palette editors go stale.** `EditorsViewModel.Receive(PaletteChangedMessage)` only re-renders graphics editors, so a flyout edit doesn't reach an open palette editor (`EditorsViewModel.cs:314-321`).
- **Undo and redo throw** `NotImplementedException` (`PaletteEditorViewModel.cs:147-160`). This isn't reachable today only because the palette editor has no Ctrl+Z binding.
- **One color at a time.** Selection is single, and there are no range operations.
- **No way to see where a color comes from.** Sources and swatches are separate lists, so finding which file offset holds color 37 means counting entries by hand.
- **The color model is fixed at creation.** A palette created with the wrong model has to be deleted and recreated.

### Rework

- [x] **One edit model.** Keep pending edits in memory in the shared `Palette`, and broadcast `PaletteChangedMessage` so graphics editors preview them live. Write to the data source and project XML only on Save (Ctrl+S). Discard calls `Reload` and rebuilds the view. Route the graphics editor's color flyout through the same path, so a palette has one modified state wherever it's edited. Close, project-node removal and app exit all prompt for modified palettes (Milestone 1).
- [x] **Undo/redo** using Milestone 2's history model. A palette is at most 256 colors plus a short source list, so snapshotting both per action is cheap.
- [x] **Sources tracked and validated.** Source edits set `IsModified`. Invalid entries show a validation error and block Save instead of disappearing. Drop the separate "Save Sources" button, since Save covers both colors and sources.
- [x] **Multi-selection:** click, Shift+click for a range, and Ctrl+click to toggle. Operations on a selection:
  - Copy and paste colors, both within TileShop and to the OS clipboard as hex text.
  - Swap two colors.
  - Fill a gradient between the first and last selected colors.

  Moving or reordering colors changes how every arranger using those indices looks, so leave it out of 1.0.
- [x] **Layout:** put the swatch grid beside the color editor instead of above it, so editing doesn't scroll the grid away. Selecting a swatch highlights the source it comes from, and shows its file offset for file-backed colors. Sources move to a collapsible section, since they're set up once and rarely touched.
- [x] **Change color model:** reinterpret the existing sources under another model, with a before/after preview of the swatches.
- [x] **Read-only global palettes:** keep them read-only, and add "Duplicate to project" so they can be used as a starting point.
- [x] **Palette file import/export:** export to `.pal` (JASC) and `.gpl`, and import either into project-native colors. Leave RIFF, ACT and HEX for later.
- [x] **Hotkeys:** Ctrl+S, Ctrl+Z/Ctrl+Y, Ctrl+C/Ctrl+V and arrow-key navigation of the grid, scoped through the existing hotkey service.
- [x] **Tests:** move the editing state (working colors, sources, selection and history) out of the ViewModel into a plain class, and unit-test edit → undo → redo → save → reload against a `MemoryDataSource`.

## Milestone 5: CLI

Exit criteria: the CLI exit code reflects what happened, and import has the same options as the UI.

- [ ] `.WithParsed(async ...)` is async-void, so the exit code can be read before the handler finishes (`Program.cs:48`).
- [ ] The directory-exists check is inverted, so nested export folders are never created (`Exporter.cs:30`).
- [ ] Export handlers ignore `ExportArranger`'s result and always return Success (`ExportHandler.cs:23,26`, `ExportAllHandler.cs:26,29`). Unknown color types write nothing without an error (`Exporter.cs:39-48`).
- [ ] `--log` has no effect: the logger always uses `DefaultLogFileName`, and `logFileName` is never read (`Program.cs:24`, `:108`).
- [ ] Import options: `--match exact|nearest|nearestrgb`, `--max-distance`, `--transparent-index0`, mapped onto `ImageImportOptions` (currently hardcoded `Default` in `Importer.cs:29`).
- [ ] Load plugin codecs (`Program.cs:134`). The constructor problem that blocked this is fixed, so a project that uses a plugin codec should work in the CLI too.
- [ ] Skip read-only arrangers on import and report them (depends on Milestone 1).
- [ ] Tests for each handler's exit code against a small fixture project.

## Milestone 6: UI completeness and polish

Exit criteria: every visible control works, and every implemented feature can be reached without knowing a hotkey.

- [ ] **Edit menu:** Undo, Redo, Cut, Copy, Paste, Delete and Select All, bound to the active editor's commands and enabled from the same state as the hotkeys. `MenuView.axaml` currently has only File, View and Help.
- [ ] **Hidden features:** decide for each of `ExpandWidth`/`ShrinkWidth` (`ArrangerTools.cs:477,501`), `EditSelection` (`Selection.cs:150`) and `CustomElementLayoutViewModel` (registered, never opened). Wire it up or delete it. **Recommendation:** wire up Expand/Shrink (they complete the Resize tool), and defer the custom layout dialog, because layouts aren't persisted yet.
- [ ] **Project-tree move.** DragOver and Drop are commented out (`ProjectTreeViewModel.cs:477-510`). Reorganizing a project without editing XML is basic, so 1.0 should support it, at least as a "Move to folder…" menu item, which stays automatable. This depends on the `MoveNodeAsync` fixes in Milestone 1.
- [ ] **Status bar:** `Indefinite` and `Reset` messages are dropped, and the handler is `async void` (`StatusViewModel.cs:19-28`).
- [ ] **Plugins menu:** keep it hidden for 1.0. Instead, list loaded plugin codecs in About, or in the log, so users can confirm a plugin loaded.
- [ ] **Preferences dialog:** a minimal one for the settings already persisted (theme, grid defaults, numeric base, symmetry tools, NES master palette path). Settings that are only reachable through files they've never seen aren't really user-facing.
- [ ] **Debug load button:** remove it from the release build, or guard it with `#if DEBUG`, rather than shipping a hidden button with a hardcoded `D:\` path (`ShellViewModel.cs:14`).
- [ ] UI pass over each feature in [FeatureRoadmap.md](docs/FeatureRoadmap.md) §3–4, fixing anything that behaves differently from its label.

## Milestone 7: Release engineering

Exit criteria: a tagged commit produces tested, downloadable builds for every target platform.

- [ ] **CI.** `.github/workflows/ci.yml` runs a Nuke `build.cmd` that no longer exists, only on manual dispatch, and runs no tests. Replace it with `dotnet build` and `dotnet test` on push and PR, and a release job that runs `publish.ps1` on tags and attaches the artifacts.
- [ ] **Publish profiles:** the CLI `.pubxml` files target `net7.0`. Update them or delete them in favor of `publish.ps1`.
- [ ] **Dead code:** delete `MagitekActions`/`IActionHistory`, `ImageColorAdapter` (all members throw), `FindStaleKeyResources` (`ProjectService.cs:727`), and `PixelRemapOperation.RemapByAnyIndex` (`ImageCopier.cs:18`; its branches are commented out, so it silently does nothing).
- [ ] **README:** it says .NET 6 and lists Autofac, Jot and Nuke. Update the stack and dependencies, and add CLI usage (verbs, options, exit codes).
- [ ] **Docs:** codec XML authoring and plugin authoring guides. Plugin authors can reuse `IndexedCodecContract` from the test project; decide whether to ship it as a package or document copying it.
- [ ] **Release notes** that call out behavior changes from the codec rework that existing projects will notice:
  - Genesis 4bpp, SNES Mode 7, Virtual Boy 2bpp and NGPC 2bpp had reversed bit order. Graphics now show correct colors, which differ from what earlier versions showed.
  - N64 RGBA32 reads big-endian `.z64` order. Convert `.v64` dumps first.
  - N64 RGBA16 now reports 16bpp and no longer zero-fills the second half of its storage on save.
  - PSX 16bpp preserves the STP bit: `0x0000` is transparent, STP on non-black is semi-transparent.
  - The C# SNES 3bpp, PSX 4bpp and PSX 8bpp codecs moved to the plugin samples. Projects that name them load the equivalent XML codec, and saving stores the XML codec's name.
  - The app now ships every codec XML (pattern codecs and SNES 3bpp Flow were missing from earlier builds).

---

## After 1.0

Candidates for 1.1, roughly in order: sequential arrangers as project resources, persisted element layouts and the custom layout dialog, indexed PNG export, export from sequential views and selections, partial/offset import, CLI `--json` output and key globs, OS clipboard, and tool hotkeys. Then [compression support](docs/CompressionSupport.md) and direct-color XML codecs. The full list is in [FeatureGaps.md](docs/FeatureGaps.md).
