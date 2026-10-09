---
id: LIB-SERVICES
title: Bootstrapping and application settings
project: ImageMagitek.Services
sources:
  - ImageMagitek.Services/BootstrapService.cs
  - ImageMagitek.Services/SettingsService.cs
  - ImageMagitek.Services/Stores/AppSettings.cs
  - ImageMagitek.Services/appsettings.json
  - ImageMagitek.Services/Actions
types:
  - BootstrapService
  - SettingsService
  - AppSettings
  - IMagitekAction
  - IActionHistory
tests:
  - SettingsServiceTests
depends:
  - LIB-PROJECT-SERVICE
  - LIB-CODECS
  - LIB-PALETTES
  - LIB-ARRANGERS
---

# Bootstrapping and application settings

## Purpose

The startup entry point a host (TileShop.UI, TileShop.CLI) uses to build the library environment, and the application settings read from `appsettings.json`. Bootstrap's palette-store member is specified in LIB-PALETTES, its codec and plugin members in LIB-CODECS, and its element-store member in LIB-ARRANGERS; this spec covers settings, default locations and the remaining factory members. User preferences live in TileShop.Shared and belong to the UI specs.

## Requirements

### Settings

- **LIB-SERVICES-001** — When the settings file does not exist, reading settings shall return the built-in defaults.
  - Tests: `SettingsServiceTests.ReadSettings_MissingFile_ReturnsDefaults`
- **LIB-SERVICES-002** — Settings JSON shall map keys case-insensitively and tolerate comments and trailing commas.
  - Tests: `SettingsServiceTests.Deserialize_CamelCaseKeys_MapsToRecord`, `SettingsServiceTests.Deserialize_CommentsAndTrailingCommas_AreTolerated`
- **LIB-SERVICES-003** — If the settings file exists but is not valid settings JSON, then reading shall throw.
  - Tests: `SettingsServiceTests.Deserialize_MalformedJson_Throws`
- **LIB-SERVICES-004** — If reading the configuration throws, then bootstrap shall log it as critical and rethrow.
  - Tests: untested
- **LIB-SERVICES-005** — If the settings path or JSON content is null or empty, then reading shall throw an argument error.
  - Tests: untested
- **LIB-SERVICES-006** — The shipped `appsettings.json` shall equal the built-in defaults.
  - Tests: `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`
- **LIB-SERVICES-007** (inherited) — The defaults shall be global palettes `["DefaultRgba32"]`, NES palette `DefaultNes`, and extension → codec associations whose `default` entry is `NES 1bpp`, with entries for `.gb`, `.gba`, `.gbc`, `.gen`, `.gg`, `.md`, `.n64`, `.ncgr`, `.ncbr`, `.ngc`, `.nes`, `.sfc`, `.smc`, `.smd`, `.tim` and `.vb`.
  - Tests: `SettingsServiceTests.ReadSettings_MissingFile_ReturnsDefaults`, `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`
- **LIB-SERVICES-008** — When a settings file omits a key, that setting shall be null; the file is not merged with the defaults.
  - Tests: untested
- **LIB-SERVICES-009** — Extension associations shall be stored as written; looking up an extension and falling back to `default` is the host's job.
  - Tests: untested

### Factories

- **LIB-SERVICES-010** — Each bootstrap factory member shall return a new object on every call and keep no reference to it.
  - Tests: untested
- **LIB-SERVICES-011** — The project service bootstrap creates shall use the serializer factory and color factory it is given, so the global resources a host passes to the serializer factory are the ones project references resolve against (LIB-PROJECT-FORMAT).
  - Tests: untested
- **LIB-SERVICES-012** — Bootstrap factory members shall be overridable, so hosts and tests can substitute any one of them.
  - Tests: untested

### Defaults

- **LIB-SERVICES-013** (inherited) — The default locations shall be `appsettings.json`, `_palettes`, `_codecs`, `_plugins`, `_layouts`, `_schemas/ResourceSchema.xsd` and `_schemas/CodecSchema.xsd`, and the default log file `errorlog.txt`, all relative paths for the host to resolve.
  - Tests: untested

## Invariants

- Settings are immutable once read; a host that wants a variant makes a modified copy.

## Edge cases

- A settings file of JSON `null` throws as invalid settings.
- A partial settings file yields null members that later throw where they are used (for example while building the palette store).

## Threading and lifetime

- Runs once at startup on the host's thread; hosts register the returned objects as singletons. No shared state.

## Decisions

- **Settings are read once at startup.** `appsettings.json` ships beside the executable and users may edit it; changes take effect on restart. A missing file falls back to the built-in defaults, which a test keeps equal to the shipped file.
- **Hosts override settings by passing a modified copy.** Bootstrap uses whatever `AppSettings` it is given, so a host can apply a per-user `NesPalette` override without the library knowing about preferences.

## Non-goals

- Writing settings back; settings are read-only to the library.
- User preferences (TileShop.Shared).

## Open items

- LIB-SERVICES-008: a partial settings file is not merged with the defaults.
- The `.tim` association names `PSX 4bpp`, a retired codec name that resolves only through LIB-CODECS's alias table.
- LIB-SERVICES-013: the UI resolves these paths against the working directory while the CLI resolves them against the executable directory, so launching the UI from another directory finds no settings, palettes or codecs.
- `IMagitekAction`, its action records and `IActionHistory` are unused (backlog: dead code).
