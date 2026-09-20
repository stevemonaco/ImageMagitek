# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack

- **Language**: C# with .NET 10 (SDK 10.0)
- **UI Framework**: Avalonia with Semi theme
- **MVVM**: CommunityToolkit.Mvvm
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Testing**: xUnit
- **Benchmarking**: BenchmarkDotNet

## Project Structure

- **ImageMagitek** - Core library for viewing/editing retro game graphics. Contains codecs, color handling, arrangers, and project serialization.
- **ImageMagitek.Services** - Service layer providing bootstrapping, plugin loading, and high-level operations.
- **TileShop.UI** - Avalonia-based GUI application using MVVM pattern.
- **TileShop.CLI** - Command-line tool for batch export/import operations.
- **TileShop.Shared** - Shared interfaces and services between UI and CLI.
- **ImageMagitek.UnitTests** - xUnit test project.

## Architecture

### Domain Concepts

- **Arranger** - A 2D grid that arranges graphic elements. Sequential arrangers read contiguous data; scattered arrangers reference arbitrary file offsets.
- **Codec** - Defines how to decode/encode pixel data for specific game platforms (NES, SNES, GBA, PSX, etc.). XML-defined in `ImageMagitek/_codecs/`.
- **Palette** - Color definitions. Indexed palettes map indices to colors; direct palettes use RGB values.
- **DataSource** - Abstraction over file or memory-backed binary data.
- **Element** - A single tile/graphic unit within an arranger.

### UI Architecture (TileShop.UI)

Feature-based folder organization under `Features/`:
- `Graphics/` - Display, Arranging, and Pixel-level editing of images (indexed and direct color)
- `Renderer/` - Renders the graphics editor state
- `Palettes/` - Palette editors
- `Dialogs/` - Modal dialogs
- `Shell/` - Main window, menu, status bar
- `Project/` - Project tree management
- `Project Nodes/` - TreeView node ViewModels

DI setup in `Bootstrapper.cs`. Views auto-registered by naming convention (FooViewModel -> FooView via `ViewLocator`).

## Code Style

- Private fields: `_camelCase` prefix
- File-scoped namespaces
- Prefer `var` for type inference
- Allman brace style (braces on new lines)
- 4-space indentation

## Workflow

- Rebuilding is allowed: `dotnet build TileShop.UI\TileShop.UI.csproj -c Debug`. The unit test suite (`dotnet test ImageMagitek.UnitTests`) runs in about a second; run it whenever a change touches the core library.
- XAML-only edits hot reload into the running Debug app (file watcher is enabled), no restart needed. C#, code-behind, and ViewModel edits need the stop → build → launch cycle below.

## Running and inspecting the UI

Claude may stop, rebuild, and relaunch the app itself to verify UI changes. Use PowerShell:

1. Stop the running instance: `Stop-Process -Name TileShop.UI -Force`. This is safe even when the user launched it from Rider/VS; the IDE sees the process exit and ends its debug session. While the app runs, builds fail at the copy step (`MSB3021`/`MSB3027`), so always stop first.
2. Build: `dotnet build TileShop.UI\TileShop.UI.csproj -c Debug -v q -nologo`.
3. Launch detached and keep the PID: `$p = Start-Process TileShop.UI\bin\Debug\net10.0\TileShop.UI.exe -WorkingDirectory TileShop.UI\bin\Debug\net10.0 -PassThru`. Wait ~6 s before attaching.
4. Attach the Avalonia DevTools MCP: `attach-to-app` with `id` = the PID. Use `search` (with `searchTemplates: true` for anything inside a template, including the docked editor), `tree`, `props`, and `screenshot` to inspect.

Driving the app through DevTools:

- `input Click` only works on controls that expose an automation Invoke/Select pattern (Button, RadioButton, ToggleButton, TreeViewItem, MenuItem). It does not synthesize pointer events, so PointerPressed/Tapped handlers on Borders, drag and drop, and gestures cannot be exercised; those need the user. Prefer building clickable UI as Buttons (with a chrome-free ControlTheme where needed) so it stays automatable and keyboard-accessible.
- `set-prop` works for state such as `IsExpanded="True"` on a TreeViewItem.
- Popups (flyouts, tooltips, context menus) are separate windows and do not appear in `search`, `tree`, or `screenshot`. Verify their contents by inspecting bound state after the interaction, or ask the user.
- Node IDs change after every relaunch and after tree changes; re-run `search` rather than reusing IDs.

Recipe to reach the graphics editor with the bundled test project:

1. Click the `debugLoadButton` ("Load FF2").
2. Expand a folder: `set-prop IsExpanded=True` on its TreeViewItem, then `search TreeViewItem` scoped to that node to get the children.
3. Open an arranger: `input Click` on its TreeViewItem (this updates the TreeView selection; `set-prop IsSelected` does not), then `input KeyDown Enter`.
4. The editor toolbar's mode RadioButtons are View / Arrange / Draw in tree order; tool buttons have x:Names such as `RemapColorsButton`, `ResizeArrangerButton`, `PaletteSwatchGrid`.
5. "Character Overworld Sprites → Adult Rydia Map" is a single-palette 8-color arranger where every Draw tool, including Color Remapper, is enabled. "Character Portraits → Portraits" references several palettes, so Color Remapper is disabled there.