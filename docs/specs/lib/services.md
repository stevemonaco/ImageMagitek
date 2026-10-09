---
id: LIB-SERVICES
title: Bootstrapping and application settings
project: ImageMagitek.Services
sources:
  - ImageMagitek.Services/BootstrapService.cs
  - ImageMagitek.Services/BootstrapPaths.cs
  - ImageMagitek.Services/StartupIssue.cs
  - ImageMagitek.Services/SettingsService.cs
  - ImageMagitek.Services/Stores/AppSettings.cs
  - ImageMagitek.Services/appsettings.json
  - ImageMagitek.Services/Actions
types:
  - BootstrapService
  - BootstrapPaths
  - StartupIssue
  - BootstrapException
  - SettingsService
  - AppSettings
  - IMagitekAction
  - IActionHistory
tests:
  - SettingsServiceTests
  - BootstrapServiceTests
  - BootstrapPathsTests
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
- **LIB-SERVICES-004** — If the settings file exists but cannot be read as settings JSON, then bootstrap shall log it, record a startup issue naming the file and return the built-in defaults.
  - Tests: `BootstrapServiceTests.ReadConfiguration_MalformedFile_ReturnsDefaultsAndRecordsIssue`
- **LIB-SERVICES-005** — If the settings path or JSON content is null or empty, then reading shall throw an argument error.
  - Tests: untested
- **LIB-SERVICES-006** — The shipped `appsettings.json` shall equal the built-in defaults.
  - Tests: `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`
- **LIB-SERVICES-007** (inherited) — The defaults shall be global palettes `["DefaultRgba32"]`, NES palette `DefaultNes`, and extension → codec associations whose `default` entry is `NES 1bpp`, with entries for `.gb`, `.gba`, `.gbc`, `.gen`, `.gg`, `.md`, `.n64`, `.ncgr`, `.ncbr`, `.ngc`, `.nes`, `.sfc`, `.smc`, `.smd`, `.tim` and `.vb`.
  - Tests: `SettingsServiceTests.ReadSettings_MissingFile_ReturnsDefaults`, `SettingsServiceTests.ShippedAppSettings_MatchesCodeDefaults`, `SettingsServiceTests.Defaults_AssociationsNameRegisteredCodecs`
- **LIB-SERVICES-008** — When a settings file omits a key, or gives an empty global palette list, bootstrap shall use the built-in default for that setting; a key that is present replaces the default as a whole.
  - Tests: `SettingsServiceTests.Deserialize_PartialFile_MissingKeysTakeDefaults`, `SettingsServiceTests.Deserialize_EmptyGlobalPalettes_TakesDefault`, `SettingsServiceTests.Deserialize_ExtensionAssociations_ReplaceDefaults`
- **LIB-SERVICES-009** — Extension associations shall be stored as written; looking up an extension and falling back to `default` is the host's job.
  - Tests: untested

### Factories

- **LIB-SERVICES-010** — Each bootstrap factory member shall return a new object on every call and keep no reference to it.
  - Tests: untested
- **LIB-SERVICES-011** — The project service bootstrap creates shall use the serializer factory and color factory it is given, so the global resources a host passes to the serializer factory are the ones project references resolve against (LIB-PROJECT-FORMAT).
  - Tests: untested
- **LIB-SERVICES-012** — Bootstrap factory members shall be overridable, so hosts and tests can substitute any one of them.
  - Tests: untested

### Startup issues

- **LIB-SERVICES-014** — Each bootstrap factory member shall record a startup issue (file path and reason) for every file it logs and skips, and the host shall be able to read the issues after bootstrapping.
  - Tests: `BootstrapServiceTests.ReadConfiguration_MalformedFile_ReturnsDefaultsAndRecordsIssue`, `BootstrapServiceTests.CreatePaletteStore_MissingGlobalPalette_SkipsAndRecordsIssue`, `BootstrapServiceTests.CreateElementStore_DuplicateName_KeepsFirstAndRecordsIssue`
- **LIB-SERVICES-015** — If an essential resource (the codec schema, the resource schema, every global palette, or a usable NES master palette) cannot be loaded, then bootstrap shall log it as critical and throw `BootstrapException` with a message naming it.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_NoGlobalPalette_ThrowsBootstrapException`, `BootstrapServiceTests.CreatePaletteStore_ShortNesPalette_ThrowsBootstrapException`, `BootstrapServiceTests.CreateCodecService_MissingSchema_ThrowsBootstrapException`
- **LIB-SERVICES-016** — When bootstrap creates the project serializer factory, it shall fail as an essential resource if the resource schema file does not exist.
  - Tests: untested

### Defaults

- **LIB-SERVICES-013** — The default locations shall be `appsettings.json`, `_palettes`, `_codecs`, `_plugins`, `_layouts`, `_schemas/ResourceSchema.xsd` and `_schemas/CodecSchema.xsd`, resolved against a given directory; hosts shall pass the application directory, so the working directory never affects which resources load.
  - Tests: `BootstrapPathsTests.FromDirectory_RootsEveryPath`; DevTools — launch `TileShop.UI.exe` with working directory `C:\` and check the codec list is populated

## Invariants

- Settings are immutable once read; a host that wants a variant makes a modified copy.

## Edge cases

- A settings file of JSON `null` throws as invalid settings; bootstrap reports it and uses the defaults (LIB-SERVICES-004).
- A missing `_plugins` folder is normal and records no issue; a missing `_codecs` or `_layouts` folder records one.

## Threading and lifetime

- Runs once at startup on the host's thread; hosts register the returned objects as singletons. No shared state.

## Decisions

- **Settings are read once at startup.** `appsettings.json` ships beside the executable and users may edit it; changes take effect on restart. A missing file falls back to the built-in defaults, which a test keeps equal to the shipped file.
- **Shipped association defaults name canonical codecs.** Every association in `appsettings.json` and the code defaults names a codec registered directly (`.tim` → `PSX 4bpp Flow`). Reason: the legacy codec-name table (LIB-CODECS) exists for old projects and old user settings files, and shipped defaults should not depend on it. Rejected: leaving `.tim` on the retired `PSX 4bpp` name.
- **Hosts override settings by passing a modified copy.** Bootstrap uses whatever `AppSettings` it is given. The per-user NES palette is the exception: it is passed to `CreatePaletteStore` as an override so the library can validate it and fall back (LIB-PALETTES).
- **Shipped resources resolve against the application directory.** A `BootstrapPaths` record (settings file, palettes, codecs, plugins, layouts, both schemas) built by `BootstrapPaths.FromDirectory`; both hosts pass `AppContext.BaseDirectory`, which is the executable's directory for single-file publishes too. Reason: one place for the rule, testable with a temp directory. Rejected: `Directory.SetCurrentDirectory` at startup (a process-wide side effect that breaks relative paths users pass to the CLI); fixing only the UI's call sites (the next caller repeats the bug).
- **Issues are collected on the bootstrap service; essentials throw one exception type.** `BootstrapService.Issues` lists `StartupIssue(Path, Message)`, appended by every factory member as it logs a Warning. An essential failure logs a critical message and throws `BootstrapException` carrying it, which each host catches once around its bootstrap sequence. Reason: every later step needs the previous one's result, so there is nothing to continue with; one catch per host is simpler than threading a `MagitekResult` through every overridable member. Rejected: `MagitekResult` returns from each factory member; parsing the log.
- **Essentials are the two schemas, a global palette and a 64-entry NES master palette.** Without a schema no XML codec or project loads; the default palette is passed to the codec factory and every indexed codec; without a valid master palette every Nes-model palette fails at project load with an error that names nothing. All four ship with the app, so their absence means a broken install. A missing `_codecs` or `_layouts` folder is an issue, not fatal; a missing `_plugins` folder is normal. Rejected: embedding fallback copies as assembly resources (masks a broken install); treating the NES master palette as optional (moves the failure somewhere confusing).
- **Settings merge per member, not per key.** After deserializing, each null member (and an empty global palette list) takes the built-in default; a present `extensionCodecAssociations` replaces the default dictionary entirely. Reason: a user who writes the dictionary means it; key-level merging would make an association impossible to remove. A malformed settings file is not essential: bootstrap reports it and uses the defaults. Rejected: key-level merge; failing startup on malformed settings.

## Non-goals

- Writing settings back; settings are read-only to the library.
- User preferences (TileShop.Shared).

## Open items

- `IMagitekAction`, its action records and `IActionHistory` are unused (backlog: dead code).
