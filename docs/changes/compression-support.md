# Read-only compressed graphics

## Why

Most commercial ROM graphics are compressed (LZ77/LZSS, RLE, Huffman, game-specific schemes), and TileShop cannot read any of them (backlog: LIB-CODECS P1 "Compression support"). Users want to view, arrange and export those graphics. The sample plugins `MarmaladeBoyCodec` and `LastArmageddonCodec` show the workaround today: a codec that reads a fixed-size window and catches the exception when it runs past the end.

## What

A compressed block becomes a virtual data source whose bytes are the decompressed data. Everything above the data source layer works on it unchanged: every codec (XML flow and pattern, specialized, plugin), rendering, arranging, mirror/rotation, palettes, image export, and sequential browsing, scrolling, Jump to Offset and the offset display, all in decompressed coordinates.

A compressed source is read-only. Arrangers over one can be viewed, arranged and exported, but Draw mode, Import and Save are disabled, exactly as for an arranger whose codec cannot encode (LIB-ARRANGERS `IsReadOnly`). Element pastes that only rearrange references stay allowed.

New library types: `IDataCompressor` (`Name`, and `Decompress(DataSource, BitAddress)` returning the bytes and the compressed length consumed) and `CompressedDataSource` (`Parent`, `ParentAddress`, `Compressor`, `CompressedLength`, `IsReadOnly` true). There is no `Compress` method.

Projects gain a resource type that references its parent data file:

```xml
<compresseddata parent="Data/Rom" fileoffset="1A2B30" compression="GBA LZ77" />
```

Users who need to edit compressed graphics export the decompressed bytes as a `.bin`, edit it as an uncompressed data file, and recompress and reinsert it with game-specific tooling.

## Decisions

- **A decompressing `DataSource`, not a compression codec.** Every element is assumed to be exactly `StorageSize` bits at `SourceAddress`: sequential layout and moves, `ReadElement`/`WriteElement`, the save-conflict overlap ranges, the scrollbar and the codec isolation tests rely on it. A compressed block decodes to many tiles, and reaching tile N needs everything before it decompressed. `DataSource`'s reads go through an overridable stream, so a subclass over a `MemoryStream` of decompressed bytes gets every read path for free, and elements keep referencing a source by key. Rejected: compression codecs, which only work as read-only whole-block elements.
- **Read-only by design.** Writing back means recompressing; the result rarely matches the original size or bytes, and when it grows the block must move, the game's pointers to it be updated, and often an archive rebuilt, all game-specific. Rejected: write-back. `IDataCompressor` can later gain an optional `Compress` without breaking compressors.
- **No `Compress` method.** Read-only support never needs one, and leaving it out keeps compressors small and stops plugin authors implementing half a feature.
- **One read-only gate.** `DataSource.IsReadOnly` (virtual, false) joins `Codec.CanEncode` in the existing arranger `IsReadOnly` check, so compressed sources reuse every UI and library refusal already in place.
- **Lazy, cached decompression.** Decompress on first access to the stream and cache it; most blocks are a few KB to a few hundred KB. `CompressedLength` is informational: the UI shows it and it marks the parent range covered; it is never used for writing.
- **Staleness: a parent write version.** `DataSource` gains a write-version counter (or `CompressedDataSource` listens to the parent's `DataWritten`), and the compressed source re-decompresses on next access after the parent changed. Rejected for now: accepting staleness until reload (the view lies), and blocking saves into covered parent ranges (more than needed to start; listed as later work).
- **Well-specified formats first.** Generic "RLE" and "LZSS" are barely standards: games vary window size, flag bit order, length encoding and initial fill. Build the GBA/NDS BIOS formats first and leave game-specific schemes to plugins.
- **A missing compressor loads as a broken resource.** An unknown compressor name (for example a missing plugin) loads the resource with an error instead of failing the whole project, like a missing data file (LIB-PROJECT-SERVICE relink).

## Spec changes

- **New spec `docs/specs/lib/compression.md` (LIB-COMPRESSION):** `IDataCompressor`, `CompressedDataSource`, built-in compressors, staleness, malformed-input behavior (throws a clear exception, never hangs), exact `CompressedLength`.
- **LIB-DATASOURCE:** reuse `IsReadOnly` (LIB-DATASOURCE-032) and the read-only write guards (LIB-DATASOURCE-033), overriding `IsReadOnly` to true; add the write version or parent-change hook.
- **LIB-ARRANGERS:** reuse the arranger read-only check over sources (LIB-ARRANGERS-009) and its reason text (LIB-ARRANGERS-049), which should name the compressed source.
- **LIB-CODECS:** `PluginService` also discovers `IDataCompressor` types and registers them by `Name`.
- **LIB-PROJECT-FORMAT:** the `compresseddata` resource (XSD root element with `parent`, `fileoffset`, optional `bitoffset`, `compression`), model, reader, writer and mapper; load order resolves compressed sources after their parents.
- **LIB-PROJECT-SERVICE:** deleting or renaming the parent cascades like other linked resources; deleting a compressed source unlinks the arrangers that reference it; code that pattern-matches or casts to `FileDataSource` (the XML writer, serialization mapper extensions, `ProjectTreeBuilder`) gains a `CompressedDataSource` branch.
- **UI-PROJECT-TREE / UI-EDITORS:** add compressed data from a data file's context menu or a sequential editor at the current offset; a compressed source opens in a sequential editor like a data file; a lock badge marks read-only state.
- **UI-GRAPHICS-EDITOR:** offsets in a compressed source's editor read "Decompressed offset 0x…" plus the parent location; Draw, Import and Save disabled through the existing read-only gate.
- **CLI-COMMANDS:** import skips read-only arrangers and reports them; export unchanged.

## Tasks

1. **Core library.** Reuse `DataSource.IsReadOnly` and the arranger check over sources (both already built); add `IDataCompressor`, `CompressedDataSource`, GBA/NDS LZ77 (`0x10`) and RLE (`0x30`). Tests: known-answer vectors per format; malformed input (truncated data, out-of-range back-references, a size header past the parent's end) throws and never hangs; `CompressedLength` exact; `Length` equals the decompressed size and every write overload throws; a parent change triggers re-decompression; `SaveImage` and `ImageImporter` over a compressed source refuse before writing and leave the parent's bytes unchanged (sentinel-byte check); the image export suites run over a `CompressedDataSource` and match the same tiles stored uncompressed.
2. **Project integration.** XSD, model, reader, writer, mapper, the `FileDataSource` cast sites, load order, cascades. Tests: write and read back a project with compressed resources, including one whose compressor plugin is missing.
3. **UI.** Add compressed data (pick a compressor, validate by decompressing, show decompressed and compressed sizes, then name and add), open it, offset labels, lock badge, "Export decompressed bytes" to `.bin`. Manual: add, open, browse and export a compressed block in the running app.
4. **Plugins.** Compressor discovery in `PluginService`; port `LastArmageddonCodec` as a compressor (its row-mask format is a sparse encoding that outputs plain 1bpp tiles an XML codec can display); port `MarmaladeBoyCodec` the same way if its format allows (variable-width glyphs may still need a codec, but reading from a decompressed source removes the fixed-window over-read).
5. **Later** (back to the backlog if not done here): GBA/NDS Huffman (`0x20`, 4- and 8-bit symbols), configurable LZSS (window size, fill byte, flag bit order, length/offset bit split), scanning a data file for candidate blocks (e.g. valid GBA LZ77 headers at 4-byte-aligned offsets), a warning when an uncompressed arranger saves into a parent range covered by a compressed source.

## Open questions

- Write-version counter on `DataSource` or a subscription to the parent's `DataWritten`? `DataWritten` is not raised from `Flush` (LIB-DATASOURCE), so a version bumped in `Write` is the more complete signal.
- Should the project tree show compressed sources as children of their parent data file, or as siblings in the folder?
