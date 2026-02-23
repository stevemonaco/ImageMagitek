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

- Do NOT build or run tests as part of your process. These are expensive operations in this repo and should only be done when explicitly requested.
