# Codec Rework Plan

Goal: make the generalized codecs (`IndexedFlowGraphicsCodec`, `IndexedPatternGraphicsCodec`) faster and leaner without changing a single encoded byte or decoded pixel. Codec round-trip regressions have been one of the hardest problems in this project, so no codec code changes until Phase 1 is in place and green.

**Scope:** XML-defined flow and pattern codecs, plus the shared encode/decode pipeline (`IndexedImage`, `ImageImporter`, arrangers, data sources). Specialized C# codecs are used only as reference implementations in Phases 1–3. Phase 4 extends the work to them.

**Non-goals:** Plugin and other custom codecs. Their correctness belongs to their author. Phase 1's harness should still be reusable, so a plugin author can run the same contract tests against their codec.

---

## Phase 1: Lock down current behavior with integration tests

Exit criteria: every flow and pattern codec in `ImageMagitek/_codecs/` is covered by the suites below, `dotnet test ImageMagitek.UnitTests` passes, and any known bugs that tests expose are recorded in this document with a skipped test that names them.

### 1.0 Current coverage and test infrastructure fixes

Current state of coverage:

- `ScatteredArrangerReversibilityTests` is the only round-trip test. It has 5 cases: NES 1bpp, NES 2bpp, SNES 4bpp and Genesis 4bpp (flow), plus SNES 3bpp (the specialized C# codec, not the flow codec). All cases use 8x8 elements.
- No pattern codec is tested.
- `GraphicsCodecTests`, `GraphicsCodecTestCases` and `IndexedImageTests` are empty stubs.
- The round-trip compares RGBA output, so it can't catch index changes between duplicate palette colors.
- Nothing checks encoded bytes against known-good data. A bug that is symmetric in encode and decode passes the round-trip.

Infrastructure fixes:

- [x] `ScatteredArrangerReversibilityTests` exports to `test.png` in the working directory, which races between parallel test cases. Change it to write to a unique temp path per test.
- [x] `TestImages.DragonForm` points to `dragon_from_8bpp.png`, but the file on disk is `dragon_form_8bpp.png`. Resolved by replacing all licensed test images with generated ones (`TestImageGenerator`).
- [x] Replace the empty stubs with the suites below, or delete them.
- [x] Add an indexed-image assert that compares palette indices, alongside the existing `ImageRgba32Assert`.

### 1.1 Codec contract tests (per codec, no arranger)

A reusable, parameterized suite that runs against any `IIndexedCodec`. The cases come from enumerating the codecs loaded by `CodecFixture`, so a new XML codec gets covered automatically.

- [x] **Pixels → bytes → pixels:** random pixel buffers with values in `[0, 2^ColorDepth)` round-trip exactly.
- [x] **Bytes → pixels → bytes:** random byte buffers of `StorageSize` bits round-trip exactly. Every flow and pattern codec maps bits one-to-one, so this should hold for all of them; any exception gets documented.
- [x] **Sizes:** default size for every codec. Resizable flow codecs also need non-default sizes that are multiples of `WidthResizeIncrement`. Include non-square sizes with width greater than height and with height greater than width (for example 16x8 and 8x16), and at least one larger single-layout size (for example PSX at 128x64).
- [x] **Buffer reuse:** decoding A, then B, then A again gives the same result as decoding A alone. This catches stale state in shared buffers, which matters once Phase 2 reuses buffers more aggressively.
- [x] **Input validation:** too-short encoded buffers and wrong-size image buffers throw as they do today.

### 1.2 Golden byte tests (known answers)

A round-trip alone can't detect a symmetric bug, so these tests pin the actual encoded bytes.

- [x] **Hand-verified known answers:** for each format family, at least one small tile whose bytes are written out by hand from platform documentation. Families: SNES/GB 2bpp row-interlaced, NES planar, SNES 3bpp/4bpp, Genesis 4bpp chunky, GBA 4bpp nibble-swapped chunky, GBA 8bpp, PSX 4bpp/8bpp, Mode 7, VB, and NGPC. The 1bpp font and pattern codecs (CotM Font, FF5 Font, FF5 Pattern, Tokimemo 1bpp) have no independent documentation, so their `*_FromXmlSemantics_*` tests derive the expected bytes from the shipped XML's own layout. These tests pin current behavior but would not catch an XML layout that is wrong for the real game.
- [x] **Golden snapshots:** `CodecGoldenTests` encodes a gradient image and a seeded random image with each codec at each size from 1.1, and writes the bytes as hex into a [Verify](https://github.com/VerifyTests/Verify) snapshot at `ImageMagitek.UnitTests/CodecTests/Snapshots/<codec>_<w>x<h>.verified.txt`. On a mismatch Verify writes a `.received.txt` next to it. To accept an intentional change, review the diff and rename the `.received.txt` file to `.verified.txt` (or use a Verify diff tool). Review snapshot changes in the diff like any other code change. The row-interlaced non-square snapshots recorded decode only until the Phase 2 encode fix; they now pin both directions.
- [x] **Decode goldens:** each snapshot also holds the palette indices decoded from seeded random bytes, which checks the decode direction independently of the encode sections.

### 1.3 Cross-codec equivalence

Several formats have two independent implementations, and each should agree with the other. Where they disagree, record the disagreement here and decide which one is right before Phase 2. Don't assume either one is the reference.

- [x] SNES 3bpp Flow (XML) ↔ SNES 3bpp (specialized C#)
- [x] PSX 4bpp Flow ↔ PSX 4bpp (specialized); PSX 8bpp Flow ↔ PSX 8bpp (specialized)
- [x] SNES 4bpp (flow) ↔ SNES 4bpp Pattern
- [x] GBA 4bpp (flow) ↔ GBA4bpp Pattern
- [x] FF5 Font (flow) ↔ FF5 Pattern
- [x] NES 1bpp (XML) ↔ NES 1bpp (specialized, unregistered)

### 1.4 Full image round-trips (integration)

These tests go through the real pipeline: arranger → `IndexedImage` / `ImageImporter` → codec → `DataSource` → back again. They compare palette indices and, where an image file is involved, RGBA as well.

- [x] **Image → data → image:** extend `ScatteredArrangerReversibilityTests` to every flow and pattern codec at its default size, plus the non-square sizes from 1.1 for resizable codecs. Use test images with matching color depth.
- [x] **Data → image → data (ROM preservation):** fill a data source with seeded random bytes, open it with an arranger, export it to PNG, re-import the PNG unmodified, and assert the data source is byte-identical. This is the guarantee users care about most: opening and re-saving graphics must never corrupt a ROM.
- [x] **Sequential arrangers:** the same two directions at a non-zero file offset, with several elements per row and column, and a codec resize mid-test.
- [x] **Neighbor isolation:** surround the target region with sentinel bytes (`0xFF`, then random), encode one element, and assert that only `[offset, offset + StorageSize)` changed. Include cases where `StorageSize` isn't a multiple of 8, or the element starts at a non-byte-aligned bit address, wherever a codec's size allows it.
- [x] **Partial edits:** edit a sub-rectangle of an `IndexedImage` that isn't aligned to element boundaries, save it, and assert that untouched pixels and bytes are unchanged. `IndexedImage.SaveImage` merges the edit into a full arranger image for exactly this case.
- [x] **Mirror and rotation:** elements with each `Mirror`/`Rotation` value round-trip, covering `InverseMirrorArray2D` and `InverseRotateArray2D`.
- [x] **File-backed source:** at least one round-trip through `FileDataSource` with `Flush`, using a temp file, not just `MemoryDataSource`.

### 1.5 Benchmark baseline

- [x] Add encode benchmarks next to `Snes3BppDecodeToImage`, plus decode and encode benchmarks for a pattern codec and for a large single-layout flow codec (PSX 64x64).
- [x] Record baseline numbers (time and allocated bytes) in this document before starting Phase 2.

`CodecElementBenchmarks` measures a single element's `DecodeElement`, `EncodeElement` and `ReadElement` for the specialized SNES 3bpp codec (the native reference) and three generalized codecs. It loads the XML formats from the source `_codecs` folder, so it doesn't depend on the packaging gap below. Run it with `dotnet run -c Release --project ImageMagitek.Benchmarks -- --filter *CodecElementBenchmarks*`.

Environment: BenchmarkDotNet 0.15.8 `ShortRun`, .NET 10.0.12 X64 RyuJIT, AMD Ryzen 9 9950X, Windows 11. ShortRun timings are noisy, so treat Mean as indicative; Allocated is deterministic.

**Baseline results**

| Codec | Size | Method | Mean | Allocated |
|---|---|---|---:|---:|
| SNES 3bpp (specialized) | 8x8 | Decode | 469 ns | 48 B |
| SNES 3bpp (specialized) | 8x8 | Encode | 762 ns | 96 B |
| SNES 3bpp (specialized) | 8x8 | ReadElement | 32 ns | 48 B |
| SNES 3bpp Flow | 8x8 | Decode | 1,090 ns | 40 B |
| SNES 3bpp Flow | 8x8 | Encode | 965 ns | 136 B |
| SNES 3bpp Flow | 8x8 | ReadElement | 45 ns | 48 B |
| SNES4bpp Pattern | 8x8 | Decode | 1,397 ns | 0 B |
| SNES4bpp Pattern | 8x8 | Encode | 6,773 ns | 18,536 B |
| SNES4bpp Pattern | 8x8 | ReadElement | 35 ns | 56 B |
| PSX 4bpp Flow | 64x64 | Decode | 74,274 ns | 40 B |
| PSX 4bpp Flow | 64x64 | Encode | 60,710 ns | 2,112 B |
| PSX 4bpp Flow | 64x64 | ReadElement | 66 ns | 2,072 B |

**Before / after**

| Codec | Method | Baseline | After Phase 2 | After Phase 3 |
|---|---|---|---|---|
| SNES 3bpp Flow 8x8 | Decode | 1,090 ns / 40 B | 152 ns / 0 B | 194 ns / 0 B |
| SNES 3bpp Flow 8x8 | Encode | 965 ns / 136 B | 117 ns / 0 B | 147 ns / 0 B |
| SNES 3bpp Flow 8x8 | ReadElement | 45 ns / 48 B | 29 ns / 0 B | 36 ns / 0 B |
| SNES4bpp Pattern 8x8 | Decode | 1,397 ns / 0 B | 1,232 ns / 0 B | 272 ns / 0 B |
| SNES4bpp Pattern 8x8 | Encode | 6,773 ns / 18,536 B | 6,300 ns / 18,536 B | 186 ns / 0 B |
| SNES4bpp Pattern 8x8 | ReadElement | 35 ns / 56 B | 30 ns / 56 B | 29 ns / 0 B |
| PSX 4bpp Flow 64x64 | Decode | 74,274 ns / 40 B | 15,760 ns / 0 B | 14,021 ns / 0 B |
| PSX 4bpp Flow 64x64 | Encode | 60,710 ns / 2,112 B | 9,470 ns / 0 B | 9,182 ns / 0 B |
| PSX 4bpp Flow 64x64 | ReadElement | 66 ns / 2,072 B | 42 ns / 0 B | 34 ns / 0 B |

Phase 3 didn't touch the flow codec, so its SNES 3bpp Flow and PSX numbers in that column are run-to-run noise. The specialized SNES 3bpp codec measured about 450-500 ns and 48-96 B per call in every run; it is the unchanged native reference. With both reworks done, the generalized SNES 3bpp Flow decodes and encodes about 3x faster than the specialized codec.

---

## Phase 2: `IndexedFlowGraphicsCodec` rework

Entry criteria: Phase 1 is green, and the baseline benchmarks are recorded.

### Known bugs to fix first (each in its own commit, with golden updates if output changes)

- [x] `EncodeElement` row-interlaced path uses `pos = y * el.Height` (line 180) where decode uses `y * el.Width`. Non-square row-interlaced elements encode to the wrong positions. 1.1 should catch this; un-skip the test when fixed.
- [x] Decode loops over `el.Width`/`el.Height`, while encode loops over `Format.Width`/`Format.Height` and the buffers are sized from the codec. Standardize on the codec's `Width`/`Height`.

### Decode

- [x] Read bits directly from `encodedBuffer` with a static helper. This removes the `_foreignBuffer` copy and the per-bit `IBitStreamReader.ReadBit()` interface call with its access and bounds checks. The existing `StorageSize` guard already covers the length.
- [x] OR each bit straight into the native buffer (`native[pos] |= bit << mergePlane`) after one clear. This removes `_elementData`, `_mergedData` and two full passes over the pixels.
- [x] Index `_nativeBuffer` as a flat span via `MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(...), Length)`. This keeps the current `y * Width + column` indexing exactly.

### Encode

- [x] Compute each bit directly from `imageBuffer` as `(pixel >> mergePlane) & 1`, and pack the bits into a reused instance buffer. This removes the copy into `_mergedData`, the split into `_elementData`, and the per-call `BitStream` and `byte[]` allocations.
- [x] Returning a reused buffer is safe with today's callers (`IndexedImage.cs`, `ArrangerSaveConflictExtensions.cs`), which consume it immediately or call `.ToArray()`. Document this on `IIndexedCodec.EncodeElement`: the returned span is valid until the next call on the same codec instance.

### Shared

- [x] Precompute `RowPixelPattern` into an `int[]` of column offsets per `ImageProperty` when buffers are allocated. `RepeatList`'s indexer costs a modulo, a division and a bounds check on every bit.
- [x] Take `MergePlanePriority` lookups out of the inner loops (`AsSpan(plane, ip.ColorDepth)` per image property).
- [x] Have `ReadElement` read into a reused instance buffer instead of allocating a new `byte[]` on every call. Callers decode the result immediately.
- [x] Seal the class, and change its `virtual` members and `protected` fields to non-virtual and private, since nothing inherits from it.
- [x] Note: `stackalloc` isn't needed. Once the intermediate stages are gone, no scratch buffers are left.

Exit criteria: Phase 1 suites pass with no golden changes, apart from the bug fixes above. The benchmarks show the improvement, and allocations per decode and encode are zero.

---

## Phase 3: `IndexedPatternGraphicsCodec` rework

Same approach as Phase 2, applied to the pattern codec's hot paths:

- [x] Encode calls `bs.SeekAbsolute(index)` followed by `WriteBit` for every bit, and builds a `PlaneCoordinate` and calls `GetEncodeIndex` each time. Precompute the bit-index ↔ (plane, x, y) mapping once per codec, and write bits directly into a reused buffer.
- [x] Decode reads through `IBitStreamReader` into a `List<int[,]>` of plane images, then merges. Read bits directly from the span and OR them into the native buffer, as in Phase 2.
- [x] Encode allocates a new `BitStream` and `byte[]` per call, and `ReadElement` allocates per call. Fix both the same way as in Phase 2.
- [x] Check the `MergePlanePriority` semantics against the flow codec. The pattern codec shifts by `MergePlanePriority[i]`, while the flow codec indexes planes by it. 1.3's equivalence tests should confirm whether both are correct for their XML definitions.

---

## Phase 4: Follow-ups

Work through these in order. Each item lists what it depends on.

### 4.1 Fix the codec packaging gap

- [ ] Copy every XML in `ImageMagitek/_codecs` to output, and remove the stale `_codecs\SNES3bpp.xml` entry from `ImageMagitek.csproj` (see the Findings log). The simplest fix is a wildcard item instead of per-file entries.
- [ ] Add a test that compares the codec XMLs in the build output with the source folder, so the gap can't come back.

### 4.2 Contract and golden tests for the direct-color codecs

Phase 1 only covers indexed codecs. The 7 direct-color codecs (BMP 24, N64 RGBA16/32, PSX 16/24bpp, RGB24 Tiled, RGBA32 Tiled) need coverage before 4.4 changes them.

- [ ] Extend the contract suite (round-trips both ways, buffer reuse, input validation) to `IDirectCodec`, using `ColorRgba32` pixel buffers.
- [ ] Add Verify snapshots of encoded bytes and decoded pixels, and hand-verified known-answer pixels per format.
- [ ] Add arranger round-trips and neighbor-isolation tests through `DirectImage`.
- [ ] Record any bugs these tests expose in the Findings log, skipped with a reason, as in Phase 1. `FeatureGaps.md` already notes that N64 RGBA16 reports a 32-bit color depth and storage size.

### 4.3 Retire specialized codecs that duplicate XML codecs

Depends on 4.1, because the XML replacements have to ship.

- [ ] SNES 3bpp, PSX 4bpp and PSX 8bpp have XML equivalents that the 1.3 equivalence tests show produce identical bytes, and the XML versions are now faster. Remove the C# versions and point their registered names at the XML definitions.
- [ ] Check that existing project files referring to those codecs by name still load, through a name alias or a migration.
- [ ] Remove the unregistered C# NES 1bpp codec, which duplicates the XML codec's name (`FeatureGaps.md`).
- [ ] Turn the equivalence tests for removed codecs into known-answer tests, or delete them.

### 4.4 Apply the buffer contract to the remaining specialized codecs

Depends on 4.2 for the direct-color codecs.

- [ ] Change `IndexedCodec.ReadElement` and `DirectCodec.ReadElement` to read into the reused `_foreignBuffer`, and remove the `BitStream` they create but never use.
- [ ] Change each remaining specialized `EncodeElement` to write into a reused buffer instead of calling `BitStream.OpenWrite` for every element.
- [ ] Rewrite per-bit hot loops (seek and read for every pixel) to use direct span access, as in Phases 2–3.
- [ ] Extend `CodecElementBenchmarks` to cover a direct-color codec, and record before and after numbers here.

### 4.5 Package the contract suite for plugin authors

- [ ] Separate the 1.1 contract checks from `CodecFixture`. For example, make an abstract xunit base class that takes a codec factory and a list of sizes, either in the test project or in a small `ImageMagitek.Testing` package.
- [ ] Add resize-increment and arranger neighbor-isolation checks, so plugins are held to the buffer lifetime contract too.
- [ ] Run it against the sample plugins in `Samples/ImageMagitek.PluginSamples`. They need the plugin-loader constructor fix from `FeatureGaps.md` first.

### Deferred: bit access API

`BitStream` stays as it is for now. Once 4.4 is done, the specialized codecs no longer use it per bit, but it remains a general utility. A later change can add a public, span-based bit reader/writer (a `ref struct` over `Span<byte>`) and move callers to it. That would replace the internal `PackedBits` helper, give plugin authors the fast path, and also suit other bit-level work such as palette writing.

---

## Findings log

Record test-exposed bugs, equivalence disagreements and baseline benchmark numbers here as Phase 1 progresses.

| Date | Area | Finding | Status |
|---|---|---|---|
| 2026-10-04 | `ImageMagitek.csproj` packaging | Only 16 of the 21 codec XMLs are copied to output. `CotMFont.xml`, `FF5Font Pattern.xml`, `GBA4bpp Pattern.xml`, `SNES4bpp Pattern.xml` and `SNES3bpp Flow.xml` are missing, and a stale `_codecs\SNES3bpp.xml` entry points at a file that no longer exists, so the app ships without any pattern codec or SNES 3bpp Flow. The tests load codecs from the source `_codecs` folder, so they don't catch this. | Open |
| 2026-10-04 | `IndexedFlowGraphicsCodec.EncodeElement` | Row-interlaced encode uses `pos = y * el.Height` (line 180). Confirmed for SNES 2bpp/3bpp Flow/4bpp/8bpp and Game Gear 4bpp at 16x8 and 8x16. Fixed to `y * Width`, and decode and encode now both loop over the codec's `Width`/`Height` instead of a mix of `el.*` and `Format.*`. The skipped cases are folded into the normal contract, reversibility and golden theories, the 10 affected snapshots now pin encode output, and SNES 3bpp Flow at 16x8 and 8x16 matches the specialized `Snes3BppCodec` in both directions. | Fixed (Phase 2) |
| 2026-10-04 | `XmlGraphicsFormatReader` / `FlowGraphicsFormat.Clone` | Both pass `defaultWidth, defaultHeight` into a constructor declared `(defaultHeight, defaultWidth)`. The registered format has width and height swapped, and `Clone` swaps them back, so codecs from `CodecFactory` are correct (verified by `AllShippedXmlCodecsLoad` on FF5 Font 8x12 and Tokimemo 16x14). Anything reading the registered format directly sees them swapped. | Open |
| 2026-10-04 | `IndexedPatternGraphicsCodec` | `WidthResizeIncrement` is never assigned and is always 0. This is harmless today because pattern codecs report `CanResize == false`, but any caller that divides by it would fail. Now `1`, matching `HeightResizeIncrement`; `CanResize` and `GetPreferredWidth`/`Height` are unchanged. | Fixed (Phase 3) |
| 2026-10-04 | `Gen4bpp.xml`, `SNESMode7.xml`, `VB2bpp.xml`, `NGPC2bpp.xml` | `mergepriority` is ascending (`0, 1, ...`), so the first bit read (the MSB) lands in color bit 0 and every pixel's bits are reversed relative to the platform format. Round-trips pass because the reversal is symmetric. Example tile row `0..7`: Genesis encodes `08 4C 2A 6E` (expected `01 23 45 67`), Mode 7 encodes `00 80 40 C0 ...` (expected `00 01 02 03 ...`), VB encodes `D8 D8` (expected `E4 E4`), NGPC encodes `27 27` (expected `1B 1B`). The known-answer tests are skipped; the snapshots pin the current (reversed) output. | Open |
| 2026-10-04 | `DataSource` / `StreamRead/WriteExtensionMethods` | Elements at a non-byte-aligned `BitAddress` don't work. `ReadUnshifted`/`WriteUnshifted` don't shift data, and codecs' `ReadElement` buffers are `(StorageSize + 7) / 8` bytes, so the read throws `ArgumentException` (insufficient buffer length) for every codec whose `StorageSize` is a multiple of 8. NES 1bpp at 3x3 (9 bits) doesn't throw, but re-rendering returns the wrong indices. `ElementIsolationTests.SaveElement_NotByteAligned_ChangesOnlyElementBits` is skipped. Byte-aligned isolation passes for every codec, including the 9-bit case. | Open |
| 2026-10-04 | Cross-codec equivalence | All six pairs in 1.3 agree in both directions at every tested size, including SNES 3bpp Flow non-square decode and NES 1bpp at 3x3. | No action |
| 2026-10-04 | `MergePlanePriority` semantics (flow vs pattern) | The decode semantics agree. Both codecs put plane `p` into color bit `MergePlanePriority[p]`. The flow codec indexes its planes by the priority, and the pattern codec shifts by it, which comes to the same mapping. The old pattern encode applied `MergePlanePriority` and `RowPixelPattern` in the forward direction instead of inverting them, so it was only correct when both are their own inverse. Every shipped pattern XML qualifies, with identity or fully reversed priorities and a `0, 1` or `1, 0` row pattern, which is why the 1.3 equivalence tests passed. Phase 3 derives encode from the same per-bit table as decode, so encode is now the exact inverse for any permutation, and output for the shipped codecs is byte-identical (no snapshot changes). | Resolved (Phase 3) |
