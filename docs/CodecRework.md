# Codec Rework Plan

Goal: make the generalized codecs (`IndexedFlowGraphicsCodec`, `IndexedPatternGraphicsCodec`) faster and leaner without changing a single encoded byte or decoded pixel. Codec round-trip regressions have been one of the hardest problems in this project, so no codec code changes until Phase 1 is in place and green.

**Scope:** XML-defined flow and pattern codecs, plus the shared encode/decode pipeline (`IndexedImage`, `ImageImporter`, arrangers, data sources). Specialized C# codecs are used here only as reference implementations.

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
- [x] **Golden snapshots:** `CodecGoldenTests` encodes a gradient image and a seeded random image with each codec at each size from 1.1, and writes the bytes as hex into a [Verify](https://github.com/VerifyTests/Verify) snapshot at `ImageMagitek.UnitTests/CodecTests/Snapshots/<codec>_<w>x<h>.verified.txt`. On a mismatch Verify writes a `.received.txt` next to it. To accept an intentional change, review the diff and rename the `.received.txt` file to `.verified.txt` (or use a Verify diff tool). Review snapshot changes in the diff like any other code change. Row-interlaced non-square cases record decode only, so the snapshots don't pin the encode bug.
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
- [ ] **Sample projects:** optionally, use `_xmlprojectsamples/*.zip` as end-to-end fixtures (already listed in `FeatureGaps.md`).

### 1.5 Benchmark baseline

- [ ] Add encode benchmarks next to `Snes3BppDecodeToImage`, plus decode and encode benchmarks for a pattern codec and for a large single-layout flow codec (PSX 64x64).
- [ ] Record baseline numbers (time and allocated bytes) in this document before starting Phase 2.

---

## Phase 2: `IndexedFlowGraphicsCodec` rework

Entry criteria: Phase 1 is green, and the baseline benchmarks are recorded.

### Known bugs to fix first (each in its own commit, with golden updates if output changes)

- [ ] `EncodeElement` row-interlaced path uses `pos = y * el.Height` (line 180) where decode uses `y * el.Width`. Non-square row-interlaced elements encode to the wrong positions. 1.1 should catch this; un-skip the test when fixed.
- [ ] Decode loops over `el.Width`/`el.Height`, while encode loops over `Format.Width`/`Format.Height` and the buffers are sized from the codec. Standardize on the codec's `Width`/`Height`.

### Decode

- [ ] Read bits directly from `encodedBuffer` with a static helper. This removes the `_foreignBuffer` copy and the per-bit `IBitStreamReader.ReadBit()` interface call with its access and bounds checks. The existing `StorageSize` guard already covers the length.
- [ ] OR each bit straight into the native buffer (`native[pos] |= bit << mergePlane`) after one clear. This removes `_elementData`, `_mergedData` and two full passes over the pixels.
- [ ] Index `_nativeBuffer` as a flat span via `MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(...), Length)`. This keeps the current `y * Width + column` indexing exactly.

### Encode

- [ ] Compute each bit directly from `imageBuffer` as `(pixel >> mergePlane) & 1`, and pack the bits into a reused instance buffer. This removes the copy into `_mergedData`, the split into `_elementData`, and the per-call `BitStream` and `byte[]` allocations.
- [ ] Returning a reused buffer is safe with today's callers (`IndexedImage.cs`, `ArrangerSaveConflictExtensions.cs`), which consume it immediately or call `.ToArray()`. Document this on `IIndexedCodec.EncodeElement`: the returned span is valid until the next call on the same codec instance.

### Shared

- [ ] Precompute `RowPixelPattern` into an `int[]` of column offsets per `ImageProperty` when buffers are allocated. `RepeatList`'s indexer costs a modulo, a division and a bounds check on every bit.
- [ ] Take `MergePlanePriority` lookups out of the inner loops (`AsSpan(plane, ip.ColorDepth)` per image property).
- [ ] Have `ReadElement` read into a reused instance buffer instead of allocating a new `byte[]` on every call. Callers decode the result immediately.
- [ ] Seal the class, and change its `virtual` members and `protected` fields to non-virtual and private, since nothing inherits from it.
- [ ] Note: `stackalloc` isn't needed. Once the intermediate stages are gone, no scratch buffers are left.

Exit criteria: Phase 1 suites pass with no golden changes, apart from the bug fixes above. The benchmarks show the improvement, and allocations per decode and encode are zero.

---

## Phase 3: `IndexedPatternGraphicsCodec` rework

Same approach as Phase 2, applied to the pattern codec's hot paths:

- [ ] Encode calls `bs.SeekAbsolute(index)` followed by `WriteBit` for every bit, and builds a `PlaneCoordinate` and calls `GetEncodeIndex` each time. Precompute the bit-index ↔ (plane, x, y) mapping once per codec, and write bits directly into a reused buffer.
- [ ] Decode reads through `IBitStreamReader` into a `List<int[,]>` of plane images, then merges. Read bits directly from the span and OR them into the native buffer, as in Phase 2.
- [ ] Encode allocates a new `BitStream` and `byte[]` per call, and `ReadElement` allocates per call. Fix both the same way as in Phase 2.
- [ ] Check the `MergePlanePriority` semantics against the flow codec. The pattern codec shifts by `MergePlanePriority[i]`, while the flow codec indexes planes by it. 1.3's equivalence tests should confirm whether both are correct for their XML definitions.

---

## Phase 4: Follow-ups (optional)

- [ ] Write the encode/decode buffer ownership contract into the `IIndexedCodec`/`IGraphicsCodec` XML docs, and apply it to the specialized codecs.
- [ ] Package the Phase 1.1 contract suite in a form plugin authors can run against their own codecs.
- [ ] Revisit `BitStream`. With the generalized codecs no longer using it per bit, decide whether it stays a general utility or shrinks to what the remaining callers need.

---

## Findings log

Record test-exposed bugs, equivalence disagreements and baseline benchmark numbers here as Phase 1 progresses.

| Date | Area | Finding | Status |
|---|---|---|---|
| 2026-10-04 | `ImageMagitek.csproj` packaging | Only 16 of the 21 codec XMLs are copied to output. `CotMFont.xml`, `FF5Font Pattern.xml`, `GBA4bpp Pattern.xml`, `SNES4bpp Pattern.xml` and `SNES3bpp Flow.xml` are missing, and a stale `_codecs\SNES3bpp.xml` entry points at a file that no longer exists, so the app ships without any pattern codec or SNES 3bpp Flow. The tests load codecs from the source `_codecs` folder, so they don't catch this. | Open |
| 2026-10-04 | `IndexedFlowGraphicsCodec.EncodeElement` | Row-interlaced encode uses `pos = y * el.Height` (line 180). Confirmed for SNES 2bpp/3bpp Flow/4bpp/8bpp and Game Gear 4bpp at 16x8 and 8x16; the round-trip tests for those cases are skipped with the bug named. | Open |
| 2026-10-04 | `XmlGraphicsFormatReader` / `FlowGraphicsFormat.Clone` | Both pass `defaultWidth, defaultHeight` into a constructor declared `(defaultHeight, defaultWidth)`. The registered format has width and height swapped, and `Clone` swaps them back, so codecs from `CodecFactory` are correct (verified by `AllShippedXmlCodecsLoad` on FF5 Font 8x12 and Tokimemo 16x14). Anything reading the registered format directly sees them swapped. | Open |
| 2026-10-04 | `IndexedPatternGraphicsCodec` | `WidthResizeIncrement` is never assigned and is always 0. This is harmless today because pattern codecs report `CanResize == false`, but any caller that divides by it would fail. | Open |
| 2026-10-04 | `Gen4bpp.xml`, `SNESMode7.xml`, `VB2bpp.xml`, `NGPC2bpp.xml` | `mergepriority` is ascending (`0, 1, ...`), so the first bit read (the MSB) lands in color bit 0 and every pixel's bits are reversed relative to the platform format. Round-trips pass because the reversal is symmetric. Example tile row `0..7`: Genesis encodes `08 4C 2A 6E` (expected `01 23 45 67`), Mode 7 encodes `00 80 40 C0 ...` (expected `00 01 02 03 ...`), VB encodes `D8 D8` (expected `E4 E4`), NGPC encodes `27 27` (expected `1B 1B`). The known-answer tests are skipped; the snapshots pin the current (reversed) output. | Open |
| 2026-10-04 | `DataSource` / `StreamRead/WriteExtensionMethods` | Elements at a non-byte-aligned `BitAddress` don't work. `ReadUnshifted`/`WriteUnshifted` don't shift data, and codecs' `ReadElement` buffers are `(StorageSize + 7) / 8` bytes, so the read throws `ArgumentException` (insufficient buffer length) for every codec whose `StorageSize` is a multiple of 8. NES 1bpp at 3x3 (9 bits) doesn't throw, but re-rendering returns the wrong indices. `ElementIsolationTests.SaveElement_NotByteAligned_ChangesOnlyElementBits` is skipped. Byte-aligned isolation passes for every codec, including the 9-bit case. | Open |
| 2026-10-04 | Cross-codec equivalence | All six pairs in 1.3 agree in both directions at every tested size, including SNES 3bpp Flow non-square decode and NES 1bpp at 3x3. | No action |
