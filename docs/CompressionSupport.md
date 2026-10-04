# Compressed Graphics Support Plan

Goal: let users view, arrange and export graphics stored in compressed blocks (LZ77, RLE, Huffman and game-specific schemes), with compressors written in C# as built-ins or plugins.

**Read-only by design.** Writing compressed data back properly means recompressing, and the result rarely matches the original size or bytes. When it grows, the block has to be moved, the game's pointers to it updated, and often a whole archive rebuilt. All of that is game-specific. A compressed block can therefore be viewed, arranged and exported, but never edited or saved through TileShop. See [Out of scope](#out-of-scope-write-support).

---

## Design: a decompressing `DataSource`

A compressed block becomes a virtual data source whose bytes are the decompressed data. Everything above the data source layer works on it unchanged.

### Why not a compression codec

Every element is assumed to be exactly `StorageSize` bits at `SourceAddress`. `SequentialArranger` layout and moves, `ReadElement`/`WriteElement`, the overlap ranges in `ArrangerSaveConflictExtensions`, the UI scrollbar and the Phase 1 isolation tests all rely on this. A compressed block breaks the assumption in two ways:

- It decodes to many tiles, not one.
- Reaching tile N requires decompressing everything before it.

A compression codec only works for read-only, whole-block "elements". The sample plugins `MarmaladeBoyCodec` and `LastArmageddonCodec` show the result: they read a fixed-size window and catch the exception when they run past the end.

### Why a `DataSource` fits

- `DataSource` is abstract, its members are virtual, and its reads go through `protected abstract Lazy<Stream> Stream`. A subclass that provides a `MemoryStream` of decompressed bytes gets every existing read path for free.
- Every codec works unchanged: XML flow and pattern, specialized, and plugin. Rendering, arranging, mirror/rotation and image export work too, because they all see a plain source whose `Length` is the decompressed size.
- `SequentialArranger` scrolling, Jump to Offset and the offset display work in decompressed coordinates. That is the useful coordinate space inside a compressed block.
- Palettes also read through `DataSource`, so compressed palettes work the same way.
- Arranger elements reference a source by key (`datafile=`), so the arranger model and project format for elements don't change.

### Core types

```csharp
public interface IDataCompressor
{
    string Name { get; }

    /// <summary>
    /// Decompresses the block starting at address, and reports how many bytes of compressed input it consumed.
    /// </summary>
    DecompressResult Decompress(DataSource source, BitAddress address);
}

public readonly record struct DecompressResult(byte[] Data, long CompressedLength);

public sealed class CompressedDataSource : DataSource
{
    public DataSource Parent { get; }
    public BitAddress ParentAddress { get; }
    public IDataCompressor Compressor { get; }
    public long CompressedLength { get; }

    public override bool IsReadOnly => true;
    protected override Lazy<Stream> Stream { get; }   // read-only MemoryStream over the decompressed bytes
}
```

- There is no `Compress` method. Read-only support never needs one, and leaving it out keeps compressors small and plugin authors from implementing half a feature.
- `CompressedLength` is informational. The UI shows it, and it marks the parent range the block covers. It is not used for writing.
- Decompression is lazy, on first access to `Stream`, and the result is cached. Most blocks are a few KB to a few hundred KB.

### Making read-only enforceable

The codebase has no read-only concept for graphics yet. `IGraphicsCodec.CanEncode` exists, and the sample plugins set it to `false`, but nothing checks it. Read-only compressed sources and non-encoding codecs should share one gate:

- [ ] Add `public virtual bool IsReadOnly => false;` to `DataSource`. In `CompressedDataSource`, each `Write`/`WriteAsync` overload throws `InvalidOperationException`, and `Flush` does nothing.
- [ ] Add an arranger-level check, for example `ArrangerExtensions.IsReadOnly(this Arranger)`. It is true when any element's `Source.IsReadOnly` is true or its `Codec.CanEncode` is false.
- [ ] Library: `IndexedImage.SaveImage`, `DirectImage.SaveImage` and `ImageImporter` refuse read-only arrangers with a clear exception, before any element is written.
- [ ] UI: `GraphicsEditorViewModel` already has `CanDraw`, `CanAcceptPixelPastes` and `CanRemapColors` gates (currently hardcoded `true` at `GraphicsEditorViewModel.cs:322`). Drive them from the arranger check, and disable Import Image and Save for read-only arrangers. Element pastes that only rearrange references stay allowed.
- [ ] CLI: the import command skips read-only arrangers and reports them. Export works unchanged.

This also fixes the existing gap where a `CanEncode == false` plugin codec can be drawn on, and fails only when saved.

### Staleness when the parent changes

A compressed source caches data decompressed from its parent. If another arranger edits the parent bytes inside the compressed range, the cache goes stale. `DataSource` has no change notification today. Options, from simplest to most complete:

1. Accept staleness until the project is reloaded, and document it.
2. Add a write-version counter to `DataSource`. `CompressedDataSource` re-decompresses on next access when the parent's version has changed.
3. Also warn, or block, when an uncompressed arranger saves into a parent range covered by a compressed source.

Start with option 2. It's a few lines, and it keeps the view honest.

---

## Project format

Each resource is its own XML file, validated by `_schemas/ResourceSchema.xsd`. Add a new resource type that references its parent data file:

```xml
<compresseddata parent="Data/Rom" fileoffset="1A2B30" compression="GBA LZ77" />
```

- [ ] XSD: a new root element `compresseddata` with `parent`, `fileoffset`, optional `bitoffset`, and `compression`.
- [ ] Serialization: a model class, reader and writer cases, and mapper entries, alongside `DataFileModel`.
- [ ] Load order: resolve compressed sources after their parent data files. An unknown compressor name (for example a missing plugin) loads as a broken resource with an error, rather than failing the whole project.
- [ ] Linked resources: deleting or renaming the parent cascades like other linked resources. Deleting a compressed source unlinks the arrangers that reference it.

### Code that assumes every data source is a file

These sites pattern-match or cast to `FileDataSource` and need a `CompressedDataSource` branch:

- `XmlProjectWriter.cs:80`, `:123` and `:184`. The last one is a hard cast that would throw.
- `TileShop.UI/Features/Shell/EditorsViewModel.cs:144`, which opens a data file in a new `SequentialArranger`. Compressed sources should open the same way.
- `SerializationMapperExtensions.cs:12-14`
- `ProjectTreeBuilder`

---

## Compressors

Generic "RLE" and "LZSS" are barely standards. Most games vary the window size, flag bit order, length encoding or initial fill. Build the well-specified formats first, and leave game-specific ones to plugins.

| Priority | Compressor | Notes |
|---|---|---|
| P1 | GBA/NDS BIOS LZ77 (`0x10`) | The header includes the decompressed size, so a block is easy to validate. Covers a large share of GBA and DS games. |
| P1 | GBA/NDS BIOS RLE (`0x30`) | Same header scheme |
| P2 | GBA/NDS BIOS Huffman (`0x20`) | 4-bit and 8-bit symbol variants |
| P2 | Configurable LZSS | Window size, fill byte, flag bit order, and length/offset bit split as parameters. Covers many SNES and PSX variants without code. |
| Plugin | Game-specific schemes | Through `PluginService`, as below |

### Plugins

- [ ] Extend `PluginService` to discover `IDataCompressor` types as well as `IGraphicsCodec`, and register them by `Name`. Compressors need no `Palette`, so the plugin loader's parameterless-constructor problem (`FeatureGaps.md`) doesn't apply.
- [ ] Port `LastArmageddonCodec` as a compressor. Its row-mask format is really a sparse encoding that outputs plain 1bpp tiles, which an ordinary XML codec can then display.
- [ ] Port `MarmaladeBoyCodec` the same way if its format allows. Variable-width glyph data may still need a codec, but reading it from a decompressed source removes the fixed-window over-read.

---

## UI

- [ ] **Add compressed data:** from a data file's context menu or the sequential editor at the current offset. Pick a compressor, validate by decompressing, show the decompressed and compressed sizes, then name and add the resource.
- [ ] **Label offsets clearly:** in a compressed source's editor, show "Decompressed offset 0x…" plus the parent location, so users don't mistake it for a file offset.
- [ ] **Show read-only state:** a lock badge in the project tree and editor title, with Draw mode, Import and Save disabled as above.
- [ ] **Export decompressed bytes:** save the decompressed block as a `.bin`, for users who recompress with external tools. This is the supported path for editing compressed graphics.
- [ ] **Later:** scan a data file for candidate blocks, for example by validating GBA LZ77 headers at 4-byte-aligned offsets.

---

## Testing

The Phase 1 codec harness applies directly.

- [ ] **Compressors:** known-answer vectors per format, from spec examples or small hand-built inputs. Malformed input (truncated data, out-of-range back-references, a size header beyond the parent's end) throws a clear exception and never hangs. `CompressedLength` is reported exactly.
- [ ] **Data source:**
  - `Length` equals the decompressed size, reads match the decompressed bytes, and every write overload throws.
  - A parent-version change triggers re-decompression.
- [ ] **Read-only enforcement:** `SaveImage` and `ImageImporter` on an arranger over a compressed source throw before writing, and the parent's bytes are unchanged (the sentinel-byte check from the isolation tests). Do the same for a codec with `CanEncode == false`.
- [ ] **End to end:** the existing image export suites run over a `CompressedDataSource`, and decode results match the same tiles stored uncompressed.
- [ ] **Project format:** writing and reading back a project with compressed resources, including a missing compressor plugin.

---

## Phases

1. **Core library:** `IDataCompressor`, `CompressedDataSource`, `DataSource.IsReadOnly`, the arranger read-only check enforced in `SaveImage` and `ImageImporter`, GBA LZ77 and RLE, and tests. No UI is needed to test any of this.
2. **Project integration:** XSD, reader, writer and mapper, the `FileDataSource` cast sites, and linked-resource cascades.
3. **UI:** add, open and export compressed data, offset labels, read-only gating of Draw, Import and Save.
4. **Plugins:** compressor discovery in `PluginService`, then port the sample plugins.
5. **Later:** Huffman, configurable LZSS, block scanning, a parent-overlap warning on save.

---

## Out of scope: write support

Not planned:

- Recompressing edited data and writing it back to the parent.
- Moving blocks that grow, updating pointer tables, or rebuilding archives.
- Overlap and conflict handling for writes into compressed ranges.

Users who need to edit compressed graphics can export the decompressed bytes, edit them as an uncompressed data file in TileShop, and recompress and reinsert them with game-specific tooling. If write support is revisited, `IDataCompressor` can gain an optional `Compress` method without breaking existing compressors.
