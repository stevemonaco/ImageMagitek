---
id: LIB-CODECS
title: Graphics codecs
project: ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek/Codec
  - ImageMagitek/_codecs
  - ImageMagitek/_schemas/CodecSchema.xsd
  - ImageMagitek/BitStream.cs
  - ImageMagitek.Services/XmlCodecService.cs
  - ImageMagitek.Services/BootstrapService.cs
types:
  - IGraphicsCodec
  - IIndexedCodec
  - IDirectCodec
  - DirectCodec
  - ImageLayout
  - IGraphicsFormat
  - FlowGraphicsFormat
  - PatternGraphicsFormat
  - PixelPacking
  - ImageProperty
  - RepeatList
  - PatternList
  - PlaneCoordinate
  - IndexedFlowGraphicsCodec
  - IndexedPatternGraphicsCodec
  - PackedBits
  - IGraphicsFormatReader
  - XmlGraphicsFormatReader
  - ICodecFactory
  - CodecFactory
  - Bmp24Codec
  - Rgb24TiledCodec
  - Rgba32TiledCodec
  - N64Rgba16Codec
  - N64Rgba32Codec
  - Psx16BppCodec
  - Psx24BppCodec
  - ICodecService
  - XmlCodecService
  - BitStream
tests:
  - IndexedCodecContract
  - IndexedCodecContractTests
  - DirectCodecContractTests
  - DirectCodecKnownAnswerTests
  - CodecKnownAnswerTests
  - CodecEquivalenceTests
  - CodecGoldenTests
  - CodecFactoryTests
  - PatternCodecPermutationTests
  - PatternListTests
  - XmlCodecServiceTests
  - BootstrapServiceTests
  - ReadOnlyArrangerTests
  - IndexedImageTests
  - BitStreamTests
depends:
  - LIB-DATASOURCE
  - LIB-COLORS
  - LIB-PALETTES
---

# Graphics codecs

## Purpose

A codec decodes one element's stored bits into pixels and encodes them back: palette indices for indexed codecs, RGBA colors for direct codecs. Most indexed formats are XML definitions (flow or pattern codecs) loaded at startup; direct formats and anything XML cannot express are built-in C# codecs or plugins. Plugins are pure transforms that the factory wraps in adapters implementing the codec interfaces; the contract, the adapters and plugin loading are specified in LIB-PLUGINS. Elements and arrangers (LIB-ARRANGERS) choose codecs by name through the codec factory; images (LIB-IMAGES) call the codec to read, decode, encode and write. This spec also covers the codec-loading member of `BootstrapService` (`CreateCodecService`).

## Requirements

### Codec contract

- **LIB-CODECS-001** — When an element's address plus the codec's storage size extends past the end of its source, `ReadElement` shall return an empty buffer and read nothing.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`, `IndexedImageTests.DirectArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`
- **LIB-CODECS-002** — Otherwise `ReadElement` shall return the codec's storage size in bits read from the element's bit address.
  - Tests: `IndexedCodecContract.SaveElement_ByteAligned_ChangesOnlyElementBits`
- **LIB-CODECS-003** — `WriteElement` shall write exactly the codec's storage size in bits at the element's address and leave every other bit of the source unchanged.
  - Tests: `IndexedCodecContract.SaveElement_ByteAligned_ChangesOnlyElementBits`
- **LIB-CODECS-004** — A codec's storage size shall be width × height × color depth bits.
  - Tests: `CodecGoldenTests.Codec_MatchesSnapshot`
- **LIB-CODECS-005** — An indexed codec shall decode to palette indices and a direct codec to RGBA colors, both as a [y, x] array of the codec's size.
  - Tests: `DirectCodecContractTests.CreatedCodec_HasRequestedSize`
- **LIB-CODECS-006** — If the encoded buffer passed to decode is shorter than the storage size, then the codec shall throw `ArgumentException`.
  - Tests: `IndexedCodecContract.Decode_TooShortBuffer_ThrowsArgumentException`, `DirectCodecContractTests.Decode_TooShortBuffer_ThrowsArgumentException`
- **LIB-CODECS-007** — If the image passed to encode does not match the codec's width and height, then the codec shall throw `ArgumentException`.
  - Tests: `IndexedCodecContract.Encode_WrongSizeImage_ThrowsArgumentException`, `DirectCodecContractTests.Encode_WrongSizeImage_ThrowsArgumentException`
- **LIB-CODECS-008** — For every encodable codec, encoding a decoded buffer shall reproduce the storage bits, and decoding an encoded image shall reproduce the pixels.
  - Tests: `IndexedCodecContract.PixelsToBytesToPixels_RoundTrips`, `IndexedCodecContract.BytesToPixelsToBytes_RoundTrips`, `DirectCodecContractTests.PixelsToBytesToPixels_RoundTrips`, `DirectCodecContractTests.BytesToPixelsToBytes_RoundTrips`
- **LIB-CODECS-009** — A decode or encode result shall not depend on earlier calls on the same codec instance.
  - Tests: `IndexedCodecContract.Decode_IsIndependentOfPriorDecode`, `IndexedCodecContract.Encode_IsIndependentOfPriorEncode`, `IndexedCodecContract.EncodeAfterDecode_IsIndependent`, `DirectCodecContractTests.Decode_IsIndependentOfPriorDecode`, `DirectCodecContractTests.Encode_IsIndependentOfPriorEncode`, `DirectCodecContractTests.EncodeAfterDecode_IsIndependent`
- **LIB-CODECS-010** — Buffers returned by `ReadElement`, `DecodeElement` and `EncodeElement` shall be owned by the codec and valid only until the next call on the same instance.
  - Tests: untested
- **LIB-CODECS-011** — If a codec reports `CanEncode` false, then encoding through it shall throw `NotSupportedException`, and any arranger with an element using it is read-only (LIB-ARRANGERS).
  - Tests: `ReadOnlyArrangerTests.IsReadOnly_DecodeOnlyCodec_IsTrue`, `IndexedCodecContract.PixelsToBytesToPixels_RoundTrips` (run by `SamplePluginContractTests` over the decode-only sample codecs)
- **LIB-CODECS-012** — When asked for a preferred size, a resizable codec shall round the request down to a multiple of its resize increment, never below one increment; a non-resizable codec shall return its default size.
  - Tests: `IndexedCodecContract.ResizeIncrements_AreHonored`

### Flow codecs

- **LIB-CODECS-013** — A flow codec shall read encoded bits most-significant-bit first and consume its images (plane groups) in order, each image's bits merging into the planes named by its slice of `mergepriority`.
  - Tests: `CodecKnownAnswerTests.Snes4Bpp_TwoInterlacedPlanePairs`, `CodecKnownAnswerTests.Snes3BppFlow_InterlacedPlanesThenThirdPlane`
- **LIB-CODECS-014** — When an image has `rowinterlace` true, the flow codec shall store, for each row, one plane's bits for the whole row before the next plane's; when false, it shall store all of a pixel's plane bits before the next pixel.
  - Tests: `CodecKnownAnswerTests.Snes2Bpp_RowInterlacedPlanes`, `CodecKnownAnswerTests.Gba4Bpp_LowNibbleIsLeftPixel`
- **LIB-CODECS-015** — The flow codec shall place the x-th stored pixel of a row at the column given by the image's `rowpixelpattern`, repeated across the row with each repeat offset by the pattern length; an image without one stores pixels left to right.
  - Tests: `CodecKnownAnswerTests.Gba4Bpp_LowNibbleIsLeftPixel`, `CodecKnownAnswerTests.CotMFont_FromXmlSemantics_PixelPairsSwapped`
- **LIB-CODECS-016** — A flow codec shall be resizable unless its `fixedsize` is true, with a height increment of 1.
  - Tests: `IndexedCodecContract.ResizeIncrements_AreHonored`
- **LIB-CODECS-017** (inherited) — A flow codec's width increment shall be the length of its longest `rowpixelpattern`.
  - Tests: `IndexedCodecContract.ResizeIncrements_AreHonored`
- **LIB-CODECS-018** — Flow codecs shall always be encodable.
  - Tests: `IndexedCodecContract.PixelsToBytesToPixels_RoundTrips`

### Pattern codecs

- **LIB-CODECS-019** — A pattern codec shall map each encoded bit to a pixel and color bit through its pattern list, then through `rowpixelpattern` and `mergepriority`.
  - Tests: `PatternCodecPermutationTests.Decode_FirstBit_LandsAtPermutedPixelAndColorBit`, `PatternCodecPermutationTests.Encode_IsInverseOfDecode_ForSingleBit`
- **LIB-CODECS-020** — The pattern list shall rank pattern symbols `A–Z`, `a–z`, `2–9`, `!?@*`, each symbol naming up to 8 consecutive bit positions, and extend the pattern by whole repeats to fill the element.
  - Tests: `PatternListTests.TryCreateRemapPattern_DecodePlanar_AsExpected`, `PatternListTests.TryCreateRemapPattern_DecodeChunky_AsExpected`, `PatternListTests.TryCreateRemapPattern_EncodePlanar_AsExpected`, `PatternListTests.TryCreateRemapPattern_EncodeChunky_AsExpected`
- **LIB-CODECS-021** — A planar pattern codec shall take one pattern per plane; a chunky pattern codec shall take one pattern covering all planes of each pixel.
  - Tests: `PatternListTests.TryCreateRemapPattern_DecodePlanar_AsExpected`, `PatternListTests.TryCreateRemapPattern_DecodeChunky_AsExpected`
- **LIB-CODECS-022** — If the declared pattern size does not match the patterns, the element size is not a multiple of it, patterns differ in length, a chunky codec has more than one pattern, a symbol is invalid or overused, or a symbol maps past the pattern size, then building the pattern list shall fail with a reason.
  - Tests: `PatternListTests.TryCreateRemapPattern_TooManyLetters_Fails`, `PatternListTests.TryCreateRemapPattern_OutOfRange_Fails`, `PatternListTests.TryCreateRemapPattern_InvalidSize_Fails`, `PatternListTests.TryCreateRemapPattern_InvalidCharacter_Fails`
- **LIB-CODECS-023** — If a planar pattern codec's pattern count differs from its color depth, or a pattern is empty, then building the pattern list shall throw `ArgumentException`.
  - Tests: untested
- **LIB-CODECS-024** — A pattern codec shall be fixed at its declared width and height, non-resizable, and encodable.
  - Tests: `IndexedCodecContract.ResizeIncrements_AreHonored`

### XML codec files

- **LIB-CODECS-025** — The XML reader shall validate each file against `CodecSchema.xsd` and, if validation fails, return a failure listing every schema message after a line naming the file.
  - Tests: untested
- **LIB-CODECS-026** — If the file does not exist, then the XML reader shall return a failure naming it.
  - Tests: untested
- **LIB-CODECS-027** — The XML reader shall read a `flowcodec` root as a flow format and a `patterncodec` root as a pattern format, and fail on any other root.
  - Tests: `IndexedCodecContractTests.AllShippedXmlCodecsLoad`
- **LIB-CODECS-028** — If a codec's color depth is outside 1–32, its `mergepriority` count differs from its color depth, its image color depths do not sum to its color depth, or its pattern size is outside 1–512, then the XML reader shall fail with a reason carrying the line number where known.
  - Tests: untested
- **LIB-CODECS-029** (inherited) — If a codec's default width or height (flow) or width or height (pattern) is 1 or less, then the XML reader shall fail.
  - Tests: untested
- **LIB-CODECS-030** — Every shipped XML codec shall load with the declared default size, and the build output shall carry an identical copy of every codec XML.
  - Tests: `IndexedCodecContractTests.AllShippedXmlCodecsLoad`, `IndexedCodecContractTests.BuildOutputShipsEveryCodecXml`

### XML codec service

- **LIB-CODECS-031** — When codecs are loaded from a directory, the service shall read each top-level file ending in `.xml`, in ordinal file-name order, and register each one that loads.
  - Tests: `XmlCodecServiceTests.LoadCodecs_DuplicateName_KeepsFirstAndNamesBothFiles`
- **LIB-CODECS-032** — When two files define the same codec name, the service shall keep the first, skip the later one, and report a failure naming both files.
  - Tests: `XmlCodecServiceTests.LoadCodecs_DuplicateName_KeepsFirstAndNamesBothFiles`
- **LIB-CODECS-033** — When a file fails to load, including a file that is not well-formed XML, the service shall report its name and reasons and continue with the remaining files.
  - Tests: `XmlCodecServiceTests.LoadCodecs_MalformedXml_ReportsAndLoadsTheRest`
- **LIB-CODECS-034** — When the bootstrapper creates the codec service, it shall log and record as a startup issue every load failure and continue with the codecs that loaded; a missing codec folder is an issue, and a missing codec schema is fatal (LIB-SERVICES).
  - Tests: `BootstrapServiceTests.CreateCodecService_MissingFolder_RecordsIssue`, `BootstrapServiceTests.CreateCodecService_MissingSchema_ThrowsBootstrapException`

### Codec factory

- **LIB-CODECS-035** — The codec factory shall register the built-in direct codecs `Rgb24 Tiled`, `Rgba32 Tiled`, `Bmp24`, `N64 Rgba16`, `N64 Rgba32`, `PSX 16bpp` and `PSX 24bpp` at construction.
  - Tests: `DirectCodecContractTests.CreatedCodec_HasRequestedSize`
- ~~**LIB-CODECS-036**~~ — Removed: names are unique across C# codecs and XML formats, so there is nothing to prefer.
- **LIB-CODECS-037** — When a name matches no C# codec or XML format but is a legacy name (`SNES 3bpp`, `PSX 4bpp`, `PSX 8bpp`), the factory shall create the corresponding XML codec (`SNES 3bpp Flow`, `PSX 4bpp Flow`, `PSX 8bpp Flow`).
  - Tests: `CodecFactoryTests.LegacyName_ResolvesToXmlCodec`
- **LIB-CODECS-038** — If the name matches nothing, then the factory shall throw `KeyNotFoundException`.
  - Tests: untested
- **LIB-CODECS-039** — If the name is an XML format with color type `direct`, then the factory shall throw `NotSupportedException`.
  - Tests: untested
- **LIB-CODECS-040** — When an element size is given, the factory shall create flow codecs and built-in C# codecs that have a sized constructor at that size (plugin codecs follow LIB-PLUGINS-009); otherwise, and for pattern codecs, it shall create the codec at its default size.
  - Tests: `CodecFactoryTests.LegacyName_ResolvesToXmlCodec`, `DirectCodecContractTests.CreatedCodec_HasRequestedSize`
- **LIB-CODECS-041** — The factory shall construct a built-in C# codec with `(width, height)` or `()`, and throw `ArgumentException` when no such constructor exists.
  - Tests: `DirectCodecContractTests.CreatedCodec_HasRequestedSize`
- **LIB-CODECS-042** — Each created codec shall be a new instance with its own buffers and its own copy of the format, so resizing one codec does not affect the registered format or other codecs.
  - Tests: untested
- ~~**LIB-CODECS-043**~~ — Moved to LIB-PLUGINS: C# codec types are no longer added at runtime; plugin types register through `AddCodecPlugin` (LIB-PLUGINS-005–007).
- ~~**LIB-CODECS-044**~~ — Moved to LIB-PLUGINS: type checks for added codecs are LIB-PLUGINS-005.
- **LIB-CODECS-045** — The factory's registered names shall list each XML format, built-in C# codec and plugin codec once, in sorted order, excluding legacy names.
  - Tests: `CodecFactoryTests.LegacyName_ResolvesToXmlCodec`, `CodecFactoryTests.RegisteredNames_AreUnique`
- **LIB-CODECS-060** — If an XML format is added under a name already registered as a C# codec or XML format, then the factory shall return a failure and keep the existing registration.
  - Tests: `CodecFactoryTests.AddFormat_NameTakenByBuiltInCodec_Fails`, `XmlCodecServiceTests.LoadCodecs_DuplicateName_KeepsFirstAndNamesBothFiles`
- **LIB-CODECS-046** — When a codec is cloned, the factory shall create a new codec of the same name and size, and throw `ArgumentException` if it cannot.
  - Tests: untested

### Built-in direct codecs

- **LIB-CODECS-047** — `Rgba32 Tiled` shall store 4 bytes per pixel in R, G, B, A order, row-major, tiled, default 8×8.
  - Tests: `DirectCodecKnownAnswerTests.Rgba32Tiled_RgbaPerPixel`
- **LIB-CODECS-048** — `Rgb24 Tiled` shall store 3 bytes per pixel in R, G, B order, tiled, default 8×8, decoding alpha as 255 and dropping alpha on encode.
  - Tests: `DirectCodecKnownAnswerTests.Rgb24Tiled_RgbPerPixel`
- **LIB-CODECS-049** — `Bmp24` shall store 3 bytes per pixel in B, G, R order with rows bottom-up, single layout, default 8×8.
  - Tests: `DirectCodecKnownAnswerTests.Bmp24_BgrPerPixel_RowsBottomUp`
- **LIB-CODECS-050** — `N64 Rgba16` shall store one big-endian 16-bit RGBA5551 word per pixel, 16 bpp, tiled, default 32×32; the alpha bit decodes to 0 or 255 and encodes as 1 only for alpha 255.
  - Tests: `DirectCodecKnownAnswerTests.N64Rgba16_BigEndian5551_Stores16BitsPerPixel`
- **LIB-CODECS-051** — `N64 Rgba32` shall store 4 bytes per pixel in R, G, B, A order (`.z64` byte order), tiled, default 32×32.
  - Tests: `DirectCodecKnownAnswerTests.N64Rgba32_RgbaPerPixel`
- **LIB-CODECS-052** — `PSX 16bpp` shall store one little-endian ABGR1555 word per pixel, converted with the ABGR16 STP rules of LIB-COLORS, single layout, default 64×64.
  - Tests: `DirectCodecKnownAnswerTests.Psx16Bpp_LittleEndianBgr555_OpaqueColorsClearStp`, `DirectCodecKnownAnswerTests.Psx16Bpp_TransparentAndSemiTransparent`
- **LIB-CODECS-053** — `PSX 24bpp` shall store 3 bytes per pixel in R, G, B order, single layout, default 64×64.
  - Tests: `DirectCodecKnownAnswerTests.Psx24Bpp_RgbPerPixel`
- **LIB-CODECS-054** — The built-in direct codecs shall be encodable and resizable in steps of 1 pixel.
  - Tests: `DirectCodecContractTests.CreatedCodec_HasRequestedSize`
- **LIB-CODECS-055** — The encoded bytes and decoded pixels of every shipped codec shall match the checked-in snapshots.
  - Tests: `CodecGoldenTests.Codec_MatchesSnapshot`, `CodecGoldenTests.DirectCodec_MatchesSnapshot`

### Plugins

- ~~**LIB-CODECS-056**~~ — Moved to LIB-PLUGINS (LIB-PLUGINS-001).
- ~~**LIB-CODECS-057**~~ — Moved to LIB-PLUGINS (LIB-PLUGINS-004).

### Bit streams

- **LIB-CODECS-058** — The bit stream shall read and write bits most-significant-bit first, from a configurable starting bit of the first byte.
  - Tests: `BitStreamTests.ReadBits_ReturnsExpected`, `BitStreamTests.WriteBits_AsExpected`
- **LIB-CODECS-059** — If a bit stream read or write goes past the declared bit length, then it shall throw `EndOfStreamException`.
  - Tests: untested

## Invariants

- A codec instance owns fixed buffers sized for its width, height and storage size; its size never changes after construction.
- A registered XML format is never mutated by codec creation.
- The XML and C# implementations of the same format (SNES 3bpp, PSX 4bpp/8bpp, NES 1bpp, SNES 4bpp pattern vs flow, GBA 4bpp pattern vs flow, FF5 font vs pattern) decode and encode identically (`CodecEquivalenceTests`).

## Edge cases

- An element that extends partly past the end of its source is skipped entirely, not partially decoded; it renders as index 0 / transparent black and is never written (LIB-IMAGES).
- A fixed-size flow codec requested at another size is created at that size; only `GetPreferredWidth`/`Height` enforce the default.
- Formats passed to the `CodecFactory` constructor are registered without the uniqueness check of `AddFormat`; the hosts pass none.

## Threading and lifetime

- Codec instances are not thread-safe: their buffers are reused across calls. Each element gets its own codec instance from the factory.
- The factory and codec service are built once at startup and live for the app. The plugin service and plugin load contexts are in LIB-PLUGINS.

## Decisions

- **XML codecs are the default.** See docs/ARCHITECTURE.md. The C# SNES 3bpp, PSX 4bpp and PSX 8bpp codecs moved to the plugin samples. Reason: they duplicated byte-identical XML codecs. Projects that name them load the XML codec through the legacy names, and saving stores the XML codec's name.
- **Legacy names resolve last.** A legacy name is used only when nothing is registered under it, so a plugin that reuses one of those names wins. Rejected: rewriting names at project load.
- **Codec names are unique; the first registration wins.** Registration order is built-in C# codecs, then XML codecs in ordinal file order, then plugins in directory order. A later registration under a taken name (any source) is refused and reported naming both sources; `AddCodecPlugin`/`AddFormat` (formerly `AddOrUpdateCodec`/`AddOrUpdateFormat`) return a result. Reason: a project names codecs, so which bytes-to-pixels mapping it gets must not depend on installed plugins; shipped codecs are the trusted ones. This generalizes the earlier "Duplicate XML names keep the first file" decision, made because a later XML file silently overwrote an earlier one. Rejected: C# codecs win (the earlier, silent behavior); last registration wins; renaming the newcomer. Legacy names are not registered names, so a plugin may still claim one ("Legacy names resolve last").
- **Past-end elements are skipped, not thrown.** `ReadElement` returns empty for an element that does not fit in its source, images render it empty and never write it, so an arranger near EOF or over a truncated file shows blank cells and saving does not grow the file. Reason: reads past EOF used to throw or show garbage. Rejected: zero-padding the read, which would let a save grow the file.
- **Read-only is decided by `CanEncode`.** A codec that cannot encode makes its arranger read-only, and every write path consults the arranger check (docs/ARCHITECTURE.md). Reason: decode-only plugin codecs (for example compressed fonts) could be drawn on and failed only on save.
- **Direct-color XML codecs are not supported for 1.0.** The schema accepts `colortype` `direct`, but the factory throws `NotSupportedException`.
- **Generalized codecs use `PackedBits`, not `BitStream`.** Flow and pattern codecs precompute per-bit tables and read and write bits directly. Reason: speed. `BitStream` remains for its tests and the FF5 samples; the sample plugins carry their own bit helper (LIB-PLUGINS).
- **Codec rework behavior changes (1.0 release notes).** Genesis 4bpp, SNES Mode 7, Virtual Boy 2bpp and NGPC 2bpp had reversed bit order and now show correct colors, which differ from earlier versions. N64 RGBA32 reads big-endian `.z64` order; `.v64` dumps must be converted first. N64 RGBA16 reports 16 bpp and no longer zero-fills the second half of its storage on save. PSX 16bpp preserves the STP bit. The app ships every codec XML; pattern codecs and SNES 3bpp Flow were missing from earlier builds.

## Non-goals

- Compressed graphics (see [the compression proposal](../../changes/compression-support.md)).
- Direct-color XML codecs, XML format extensions (stride, padding, endianness, per-tile headers), codec auto-detection and a codec authoring UI.
- Bit-aligned sequential arranger offsets (LIB-ARRANGERS).

## Open items

- `PatternList` checks `mapIndex > patternSize` where the largest valid index is `patternSize - 1`, so an index equal to the pattern size is accepted.
- The chunky pattern check `letterCount > maxInstancesPerCharacter` runs before the increment, so a symbol may occur one more time than the limit.
- Planar pattern count mismatch and empty patterns throw `ArgumentException` while every other pattern error returns a failed result.
- The XML reader rejects a default width or height of 1 while the schema accepts any positive integer.
- No tests cover XML schema failures, semantic validation, unknown codec names, or the direct-XML `NotSupportedException`.
