# ImageMagitek / TileShop — Architecture

ImageMagitek is a .NET library for viewing, editing and organizing retro game graphics embedded in binaries with no headers or identifiers. TileShop is the Avalonia GUI over it, and TileShop.CLI a batch export/import tool for toolchains. This document is the map: goals, project layout, architecture, testing, and project-wide decisions. What each feature does, and why, is in the [feature specs](specs/README.md); work not done yet is in the [backlog](BACKLOG.md); new work starts as a [change proposal](changes/README.md).

## 1. Goals and non-goals

**Goals**

- Find, view, arrange and edit tile and bitmap graphics inside ROMs and other binaries, for the platforms romhackers work with (NES, SNES, GBA, Genesis, Game Gear, PSX, N64, NGPC, Virtual Boy, plus game-specific fonts).
- Codecs are data, not code, where possible: an XML codec definition covers most planar and chunky tile formats; C# codecs and plugins cover the rest.
- Round-trip through external image editors: export to PNG (paletted when it fits, so indices survive), import back with color matching and a report of every pixel that changes.
- **A dependable 1.0.** A 1.0 user never loses work, never corrupts a ROM, and never hits a control that silently does nothing. Finishing and hardening what exists comes before adding anything new.

**Non-goals for 1.0**

- Direct-color XML codecs, compression support ([proposal](changes/compression-support.md)), new platforms and color models, new drawing or selection tools, layers, tilemaps, and scripting. They are in the backlog.
- Writing compressed data back, relocating blocks, or rebuilding archives, ever (see the compression proposal).
- A plugin manager UI. Loaded plugin codecs are listed in About.

## 2. Projects

| Project | Purpose | Specs |
|---|---|---|
| `ImageMagitek` | Core library: data sources, codecs (`_codecs/` XML, `Codec/Specialized`), colors and palettes (`_palettes/`), arrangers and element layouts (`_layouts/`), images, PNG import/export, project tree and XML project format (`_schemas/`). No UI dependency. | [`lib/`](specs/lib/index.md) |
| `ImageMagitek.Services` | Bootstrapping, settings, codec/palette/plugin services, `ProjectService` (the project operations both front ends use). | [`lib/`](specs/lib/index.md) |
| `TileShop.Shared` | Interfaces, messages, input and tool types shared by the UI and CLI. | [`ui/`](specs/ui/index.md) |
| `TileShop.UI` | Avalonia app (Semi theme, Dock, PanAndZoom), MVVM with CommunityToolkit.Mvvm, DI in `Bootstrapper.cs`. Feature folders under `Features/`. | [`ui/`](specs/ui/index.md) |
| `TileShop.UI.Controls` | Custom Avalonia controls used by TileShop.UI. | [`ui/`](specs/ui/index.md) |
| `TileShop.CLI` | `print`, `export`, `exportall`, `import`, `importall` over an existing project. | [`cli/`](specs/cli/index.md) |
| `ImageMagitek.UnitTests` | xUnit tests for the library, services and UI ViewModel logic (references TileShop.UI). | — |
| `ImageMagitek.Benchmarks` | BenchmarkDotNet codec benchmarks. | — |
| `Samples/` | Plugin samples (`ImageMagitek.PluginSamples`, also used by the tests), and FF5 monster sprite samples: a CLI exporter/importer and an Avalonia viewer. | — |

Build configuration is shared through `Directory.Build.props` and `Directory.Packages.props` (central package management: package versions go there, never in a csproj). `publish.ps1` produces self-contained single-file builds.

## 3. Domain

- **DataSource** — a byte source: a file (`FileDataSource`, which may be missing and relinked) or memory (`MemoryDataSource`, never serialized). Raises `DataWritten` after pixel saves. [LIB-DATASOURCE]
- **Codec** — decodes and encodes one element's pixels for a platform format. Indexed codecs yield palette indices; direct codecs yield colors. `CanEncode` false makes an arranger read-only. [LIB-CODECS]
- **Palette** — an ordered list of color sources (file ranges, project-native colors, foreign raw values) under one color model. [LIB-COLORS, LIB-PALETTES]
- **Arranger** — a 2D grid of elements. A *sequential* arranger reads contiguous data from one source (browsing a file); a *scattered* arranger references arbitrary sources, offsets, codecs and palettes per element (a composed sprite or screen). [LIB-ARRANGERS]
- **Element** — one cell of an arranger: source, address, codec, palette, mirror and rotation.
- **Image** — `IndexedImage`/`DirectImage` decode an arranger to pixels and encode them back. [LIB-IMAGES, LIB-IMAGE-IO]
- **Project** — a tree of folders, data files, palettes and scattered arrangers, one XML file per resource. A `ProjectTree` is rooted either at a project or, for a standalone file opened without a project, at a single data file. [LIB-PROJECT-TREE, LIB-PROJECT-SERVICE, LIB-PROJECT-FORMAT]

## 4. Architecture

```
TileShop.UI ─────┐            TileShop.CLI
  ViewModels      │                │
  (projections)   ▼                ▼
            ImageMagitek.Services (ProjectService, codec/palette/plugin services, settings)
                              │
                              ▼
            ImageMagitek (DataSource, Codec, Palette, Arranger, Image, ProjectTree, XML format)
```

- **The domain raises events; ViewModels are projections.** `ProjectTree` raises `Changed` (Added, Removed, Moved, Renamed) after the in-memory change, so rollbacks emit the reverse event; resources raise content events (`Palette.Changed`, `DataSource.DataWritten`). `ProjectService` raises `ProjectOpened`/`ProjectClosed` and forwards `TreeChanged`/`ResourceChanged` from every open tree with the tree as sender, so consumers subscribe once. The singleton consumers (`ProjectTreeViewModel`, `EditorsViewModel`, `MenuViewModel`) live for the app's lifetime; no caller reports back what it changed, and no UI message bus republishes domain events (a bridge was built and removed). Messenger messages are left only for UI commands and status text.
- **One kind of open tree.** A project or a standalone file is a `ProjectTree`, so lifetime events, the resource index and lookups cover both. Project-only operations refuse a standalone tree with a result. Lookups that may miss use `Find*` (null), not the throwing `Get*`.
- **Results for expected failures; exceptions for programming errors.** Domain and service operations return `MagitekResult`/`MagitekResult<T>` (OneOf-based) for failures a user can cause (name collisions, invalid moves, project-only operations on a standalone file, import staging); bad arguments throw.
- **Tree operations write to disk immediately.** Add, rename, move and delete change files as they happen; there is no tree-level dirty state. Only editor content is pending.
- **Pending edits live in memory until Save.** Graphics editors work on a cloned working arranger copied back to the project resource on Save (codec instances may be shared between clones, copies and pastes and are never changed in place; assigning a palette gives the element a cloned codec; the exceptions are `SequentialArranger.ChangePalette`, which the UI does not call, and the palette reset after a confirmed resource deletion). Palette edits sit in the shared `Palette`, so graphics editors preview them live, and the project writer serializes the node's committed model while a palette has pending edits.
- **Multi-file XML saves go through a write-ahead log** and are recovered the next time the project opens. Binary data writes are in place.
- **Dialogs go through `IInteractionService`.** Alerts, prompts and request dialogs (`RequestViewModel<T>`) render as overlays in the root dialog host; ViewModels never create windows.
- **Window-wide hotkeys belong to the active editor.** Each editor exposes `Hotkeys` and `EditCommands` holding the same command instances, with real `CanExecute` predicates, so Edit menu enablement equals hotkey enablement. Menu `InputGesture`s are display only. Plain keys yield to a focused TextBox.
- **Views are registered explicitly.** `ViewLocator` maps exact ViewModel types to views through `RegisterViewFactory<VM, View>` calls in `Bootstrapper.ConfigureViewLocator`; DI registers `*View`/`*ViewModel` types by name. A new dialog needs a view registration.

## 5. Testing

- `dotnet test ImageMagitek.UnitTests` runs in about a second; run it whenever a change touches the library or services.
- Codec tests run every indexed codec through a shared contract (`IndexedCodecContract`): decode, encode round-trip, isolation from neighboring bytes (sentinel bytes), and rejection of encoding when `CanEncode` is false. Plugin authors can reuse it.
- ViewModel logic worth testing is moved into plain classes (editing sessions, history) and tested without Avalonia.
- UI changes are verified in the running Debug app through the Avalonia DevTools MCP (see `CLAUDE.md`). DevTools cannot drive pointer gestures, drag and drop, or popups; requirements that need them are marked `manual` in the specs and their pending checks are in the backlog.

## 6. Project-wide decisions

- **XML codecs are the default way to add a platform.** A C# codec is justified only when the format is not expressible as flow or pattern planes. Built-in C# twins of XML codecs moved to the plugin samples, and projects naming them load the XML codec.
- **Path keys, not ids, for references.** Resources reference each other by project path (`datafile="Roms/FF2"`). Readable and hand-editable; the cost is that renames and moves rewrite referencing files. See LIB-PROJECT-FORMAT.
- **Read-only is one gate.** Anything that cannot be written back (a codec with `CanEncode` false, a read-only data source, later a compressed source) makes the arranger read-only, and every write path (draw, paste pixels, color remap, import, save, CLI import) consults the same check.
- **Writes never grow a data source.** Elements past the end of their source render blank and are skipped on save.
- **Bits are addressed MSB-first everywhere.** `BitAddress` bit 0 is the most significant bit; data source reads and writes and the codecs' bit packing follow it.
- **Native colors are RGBA32; foreign colors are the target system's raw values.** Images and files use native colors; palettes store foreign values in a color model. Conversion lives in one color factory, configured at startup with the NES master palette.
- **Element mirror and rotation are display and encode attributes,** not pixel edits.
- **Gate, don't hide, project-only actions.** An action that needs a project is disabled with a tooltip in a standalone file; a resource that reads a missing data file is refused with a Relink hint.
- **The CLI's exit code is a contract.** Zero for success, a distinct negative code per failure class; toolchains branch on it, and new verbs reuse the codes. Exported files mirror project paths, so a round trip needs no manifest.
- **Build UI as automatable controls.** Clickable UI is a Button, RadioButton, ToggleButton, MenuItem or TreeViewItem (with a chrome-free ControlTheme where needed), so it stays keyboard-accessible and drivable through DevTools.
- **Startup degrades; it stops only for essentials.** Each file in `_codecs`, `_plugins`, `_palettes` and `_layouts`, and the settings file, loads on its own: a bad one is logged, skipped and reported to the user once per launch (the UI's startup alert, the CLI's console), and the rest load. Startup fails, naming the file, only when an essential is missing or unusable: `CodecSchema.xsd`, `ResourceSchema.xsd`, a global palette, or an NES master palette of at least 64 colors. Shipped resources resolve against the application directory, never the working directory. See LIB-SERVICES.
