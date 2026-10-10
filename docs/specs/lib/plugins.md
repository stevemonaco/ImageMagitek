---
id: LIB-PLUGINS
title: Plugin contract and loading
project: ImageMagitek.Plugins.Contracts, ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek.Plugins.Contracts
  - ImageMagitek/Codec/Plugins
  - ImageMagitek.Services/PluginService.cs
  - ImageMagitek.Services/BootstrapService.cs
  - Samples/ImageMagitek.PluginSamples
types:
  - ICodecPlugin
  - IIndexedCodecPlugin
  - IDirectCodecPlugin
  - CodecInfo
  - CodecLayout
  - PluginColor
  - CodecPluginAdapter
  - IndexedCodecPluginAdapter
  - DirectCodecPluginAdapter
  - CodecPluginEvents
  - IPluginService
  - PluginService
tests:
  - SamplePluginContractTests
  - CodecPluginAdapterTests
  - PluginServiceTests
  - CodecFactoryTests
  - CodecEquivalenceTests
  - BootstrapServiceTests
depends:
  - LIB-CODECS
  - LIB-DATASOURCE
  - LIB-PALETTES
---

# Plugin contract and loading

## Purpose

Plugins add codecs that XML cannot express. A plugin compiles against `ImageMagitek.Plugins.Contracts` only. That assembly is a pure transform contract with a compatibility promise across a major version. The host wraps each plugin in an adapter that implements the core codec interfaces (LIB-CODECS), so arrangers, images and the UI treat plugin codecs like built-in ones. This spec also covers `CodecFactory.AddCodecPlugin` and the plugin member of `BootstrapService` (`CreatePluginService`).

## Requirements

### Loading

- **LIB-PLUGINS-001** — When plugins are loaded from a directory, the plugin service shall load `<sub>/<sub>.dll` from each immediate subdirectory that has one, in its own load context sharing only `ImageMagitek.Plugins.Contracts`, and collect every non-abstract type implementing `ICodecPlugin`. If loading the assembly or enumerating its types throws, then it shall report that plugin and skip it.
  - Tests: `PluginServiceTests.LoadCodecPlugins_Samples_DiscoversEveryCodec`, `PluginServiceTests.LoadCodecPlugins_BadDllBesideSamples_SkipsItAndLoadsSamples`
- **LIB-PLUGINS-002** — When a plugin references a version of `ImageMagitek.Plugins.Contracts` with a different major version, or the same major and a higher minor than the host's, the plugin service shall skip it before enumerating its types and report it as built for an incompatible plugin contract or a newer TileShop, naming both versions.
  - Tests: `PluginServiceTests.CheckContractVersion_FollowsMajorMinorRule`
- **LIB-PLUGINS-003** — When a plugin assembly references `ImageMagitek` and defines no `ICodecPlugin` type, including when its other types fail to load against the current core, the plugin service shall report it as built for the pre-1.0 plugin contract and needing a rebuild against `ImageMagitek.Plugins.Contracts`.
  - Tests: `PluginServiceTests.LoadCodecPlugins_CoreReferenceWithoutPluginTypes_ReportsRebuild`
- **LIB-PLUGINS-004** — When the bootstrapper creates the plugin service, it shall load plugins only if the plugin directory exists, register each discovered type with the codec factory, record each registered codec name for About (UI-SHELL-043), and log and record as a startup issue each plugin or type that fails to load or register; a type that fails to register shall be dropped from the discovered types.
  - Tests: `BootstrapServiceTests.CreatePluginService_CollidingName_SkipsAndRecordsIssue`

### Registration

- **LIB-PLUGINS-005** — If a type passed to `AddCodecPlugin` is abstract, implements neither or both of `IIndexedCodecPlugin` and `IDirectCodecPlugin`, has no public parameterless constructor, or throws while being constructed or while reading `Info`, then registration shall fail naming the type and the reason.
  - Tests: `CodecPluginAdapterTests.AddCodecPlugin_InvalidType_FailsWithReason`
- **LIB-PLUGINS-006** — If a plugin's `CodecInfo` has a blank name, a `ColorDepth` outside 1–8 (indexed) or 1–32 (direct), a default width or height below 1, a negative resize increment, or `GetStorageBits` at the default size below 1, then registration shall fail naming the type and the field.
  - Tests: `CodecPluginAdapterTests.AddCodecPlugin_InvalidInfo_FailsNamingField`
- **LIB-PLUGINS-007** — When a plugin codec is registered, it shall be registered under `Info.Name` with the uniqueness rule of LIB-CODECS (first registration wins), and `AddCodecPlugin` shall return the registered name.
  - Tests: `CodecPluginAdapterTests.AddCodecPlugin_NameCollision_KeepsFirstRegistration`, `CodecFactoryTests.AddCodecPlugin_NameTakenByXmlFormat_FailsAndKeepsFormat`, `BootstrapServiceTests.CreatePluginService_CollidingName_SkipsAndRecordsIssue`

### Adapters

- **LIB-PLUGINS-008** — When a plugin codec is created, the factory shall construct a new plugin instance and wrap it in a new adapter (`IndexedCodecPluginAdapter` with the factory's default palette, or `DirectCodecPluginAdapter`).
  - Tests: `CodecPluginAdapterTests.CreateCodec_ReturnsNewAdapterWithDefaultPalette`, `CodecPluginAdapterTests.DirectPlugin_DecodesThroughReinterpretedPixels`, `SamplePluginContractTests.AllSamples_AreRegistered`
- **LIB-PLUGINS-009** — When a plugin codec is created at a requested size, the adapter shall use the default size in each dimension whose resize increment is 0, and otherwise round the requested size down to a multiple of the increment, never below one increment. The adapter can resize when either increment is above 0.
  - Tests: `CodecPluginAdapterTests.Create_FixedSizePlugin_UsesDefaultSize`, `CodecPluginAdapterTests.Create_OneResizableDimension_RoundsOnlyThatDimension`, `IndexedCodecContract.ResizeIncrements_AreHonored` (run by `SamplePluginContractTests`)
- **LIB-PLUGINS-010** — The adapter's storage size shall be `GetStorageBits(width, height)` at its size. It shall read and write exactly that many bits at the element's address, and `ReadElement` shall return empty for an element that does not fit in its source.
  - Tests: `IndexedCodecContract.SaveElement_ByteAligned_ChangesOnlyElementBits` (run by `SamplePluginContractTests`), `CodecPluginAdapterTests.SaveImage_PluginWritesPastStorageBits_LeavesNeighborBitsUnchanged`
- **LIB-PLUGINS-011** — When the adapter encodes, it shall pass the plugin a zeroed buffer of `(storage bits + 7) / 8` bytes, and bits the plugin sets past the storage size shall not reach the data source.
  - Tests: `CodecPluginAdapterTests.SaveImage_PluginWritesPastStorageBits_LeavesNeighborBitsUnchanged`, `IndexedCodecContract.Encode_IsIndependentOfPriorEncode` (run by `SamplePluginContractTests`)
- **LIB-PLUGINS-012** — When a plugin's `CanEncode` is false, the adapter shall never call its `Encode`, and the adapter's `EncodeElement` shall throw `NotSupportedException`.
  - Tests: `CodecPluginAdapterTests.EncodeElement_CanEncodeFalse_ThrowsWithoutCallingPlugin`
- **LIB-PLUGINS-013** — If a plugin's `Decode` throws, then the adapter shall return all-zero pixels for that element and, on its first failure, raise `CodecPluginEvents.DecodeFailed` with the codec name and exception. If its `Encode` throws, then the adapter shall throw `InvalidOperationException` naming the codec, wrapping the plugin's exception.
  - Tests: `CodecPluginAdapterTests.DecodeElement_PluginThrows_ReturnsZerosAndRaisesDecodeFailedOnce`, `CodecPluginAdapterTests.EncodeElement_PluginThrows_ThrowsNamingCodec`
- **LIB-PLUGINS-014** — Pixels passed to and from a plugin shall be row-major (`y * width + x`), and `PluginColor` shall have the same size and field layout as `ColorRgba32`.
  - Tests: `CodecPluginAdapterTests.PluginColor_HasColorRgba32Layout`, `CodecPluginAdapterTests.DirectPlugin_DecodesThroughReinterpretedPixels`, `CodecEquivalenceTests.Snes3BppFlow_MatchesSpecialized`
- **LIB-PLUGINS-015** — The adapter shall zero the pixel buffer before calling `Decode`, and shall itself throw `ArgumentException`, without calling the plugin, for an encoded buffer shorter than the storage size or an image whose size differs from the codec's, so the codec contract of LIB-CODECS holds for every plugin.
  - Tests: `IndexedCodecContract.Decode_IsIndependentOfPriorDecode`, `IndexedCodecContract.Decode_TooShortBuffer_ThrowsArgumentException`, `IndexedCodecContract.Encode_WrongSizeImage_ThrowsArgumentException` (all run by `SamplePluginContractTests`)
- **LIB-PLUGINS-016** — When the bootstrapper has created the plugin service over an existing plugin directory, it shall log each `DecodeFailed` as a warning naming the codec, with the exception.
  - Tests: `BootstrapServiceTests.CreatePluginService_PluginDecodeFails_LogsWarningNamingCodec`

## Invariants

- No type in `ImageMagitek.Plugins.Contracts` references another assembly besides the runtime.
- A plugin never receives an arranger, element, data source or palette, and never writes to a data source.
- The contracts `AssemblyVersion` is `<major>.<minor>.0.0`. A minor version only adds to the contract; anything else is a major version.

## Edge cases

- A plugin built against an older minor version loads against a newer host; the runtime binds it to the host's assembly.
- A plugin folder that also contains `ImageMagitek.dll` loads it privately; its types do not unify with the host's and are ignored.
- An empty `_plugins` folder, or one with subdirectories lacking `<sub>.dll`, loads nothing and reports nothing.
- A plugin with one resizable dimension reports `CanResize` true; its fixed dimension's increment is 0, which the UI's resize steps do not expect. No sample has this shape.

## Threading and lifetime

- Each created codec has its own plugin instance; the host never calls one instance from two threads at once, so plugins may keep scratch buffers.
- `DecodeFailed` is static and may be raised on any thread that renders. Each bootstrapper subscribes once when the plugin directory exists and never unsubscribes; both hosts create one bootstrapper.
- Plugin load contexts are never unloaded; plugin DLLs stay locked until exit.

## Decisions

- **A separate contracts assembly, not the core library.** Only `ImageMagitek.Plugins.Contracts` is shared with plugins and frozen. Reason: the core can change through 1.x and package validation guards one small surface. Rejected: freezing the core's codec types (locks in the palette-on-codec design and every type a codec signature reaches).
- **Plugins are pure transforms; the host does I/O.** Reason: the host owns every byte written to a user's file. Rejected: plugin `ReadElement`/`WriteElement`; variable-length data belongs to compressors.
- **Host-side adapters, not a plugin-facing base class.** `IndexedCodecPluginAdapter` and `DirectCodecPluginAdapter` implement `IIndexedCodec`/`IDirectCodec` and share a base whose constructor only the core can call. Reason: the arranger, image, UI and CLI code is unchanged, and built-in codecs keep their own paths. The base is public because a public class cannot derive from an internal one. `IndexedCodec` had no non-sample subclass and is deleted. Rejected: moving the core onto the plugin interfaces.
- **The palette stays in the host adapter.** Reason: per-element host state no codec reads.
- **Its own `CodecLayout` and `PluginColor`.** They mirror `ImageLayout` and `ColorRgba32`. Reason: the contract must not reference core types. `PluginColor` has `ColorRgba32`'s layout, so the adapter reinterprets the span instead of copying it.
- **One plugin instance per created codec, from a public parameterless constructor.** Rejected: a plugin factory interface; shared stateless instances. Replaces the earlier decision that the factory picks a `(Palette, int, int)` or `(Palette)` constructor.
- **`AddCodecPlugin` replaces `AddCodec(Type)`.** `CodecFactory.AddCodec` and `ICodecService.AddCodec` are removed. Reason: no caller remained besides plugin registration, and a back door taking any `IGraphicsCodec` type would let plugins bypass the contract. Rejected: keeping both.
- **Size is chosen by the host.** A dimension with increment 0 is always the default; otherwise the request is rounded like `GetPreferredWidth`/`Height`. Reason: plugins never see a size they did not declare.
- **Registration validates `CodecInfo`.** A failing type is skipped and reported, like a colliding name.
- **A plugin DLL that fails to load is skipped whole.** Any exception while loading the assembly or enumerating its types skips that plugin directory. Reason: types from a partially loaded assembly can fail later when a member touches a missing dependency. Rejected: registering the loadable types from `ReflectionTypeLoadException.Types`. A type that fails registration is skipped on its own.
- **Contract version is major.minor, checked at load.** The referenced version is read from the assembly's references before its types are enumerated. Rejected: no check (fails later with `MissingMethodException`); exact match (breaks plugins on every additive change).
- **Pre-1.0 plugins are detected, not supported.** Rejected: a compatibility shim for `IGraphicsCodec` plugins.
- **`CanEncode` is a flag.** Rejected: a separate optional encoder interface.
- **Decode failures raise an event; the bootstrapper logs it.** Once per adapter. `BootstrapService.CreatePluginService` subscribes with its own logger, so both hosts log without host code. Reason: the core has no logger. Rejected: a logger dependency in the core.
- **No bit-reader helper at 1.0.** It would freeze an API the backlog plans to redesign; it can arrive as a minor version. The samples carry `SampleBits`.
- **Testing kit and authoring guide after 1.0.** The contract tests run over the samples in the unit tests (`SamplePluginContractTests`, `CodecPluginAdapterTests`); shipping them for plugin authors stays in the backlog.
- **Future plugin kinds join the same assembly.** Compressors and container scanners add their own interfaces to `ImageMagitek.Plugins.Contracts` as minor versions, with span or stream signatures and no core types.

## Non-goals

- Unloading or reloading plugins at runtime; a plugin manager UI (ARCHITECTURE.md §1).
- Sandboxing: a plugin runs with the app's full trust.
- Plugins for compression and container scanning, until their proposals land as minor versions of this contract.

## Open items

- Showing decode failures to the user is [status-notifications](../../changes/status-notifications.md); until then the hosts only log them.
- Package validation has no baseline until 1.0 ships (BACKLOG).
- A plugin with one resizable dimension gives the UI a 0 resize step on the fixed axis (BACKLOG).
