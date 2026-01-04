# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack

- **Language**: C# with .NET 9 (SDK 10.0)
- **UI Framework**: Avalonia with Semi theme
- **MVVM**: CommunityToolkit.Mvvm
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Testing**: xUnit
- **Benchmarking**: BenchmarkDotNet

## Build Commands

```bash
# Build solution
dotnet build ImageMagitek.slnx

# Run tests
dotnet test ImageMagitek.UnitTests/ImageMagitek.UnitTests.csproj

# Run a single test
dotnet test ImageMagitek.UnitTests/ImageMagitek.UnitTests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run the UI application
dotnet run --project TileShop.UI/TileShop.UI.csproj
```

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
- `Arrangers/` - Arranger editors (sequential and scattered)
- `Pixels/` - Pixel-level editing (indexed and direct color)
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
