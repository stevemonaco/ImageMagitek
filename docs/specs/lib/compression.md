---
id: LIB-COMPRESSION
title: Read-only compressed data sources
status: draft
project: ImageMagitek.Plugins.Contracts, ImageMagitek, ImageMagitek.Services
sources:
  - ImageMagitek.Plugins.Contracts/ICompressionPlugin.cs
  - ImageMagitek.Plugins.Contracts/CompressionInfo.cs
  - ImageMagitek.Plugins.Contracts/DecompressionResult.cs
  - ImageMagitek/Compression
  - ImageMagitek/CompressedDataSource.cs
  - Samples/ImageMagitek.PluginSamples/Compression
types:
  - ICompressionPlugin
  - CompressionInfo
  - DecompressionResult
  - ICompressorFactory
  - CompressorFactory
  - GbaLz77Compression
  - GbaRleCompression
  - CompressedDataSource
tests:
  - CompressionFormatTests
  - CompressorFactoryTests
  - CompressedDataSourceTests
  - SampleCompressionPluginTests
  - CompressedDataProjectTests
depends:
  - LIB-DATASOURCE
  - LIB-PLUGINS
---

# Read-only compressed data sources

## Purpose

Lets arrangers read graphics that a game stores compressed. A compressed block becomes a `CompressedDataSource` whose bytes are the decompressed data, so every codec, arranger, image and export path works on it unchanged (LIB-DATASOURCE). Compression formats are `ICompressionPlugin` transforms: two are built in and more come from plugins (LIB-PLUGINS). The source is read-only, so the arranger read-only gate (LIB-ARRANGERS) blocks drawing, import and save. LIB-PROJECT-FORMAT stores it as a `compresseddata` resource.

## Requirements

### Contract

- **LIB-COMPRESSION-001** — `ICompressionPlugin.Decompress` shall receive the bytes from the block's start to the end of the parent, at most `CompressorFactory.MaxInputLength` (16 MiB), and return the decompressed bytes and the number of input bytes consumed. A plugin shall signal malformed or truncated input by throwing.
  - Tests: `CompressionFormatTests.Lz77_BackReference_Decompresses`, `CompressionFormatTests.Lz77_TruncatedInput_Throws`
- **LIB-COMPRESSION-002** — If a type passed to `AddCompressionPlugin` is abstract, does not implement `ICompressionPlugin`, has no public parameterless constructor, throws while being constructed or reading `Info`, or has a blank `Info.Name`, then registration shall fail naming the type and the reason.
  - Tests: `CompressorFactoryTests.AddCompressionPlugin_InvalidType_FailsWithReason`
- **LIB-COMPRESSION-003** — When a compression plugin is registered, it shall be registered under `Info.Name`; if the name is already registered (case-insensitive), registration shall fail and keep the first registration.
  - Tests: `CompressorFactoryTests.AddCompressionPlugin_NameCollision_KeepsFirst`
- **LIB-COMPRESSION-004** — When a compressor is created by name, the factory shall return a new instance, or null for an unknown name.
  - Tests: `CompressorFactoryTests.Create_KnownAndUnknownNames`

### Built-in formats

- **LIB-COMPRESSION-010** — `GBA LZ77` shall decompress the GBA/NDS BIOS LZ77 format: a `0x10` header byte, a 24-bit little-endian decompressed size, then flag bytes read MSB-first where 0 is a literal byte and 1 is a 2-byte back-reference of length `(b1 >> 4) + 3` and distance `((b1 & 0xF) << 8 | b2) + 1`.
  - Tests: `CompressionFormatTests.Lz77_Literals_Decompress`, `CompressionFormatTests.Lz77_BackReference_Decompresses`
- **LIB-COMPRESSION-011** — If a `GBA LZ77` block has a different header byte, or a back-reference reaches before the start of the output, then decompression shall throw `InvalidDataException`.
  - Tests: `CompressionFormatTests.Lz77_WrongHeader_Throws`, `CompressionFormatTests.Lz77_DistanceBeforeStart_Throws`
- **LIB-COMPRESSION-012** — `GBA RLE` shall decompress the GBA/NDS BIOS RLE format: a `0x30` header byte, a 24-bit size, then flag bytes where bit 7 set repeats the next byte `(flag & 0x7F) + 3` times and bit 7 clear copies the next `(flag & 0x7F) + 1` bytes.
  - Tests: `CompressionFormatTests.Rle_RunsAndLiterals_Decompress`
- **LIB-COMPRESSION-013** — Both built-in formats shall stop at the declared size, report the input consumed up to that point, and throw `InvalidDataException` when the input ends first.
  - Tests: `CompressionFormatTests.Lz77_TruncatedInput_Throws`, `CompressionFormatTests.Rle_TruncatedInput_Throws`, `CompressionFormatTests.Lz77_ReportsConsumedLength`

### Compressed data source

- **LIB-COMPRESSION-020** — A `CompressedDataSource` shall read as the decompressed bytes of the block at `ParentOffset` in `Parent`, decompressing on first access and caching the result; `CompressedLength` shall be the input consumed.
  - Tests: `CompressedDataSourceTests.Read_ReturnsDecompressedBytes`
- **LIB-COMPRESSION-021** — A `CompressedDataSource` shall be read-only: `IsReadOnly` is true and every write overload throws `InvalidOperationException` without touching the parent.
  - Tests: `CompressedDataSourceTests.Write_Throws_ParentUnchanged`
- **LIB-COMPRESSION-022** — If decompression fails, the compressor name is not registered, or the parent cannot be read, then the source shall have length 0 and `DecompressionError` shall describe the failure; reading the source shall not throw for that reason.
  - Tests: `CompressedDataSourceTests.UnknownCompressor_HasErrorAndZeroLength`, `CompressedDataSourceTests.MalformedBlock_HasErrorAndZeroLength`
- **LIB-COMPRESSION-023** — When the parent raises `DataWritten`, the source shall discard its cached data, decompress again on next access, and raise its own `DataWritten`.
  - Tests: `CompressedDataSourceTests.ParentWritten_Redecompresses`
- **LIB-COMPRESSION-024** — `Invalidate` shall discard the cached data and error so the next access decompresses again, as after a parent relink.
  - Tests: `CompressedDataSourceTests.Invalidate_RetriesAfterParentFixed`
- **LIB-COMPRESSION-025** — An arranger whose elements read a `CompressedDataSource` shall be read-only with a reason naming the compressed source (LIB-ARRANGERS read-only gate).
  - Tests: `CompressedDataSourceTests.Arranger_OverCompressedSource_IsReadOnly`
- **LIB-COMPRESSION-026** — A palette whose file or scattered colors read a `CompressedDataSource` shall load its colors from the decompressed bytes and be read-only (LIB-PALETTES `IsReadOnly`): `SavePalette` returns false without writing, and the palette editor and the graphics editor's color flyout offer no edits.
  - Tests: `CompressedDataSourceTests.Palette_OverCompressedSource_IsReadOnly`; manual — FireRed `/Pokemon/Palettes/005 Charmeleon` opens with the "Read Only" badge

### Plugins and samples

- **LIB-COMPRESSION-030** — When plugins are loaded, the plugin service shall collect every non-abstract `ICompressionPlugin` type alongside codec types, and the bootstrapper shall register each with the compressor factory, recording failures as startup issues (LIB-PLUGINS-004).
  - Tests: `PluginServiceTests.LoadPlugins_Samples_DiscoversCompressionPlugins`, `BootstrapServiceTests.CreatePluginService_RegistersCompressionPlugins`
- **LIB-COMPRESSION-031** — The sample plugins shall include at least one compression format implemented against the contracts assembly only.
  - Tests: `SampleCompressionPluginTests.SquareLzss_LiteralsAndBufferReference_Decompress`, `SampleCompressionPluginTests.SquareLzss_ReferenceToUnwrittenBuffer_ReadsZeros`, `SampleCompressionPluginTests.SquareLzss_LengthPastInput_Throws`, `SampleCompressionPluginTests.SquareLzss_RegistersWithCompressorFactory`; manual — FF5 (J) offset `0x37E4D` decompresses `0x4800` bytes to `0xF983` bytes of 65816 code

## Invariants

- A compressed source never writes to its parent.
- `Length` equals the decompressed size, or 0 while `DecompressionError` is set.
- A plugin receives only a byte span and returns only bytes; it never sees a data source.

## Edge cases

- A block at the very end of the parent receives a short input span; a format that needs more throws, which becomes `DecompressionError`.
- A parent shorter than `ParentOffset` gives an error, not an exception.
- A missing parent file (LIB-DATASOURCE) gives an error until relinked and invalidated.
- Two compressed sources over overlapping parent ranges are independent.

## Threading and lifetime

- Decompression runs on the first reader's thread under a thread-safe `Lazy`; other readers wait for it.
- The source subscribes to the parent's `DataWritten` in its constructor and unsubscribes in `Dispose`.
- Each `Decompress` call uses a new plugin instance, so plugins may keep state between calls on one instance without locking.

## Decisions

- **A decompressing data source, not a codec.** Elements stay fixed-size reads at addresses, so every existing path works on decompressed coordinates. Rejected: compression codecs, which only work as read-only whole-block elements (see the change proposal).
- **The contract takes a span and returns bytes.** `Decompress(ReadOnlySpan<byte>)` with the bytes to the end of the parent, capped at 16 MiB. Reason: compressed formats are sequential and small, a span is the simplest thing for a plugin author, and no stream lifetime crosses the boundary. Rejected: a `Stream` (seeking and disposal rules for plugin authors); a plugin-declared input length (most formats only know their length after decoding).
- **Writing is a later, separate interface.** A future minor contract version adds an interface with `Compress`; a source becomes writable only when its compressor implements it and the recompressed block fits in `CompressedLength`. Reason: read-only plugins never change, and the in-place size rule keeps "writes never grow a data source" (ARCHITECTURE §6). Rejected: a `CanCompress` flag on `CompressionInfo` now (an unused member frozen into the contract).
- **New plugin instance per decompression.** Reason: matches codec plugins' one-instance-per-use rule and needs no thread-safety from plugins.
- **Failures become an error state, not a load failure.** Reason: a project must open when a compressor plugin is missing, like a missing data file. The UI shows `DecompressionError` when the source is opened.
- **Compressed sources are siblings of their parent in the tree.** Reason: data files cannot hold children (`CanContainChildResources` is false) and the format stores one resource per file in folders. Rejected: nesting under the data file node (a tree-model change).

## Non-goals

- Writing compressed data back; relocating or rebuilding blocks.
- Scanning for compressed blocks (see the container-scanners proposal).
- Editing compressed palettes; they are read-only like compressed graphics (LIB-COMPRESSION-026).

## Open items

- Decompressed offsets in the sequential editor are shown like file offsets, with no parent location.
