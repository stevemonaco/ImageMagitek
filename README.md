# Installation
### [**Latest Release**](https://github.com/stevemonaco/ImageMagitek/releases) | [**Installation Requirements**](https://github.com/stevemonaco/ImageMagitek/wiki/TileShop-Installation-and-Overview) | [**Screenshots**](https://github.com/stevemonaco/ImageMagitek/wiki/TileShop-Workflow)

# TileShop and ImageMagitek
TileShop is an upcoming crossplatform application that implements ImageMagitek and allows end-users to manage specialized graphics in a modern GUI environment. ImageMagitek is an internal .NET library written in C# to view, edit, and organize common and complex retro videogame system graphics. Emphasis is given to the features most valuable to the common, cumbersome tasks when encountering graphics embedded within binaries without any distinguishable headers or identifiers. Exporting and importing is supported to allow advanced editing features to be performed in third-party image editors that support standard PNG.

TileShopCLI is a portable, limited implementation of TileShop where users can export/import resources from existing TileShop projects. This is especially useful in toolchains.

Old WPF Client
![TileShop Workspace Dark Theme](https://raw.githubusercontent.com/stevemonaco/ImageMagitek/master/TileShop.WPF/Assets/DemoImages/TileShopLayoutDark10142020.png)

# Tech Stack
Language - C# / .NET 6

GUI Framework - Avalonia

# Major Third Party Dependencies
Big thanks to the authors of these open source libraries for making this project much higher quality than otherwise possible

[Dock](https://github.com/wieslawsoltes/Dock) for the docking window layout

[FluentAvalonia](https://github.com/amwx/FluentAvalonia) for styling/theming

[PanAndZoom](https://github.com/wieslawsoltes/PanAndZoom) for the infinite canvas control

[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for the MVVM framework

[ImageSharp](https://github.com/SixLabors/ImageSharp) for loading/saving PNG images

[Autofac](https://github.com/autofac/Autofac) for Dependency Injection

[Jot](https://github.com/anakic/Jot) for tracking window settings

[OneOf](https://github.com/mcintyre321/OneOf) for creating better result types from domain actions

[McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) for plugin support

[Serilog](https://github.com/serilog/serilog) for logging

[Nuke](https://github.com/nuke-build/nuke) for the C#-based build system

[CommandLineParser](https://github.com/commandlineparser/commandline) for the CLI client parsing

## Old WPF Client Dependencies
[AvalonDock](https://github.com/Dirkster99/AvalonDock) for the docking window layout

[ModernWPF](https://github.com/Kinnara/ModernWpf) for styling/theming

[Stylet](https://github.com/canton7/Stylet) for MVVM architecture support

[GongSolutions.WPF.DragDrop](https://github.com/punker76/gong-wpf-dragdrop) for easy drag+drop support

[PixiEditor/ColorPicker](https://github.com/PixiEditor/ColorPicker) for the color picker for direct graphics

# External Contributors
Thanks to these people for helping push TileShop along

FCandChill - Testing/bug reports

Kajitani-Eizan - Testing/bug reports, 8bpp GBA codec
