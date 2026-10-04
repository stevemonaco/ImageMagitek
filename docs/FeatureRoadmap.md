# TileShop / ImageMagitek Feature Roadmap

Inventory of the current feature set, organized by area. Use it as the baseline roadmap; gaps and proposed work are tracked in [FeatureGaps.md](FeatureGaps.md).

**Legend:** `[x]` implemented · `[ ]` not implemented / stubbed · *(partial)* works with notable limits

---

## 1. Graphics Codecs

### 1.1 Indexed codecs (XML-defined, `ImageMagitek/_codecs/`)

| Platform | Codec | bpp | Type | Resizable |
|---|---|---|---|---|
| NES / Famicom | NES 1bpp | 1 | Flow | Yes |
| NES / Famicom | NES 2bpp | 2 | Flow (planar 1+1) | Yes |
| SNES | SNES 2bpp | 2 | Flow (interlaced) | Yes |
| SNES | SNES 3bpp Flow | 3 | Flow (2+1) | Yes |
| SNES | SNES 4bpp | 4 | Flow (2+2) | Yes |
| SNES | SNES 4bpp Pattern | 4 | Pattern (planar) | Fixed |
| SNES | SNES 8bpp | 8 | Flow | Yes |
| SNES | SNES Mode7 | 8 | Flow | Yes |
| Game Boy Advance | GBA 4bpp | 4 | Flow | Yes |
| Game Boy Advance | GBA 4bpp Pattern | 4 | Pattern (chunky) | Fixed |
| Game Boy Advance | GBA 8bpp | 8 | Flow | Yes |
| Sega Genesis | Genesis 4bpp | 4 | Flow | Yes |
| Sega Game Gear | Game Gear 4bpp | 4 | Flow (row interlace) | Yes |
| Neo Geo Pocket Color | NGPC 2bpp | 2 | Flow | Fixed |
| Virtual Boy | VB 2bpp | 2 | Flow | Fixed |
| PlayStation | PSX 4bpp Flow | 4 | Flow (linear 64x64) | Yes |
| PlayStation | PSX 8bpp Flow | 8 | Flow (linear 64x64) | Yes |
| Game-specific fonts | CotM Font, FF5 Font, FF5 Pattern, Tokimemo 1bpp | 1 | Flow / Pattern | Fixed |

### 1.2 Specialized C# codecs (`ImageMagitek/Codec/Specialized/`)

- [x] **Direct color:** BMP 24bpp, RGB24 Tiled, RGBA32 Tiled, N64 RGBA16, N64 RGBA32, PSX 16bpp (ABGR1555), PSX 24bpp

### 1.3 Codec definition framework

- [x] XML **flow codecs**: 1–32 planes, per-plane `rowinterlace`, `rowpixelpattern`, `mergepriority`, tiled or single layout, fixed or resizable size
- [x] XML **pattern codecs**: planar or chunky packing, up to 8 patterns, 64-symbol pattern alphabet, `RepeatList` with repeat increments
- [x] XSD schema validation (`_schemas/CodecSchema.xsd`)
- [ ] Direct-color XML codecs: the schema accepts them, but `CodecFactory` throws
- [ ] Compressed graphics (LZ, RLE, etc.), read-only. See [CompressionSupport.md](CompressionSupport.md)

### 1.4 Plugin codecs

- [x] Plugin loading via McMaster.NETCore.Plugins (`_plugins/<Name>/<Name>.dll`)
- [x] Sample plugins: Last Armageddon Font (read-only), Marmalade Boy Font (read-only, variable-width), SNES 4bpp, plus C# twins of the SNES 3bpp, PSX 4bpp, PSX 8bpp and NES 1bpp XML codecs
- [x] Projects that name the old built-in C# codecs load the equivalent XML codec
- [ ] Plugins in the CLI (loading is commented out)
- [ ] Plugins menu in the UI (stubbed out)

### 1.5 Element layouts (`_layouts/`)

- [x] JSON-defined tile layouts: Default (1x1), 2x2 H, 4x4 H
- [ ] *(partial)* Applied to sequential arrangers only; the Custom Element Layout dialog exists but nothing opens it, and the layout isn't saved to the project

---

## 2. Palettes & Color

### 2.1 Color models

| Model | Storage | Typical platform |
|---|---|---|
| RGBA32 | 32-bit | Generic / direct |
| BGR15 | 16-bit | SNES, GBA |
| RGB15 | 16-bit | — |
| ABGR16 (1555) | 16-bit | PSX |
| BGR9 | 16-bit | Genesis |
| BGR6 | 8-bit | Master System / Game Gear |
| NES | 8-bit index into 64-color table | NES |

### 2.2 Palette sources

- [x] File-backed range (offset, count, 1/2/4-byte entries, little or big endian)
- [x] Project native colors (`#RRGGBBAA`)
- [x] Project foreign colors (raw value in the palette's color model)
- [x] Mixed sources in one palette (ordered source list)
- [ ] Scattered color source: a stub that loads as null and throws on save
- [x] Global JSON palettes (`_palettes/DefaultNes.json`, `DefaultRgba32.json`)
- [x] Configurable NES master palette (`appsettings.json`)

### 2.3 Palette editor

- [x] Swatch grid with selection
- [x] Color editing: R/G/B/A, native hex, foreign raw value
- [x] Table-color picker for table-based models (NES)
- [x] Add or remove color sources, then "Save Sources" to rebuild
- [x] Index-0 transparency toggle
- [x] Read-only indicator for global palettes
- [ ] Palette undo/redo (throws `NotImplementedException`)
- [ ] Palette hotkeys (Ctrl+S, etc.)

### 2.4 Palette usage in graphics

- [x] Per-element palette assignment (Apply Palette tool, click or drag, applies to whole selection)
- [x] Pick Palette tool
- [x] Associate project or global palettes with an editor
- [x] Inline primary-color edit flyout from the graphics editor
- [x] Color Remap dialog: drag-and-drop index remapping (single-palette arrangers)

### 2.5 Color matching

- [x] Exact
- [x] Nearest (CIE94 ΔE in Lab)
- [x] NearestRgb (redmean-weighted)
- [x] Max-distance threshold and entry limit by codec depth

---

## 3. Graphics Editing (TileShop.UI)

### 3.1 Editor modes

- [x] **View**: browse a raw data file as a sequential arranger
- [x] **Arrange**: compose scattered arrangers from elements
- [x] **Draw**: pixel editing (indexed and direct color)
- [x] Prompt to save on mode switch when there are unsaved changes

### 3.2 Arranging tools

- [x] Element select, with resize handles and Ctrl+click single-cell select
- [x] Copy and paste elements: floating paste overlay, Enter to apply, Esc to cancel
- [x] Drag and drop selections between editors
- [x] Delete (reset) selected elements
- [x] Resize scattered arranger
- [x] Create a new scattered arranger from a selection
- [x] Inspect Element (codec, palette, source and offset shown in the status bar)
- [x] Element mirror (H/V) and rotate (L/R), as opt-in "symmetry tools"

### 3.3 Drawing tools

- [x] Pencil (primary on left click, secondary on right click)
- [x] Flood fill
- [x] Color picker (also a temporary modifier override)
- [x] Pixel select with pixel-level copy and paste (index or color remap)
- [x] Draw clip: restrict edits to a region, with greyscale or hidden mask
- [x] Primary and secondary colors, indexed swatch grid, direct-color picker

### 3.4 Sequential (raw file) browsing

- [x] Byte, row, column and page navigation with keyboard and wheel; Home/End
- [x] File-offset scrollbar
- [x] Jump to Offset (hex or decimal, preference remembered)
- [x] Codec switching and element width/height adjustment
- [x] Default codec per file extension (`ExtensionCodecAssociations`)

### 3.5 View and canvas

- [x] Zoom 0.25x–32x, pan, center, fit, reset, align top-left
- [x] Gridlines with configurable spacing, origin and colors, plus checkerboard background
- [x] Snap toggle (element or pixel)

### 3.6 History

- [x] Undo and redo for Pencil, Flood Fill, Color Remap, and Paste
- [ ] *(partial)* Mirror, Rotate, Apply Palette, Delete, and Resize are recorded but never replayed on redo, and undoing them may not restore the arranger

---

## 4. Project Management & Import/Export

### 4.1 Project model

- [x] Resource tree: Project → Folders → Data Files, Palettes, Scattered Arrangers
- [x] One XML file per resource, with folders mirrored to disk directories
- [x] Multiple projects open at once
- [x] New empty project, new project from an existing file, open, open recent, save, save as, close
- [x] Atomic multi-file saves via write-ahead log, with crash recovery when the project is next opened
- [x] Delete preview showing cascading effects (data file → palettes → arrangers), with fallback to the default palette
- [x] Rename and remove resources; "Open in Folder"
- [x] In-memory data sources (not serialized)
- [ ] Sequential arrangers saved as project resources
- [ ] Project-tree drag and drop / move (the drop handler is a stub)
- [ ] Schema versioning and migration (version `0.9` is written but never checked)
- [ ] Relinking missing data files

### 4.2 Image import

- [x] PNG import into scattered arrangers (UI and CLI)
- [x] Import preview: diff view, onion skin, peek, zoom
- [x] Match-strategy choice, transparent pixels mapped to index 0, alpha threshold, max distance
- [x] Import report listing changed, substituted and unmatched pixels; commit is blocked while any color is unmatched
- [x] Indexed (palette-matched) and direct import
- [ ] *(partial)* The image size must exactly match the arranger

### 4.3 Image export

- [x] Export a scattered arranger to PNG (always 32-bit RGBA)
- [ ] Indexed / palettized PNG export
- [ ] Export from sequential arrangers or from a selection
- [ ] Palette file import/export (.pal, .act, .gpl, JSON writer)

---

## 5. Toolchain / CLI (TileShop.CLI)

| Verb | Purpose | Options |
|---|---|---|
| `print` | List every project resource with its type and key | `--log` |
| `export` | Export named arrangers to PNG | `--overwrite`, `--log` |
| `exportall` | Export every scattered arranger | `--overwrite`, `--log` |
| `import` | Import PNGs into named arrangers | `-f` skip missing files, `-r` skip bad keys, `--log` |
| `importall` | Import every arranger from a folder tree | `-f`, `-r`, `--log` |

- [x] Distinct exit codes (0 success; −2 to −7 for each failure class)
- [x] Serilog file logging
- [x] Self-contained single-file publishing (`publish.ps1`: win-x64, linux-x64, osx-x64, osx-arm64)
- [ ] *(partial)* Import always uses exact color matching, with no CLI options to change it
- [ ] *(partial)* Nested export paths fail (inverted directory-exists check in `Exporter.cs`)

---

## 6. Application Shell & UX

- [x] Docking layout (project tree with a document area)
- [x] Light and dark themes (Semi.Avalonia), saved in preferences
- [x] Persisted preferences: recent projects, grid, dialog defaults, numeric base, symmetry tools
- [x] Hotkey service scoped to the active editor
- [x] Status bar showing hover info and transient messages
- [x] Prompt to save on exit
- [x] Help menu: Wiki link, About
- [ ] Edit menu (undo, redo, copy and paste exist only as hotkeys)
- [ ] Preferences dialog
- [ ] Plugins menu

---

## 7. Developer Platform

- [x] Core library (`ImageMagitek`) usable independently; samples show a standalone CLI and a WPF viewer
- [x] Services layer: bootstrapping, settings, codec, palette and plugin services
- [x] xUnit test suite: WAL transactions, BitStream, image importer, color matcher, pattern lists, settings, array/stream extensions
- [x] BenchmarkDotNet project
- [ ] CI pipeline (the workflow references a removed Nuke build, has no push/PR triggers and no test step)
