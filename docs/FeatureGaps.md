# TileShop / ImageMagitek Feature Gaps

Gaps measured against the baseline in [FeatureRoadmap.md](FeatureRoadmap.md). Each item has a rough priority:

- **P1**: broken or half-finished functionality, or a blocker for common romhacking workflows
- **P2**: high-value additions expected of a tile editor
- **P3**: nice to have

Defects found while surveying the code are listed first, because several of them undermine features that look complete.

---

## 0. Defects Found During Review

Items marked *verified* were confirmed by reading the code. The rest were found by reading the code but haven't been reproduced.

| Pri | Area | Defect | Location |
|---|---|---|---|
| P1 | Undo/redo | Redo ignores Mirror, Rotate, ApplyPalette, DeleteElementSelection, and ResizeArranger; undo only re-clones the arranger when a paste is in the history *(verified)* | `GraphicsEditorViewModel.History.cs` |
| P1 | Arranging | Delete divides width by element height and height by element width, which is wrong for non-square elements *(verified)* | `GraphicsEditorViewModel.ArrangerTools.cs:387-388` |
| P1 | Plugins | The plugin loader calls a parameterless constructor, but every sample plugin needs a `Palette` *(verified)* | `PluginService.cs:39`, `CodecFactory.cs:43` |
| P1 | CLI | Directory-exists check is inverted, so nested export folders are never created *(verified)* | `TileShop.CLI/Porters/Exporter.cs:30` |
| P1 | CLI | `WithParsed(async ...)` is async-void, so the exit code can be read before the handler finishes | `TileShop.CLI/Program.cs` |
| P1 | CLI | Export handlers ignore failures and always return Success; `--log` file name is computed but unused | `TileShop.CLI/CommandHandlers` |
| P1 | Project | Folder rename computes the new location before renaming (no-op move); file rename deletes the old file even if the write failed | `ProjectService.RenameResourceAsync` |
| P1 | Project | Removing a node closes modified editors without prompting | `ProjectTreeViewModel.cs:315-318` |
| P2 | Project | `MoveNodeAsync` rollback moves the file back even when the first move failed; folder delete is non-recursive | `ProjectService.cs` |
| P2 | Arranging | Delete key isn't mode-guarded (fires in Draw/View and on sequential arrangers) | `ArrangerTools.cs` |
| P2 | Drawing | Pencil stays "drawing" after a stroke that modifies no pixels | `GraphicsEditorViewModel.Drawing.cs:114-131` |
| P2 | History | Color Remap bypasses `AddHistoryAction`, so the redo list isn't cleared | `ArrangerTools.cs:591` |
| P2 | Palettes | `SetNativeColor` writes an RGBA32 value into the foreign palette whatever the color model | `Palette.cs` |
| P2 | Codecs | N64 RGBA16 reports a 32-bit color depth and storage size | `N64Rgba16Codec.cs` |
| P2 | Codecs | Duplicate XML codec names silently overwrite each other (the `formats` dictionary is never populated) | `XmlCodecService.cs` |
| P2 | Colors | BGR9 allows 4-bit nibbles but the converter scales to 3-bit | `ColorBgr9.cs` / `ColorConverterBgr9.cs` |
| P2 | Status bar | `NotifyStatusDuration.Indefinite` and `Reset` messages are silently dropped | `StatusViewModel.cs` |
| P3 | Palettes | JSON palette colors that fail to parse are skipped silently | `PaletteJsonSerializer.cs` |
| P3 | Shell | Debug load button hardcodes `D:\ImageMagitekTest\FF2\FF2project.xml` (hidden) | `ShellViewModel.cs` |

---

## 1. Codecs

| Pri | Gap | Notes |
|---|---|---|
| P1 | Direct-color XML codecs | The schema accepts `colortype="direct"` but `CodecFactory` throws `NotSupportedException` |
| P1 | Compression support | No LZ77/LZSS, RLE, or Huffman decompress/recompress pipeline. Most commercial ROM graphics are compressed. This could be a pluggable `IDataTransform` on DataSource or arranger |
| P2 | Missing common platforms | Game Boy / GBC 2bpp (explicit entry), Master System 4bpp, PC Engine / TG16, N64 CI4/CI8/IA/I, NDS, Saturn, Neo Geo AES/MVS sprites, Atari/Lynx, WonderSwan |
| P2 | GBA/NDS direct bitmap codecs | BGR555 bitmap modes |
| P2 | Bit-wise sequential offsets | Offsets are byte-aligned; there's a TODO in `SequentialArranger.cs:119` |
| P2 | Register or remove the C# NES 1bpp codec | It's unused and its name collides with the XML codec |
| P2 | XML format extensions | Tile stride/padding, bit order and endianness, and per-tile header bytes (variable-width fonts currently need a plugin) |
| P3 | Codec authoring UI | Live preview while editing a codec XML |
| P3 | Codec auto-detection | Heuristic search across codecs for a given offset |

## 2. Palettes & Color

| Pri | Gap | Notes |
|---|---|---|
| P1 | Palette editor undo/redo | `PaletteEditorViewModel.cs:147-160` throws `NotImplementedException` |
| P1 | Palette file import/export | .pal (raw/JASC/RIFF), .act, .gpl, .hex, and a JSON writer. `PaletteService` is read-only |
| P1 | Scattered color source | It's a stub (loads as null, throws on save) |
| P2 | Palette editing operations | Reorder, swap, duplicate, copy/paste ranges, gradients, bulk hue/brightness adjust |
| P2 | Palette hotkeys | No Ctrl+S or other bindings in the palette editor |
| P2 | Additional color models | RGB24, ARGB32, RGB565, TG16 GRB333, N64 IA/I, grayscale |
| P2 | Palette sub-banks | Choosing a 16-color sub-palette of a 256-color palette per element |
| P3 | Palette generation | Quantize an image to N colors to create a palette, and dithering on import |
| P3 | Palette animation preview | Color cycling |

## 3. Graphics Editing

| Pri | Gap | Notes |
|---|---|---|
| P1 | Complete undo/redo coverage | See defects; all recorded actions should replay |
| P1 | Edit menu | Undo, Redo, Cut, Copy, Paste, Select All are only reachable by hotkey |
| P2 | Drawing tools | Line, rectangle, ellipse, eraser, brush size, replace color |
| P2 | Selection tools | Magic wand, lasso; pixel-level flip and rotate of a selection (current mirror/rotate only change how an element is displayed) |
| P2 | Cut (Ctrl+X) and OS clipboard | The clipboard is a private static field, so images can't be exchanged with external editors |
| P2 | Tool hotkeys | Single-key tool switching, plus `[` / `]` to step through palette indices |
| P2 | Wire up existing hidden features | `ExpandWidth`/`Height`, `ShrinkWidth`/`Height`, `EditSelection`, and the Custom Element Layout dialog are implemented but not bound to UI |
| P2 | Save-conflict handling | `SaveConflictsDetectedMessage` handler is an empty TODO (`GraphicsEditorViewModel.cs:574`) |
| P3 | Undo history panel | |
| P3 | Layers / reference overlay | |
| P3 | Text tool using a font arranger | Useful for translation work |
| P3 | Zoom percentage display and presets | |

## 4. Sequential Browsing

| Pri | Gap | Notes |
|---|---|---|
| P1 | Save view as a sequential arranger | Sequential arrangers can't be persisted as project resources |
| P2 | Bookmarks, plus back/forward navigation history | |
| P2 | Persist the element layout (2x2, 4x4, custom) | Only the Tiled/Single enum is saved |
| P2 | More layout presets | 1x2, 2x1, vertical-first, OAM shapes |
| P3 | Hex view side panel | |
| P3 | Search: find a pattern from an image or bytes | |
| P3 | Minimap / file overview strip | |

## 5. Project Management & Import/Export

| Pri | Gap | Notes |
|---|---|---|
| P1 | Project-tree move / drag and drop | The drop handler is a stub; the WPF code is commented out |
| P1 | Schema versioning and migration | Version `0.9` is written but never validated |
| P1 | Relink missing data files | Currently a hard load failure |
| P2 | Indexed PNG export | Preserves palette indices for round-tripping through external editors |
| P2 | Export sequential views and selections | Export is scattered-only |
| P2 | Partial and offset import | Image size must match exactly; allow crop, offset, or import into a selection |
| P2 | Duplicate resource; "New Sequential Arranger" in the tree | |
| P2 | Write-ahead-log protection for binary data writes | It covers XML saves only, not pixel commits |
| P2 | Additional export formats | BMP, GIF (indexed), and a palette sidecar file |
| P3 | Project tree search / filter | |
| P3 | Project metadata | Description, target ROM checksum/CRC, notes |
| P3 | Tilemap / nametable resources | Render a screen from a map plus a tileset |
| P3 | Patch generation | IPS/BPS output instead of writing to the ROM in place |

## 6. Toolchain / CLI

| Pri | Gap | Notes |
|---|---|---|
| P1 | Fix the CLI defects | Async-void handlers, inverted directory check, ignored export failures, unused `--log` |
| P1 | Import options | `--match exact\|nearest\|nearestrgb`, `--max-distance`, `--transparent-index0` |
| P2 | Load plugin codecs in the CLI | Commented out at `Program.cs:134` |
| P2 | Dry-run and report output | Show the import report (unmatched/substituted colors) without committing |
| P2 | Machine-readable output | `--json` for `print`, import, and export results |
| P2 | Key filters / globs | e.g. `export "Sprites/*"` |
| P2 | Palette export/import verbs | |
| P3 | Project authoring verbs | `new`, `add-datafile`, `add-palette`, `add-arranger` |
| P3 | Batch manifest | A JSON/YAML job file for multi-step builds |
| P3 | Scripting host | C# script or Lua, for custom extraction pipelines (see the FF5 sample) |
| P3 | `dotnet tool` packaging | |

## 7. Application Shell & UX

| Pri | Gap | Notes |
|---|---|---|
| P2 | Preferences dialog | Preferences are persisted but only editable indirectly |
| P2 | Plugins menu / plugin manager | The menu is stubbed out |
| P3 | Customizable keybindings | |
| P3 | Save and restore the window/dock layout | |
| P3 | Localization | |

## 8. Infrastructure & Documentation

| Pri | Gap | Notes |
|---|---|---|
| P1 | Working CI | Replace the Nuke workflow (`build.cmd` no longer exists) with `dotnet build` and `dotnet test` on push/PR |
| P1 | Update the README | It still says .NET 6, lists Autofac, Jot and Nuke, and has no CLI usage docs |
| P2 | Update stale publish profiles | `.pubxml` files target net7.0 |
| P2 | Test coverage | Project XML round-trip, ProjectService rename/move/delete, ResourceChange cascades, codec decode/encode (the `GraphicsCodecTests` and `ImageCopierTests` stubs are empty), CLI handlers |
| P2 | Remove or implement dead code | `MagitekActions`/`IActionHistory` stubs, `ImageColorAdapter` (all `NotImplementedException`), `PixelRemapOperation.RemapByAnyIndex`, `FindStaleKeyResources` |
| P3 | Codec XML authoring guide and plugin authoring guide | |
| P3 | Use the sample projects as integration test fixtures | `_xmlprojectsamples/*.zip` |
