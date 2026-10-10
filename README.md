# Installation
### [**Latest Release**](https://github.com/stevemonaco/ImageMagitek/releases) | [**Installation Requirements**](https://github.com/stevemonaco/ImageMagitek/wiki/TileShop-Installation-and-Overview) | [**Screenshots**](https://github.com/stevemonaco/ImageMagitek/wiki/TileShop-Workflow)

# TileShop and ImageMagitek
TileShop is an upcoming crossplatform application that implements ImageMagitek and allows end-users to manage specialized graphics in a modern GUI environment. ImageMagitek is an internal .NET library written in C# to view, edit, and organize common and complex retro videogame system graphics. Emphasis is given to the features most valuable to the common, cumbersome tasks when encountering graphics embedded within binaries without any distinguishable headers or identifiers. Exporting and importing is supported to allow advanced editing features to be performed in third-party image editors that support standard PNG.

TileShopCLI is a portable, limited implementation of TileShop where users can export/import resources from existing TileShop projects. This is especially useful in toolchains.

# Tech Stack
Language - C# / .NET 6

GUI Framework - Avalonia

# Major Third Party Dependencies
Big thanks to the authors of these open source libraries for making this project much higher quality than otherwise possible

[Dock](https://github.com/wieslawsoltes/Dock) for the docking window layout

[Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) for styling/theming

[PanAndZoom](https://github.com/wieslawsoltes/PanAndZoom) for the infinite canvas control

[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for the MVVM framework

[ImageSharp](https://github.com/SixLabors/ImageSharp) for loading/saving PNG images

[Autofac](https://github.com/autofac/Autofac) for Dependency Injection

[Jot](https://github.com/anakic/Jot) for tracking window settings

[OneOf](https://github.com/mcintyre321/OneOf) for creating better result types from domain actions

[McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) for plugin support

[Serilog](https://github.com/serilog/serilog) for logging

[Nuke](https://github.com/nuke-build/nuke) for the C#-based build system

[System.CommandLine](https://github.com/dotnet/command-line-api) for the CLI client parsing

[Lucide](https://lucide.dev) for the toolbar icons (ISC License)

The TileShop.WPF client's source is preserved on the wpf branch

# External Contributors
Thanks to these people for helping push TileShop along

FCandChill - Testing/bug reports

Kajitani-Eizan - Testing/bug reports, 8bpp GBA codec
