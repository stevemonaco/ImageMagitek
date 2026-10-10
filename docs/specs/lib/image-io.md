---
id: LIB-IMAGE-IO
title: Image export and import
project: ImageMagitek
sources:
  - ImageMagitek/Image/IImageFileAdapter.cs
  - ImageMagitek/Image/ImageSharpFileAdapter.cs
  - ImageMagitek/Image/IndexedPngFile.cs
  - ImageMagitek/Image/CombinedPalette.cs
  - ImageMagitek/Image/DecodedImage.cs
  - ImageMagitek/Image/Import/ImageImporter.cs
  - ImageMagitek/Image/Import/ImageImportOptions.cs
  - ImageMagitek/Image/Import/ImageImportPreview.cs
  - ImageMagitek/Image/Import/ImportReport.cs
types:
  - IImageFileAdapter
  - ImageSharpFileAdapter
  - IndexedPngFile
  - CombinedPalette
  - DecodedImage
  - ImageImporter
  - ImageImportOptions
  - ImageImportPreview
  - ImportReport
  - ImportPixelState
  - ColorMatchEntry
  - UnmatchedColorEntry
tests:
  - IndexedPngFileTests
  - ImageImporterTests
  - ScatteredArrangerReversibilityTests
  - ReadOnlyArrangerTests
depends:
  - LIB-IMAGES
  - LIB-ARRANGERS
  - LIB-PALETTES
  - LIB-COLORS
---

# Image export and import

## Purpose

Exports an arranger's image to PNG and stages image files for import back into an arranger. Indexed arrangers export as paletted PNGs when their palettes fit, so palette indices survive a round trip through external editors; import matches colors to each element's palette (color matching itself is LIB-PALETTES/LIB-COLORS) and reports the outcome before anything is written. Writing goes through `SaveImage` (LIB-IMAGES).

## Requirements

### Indexed export

- **LIB-IMAGE-IO-001** — When an indexed image is exported, the adapter shall write a PNG the size of the whole arranger.
  - Tests: `ImageImporterTests.ExportThenImport_SinglePalette_RoundTripsIndicesThroughPng`
- **LIB-IMAGE-IO-002** — When every palette the arranger references fits in one combined palette of at most 256 entries and every pixel's index lies inside its palette's slot, export shall write an 8-bit paletted PNG holding each pixel's index shifted into its palette's slot.
  - Tests: `ImageImporterTests.ExportThenImport_MultiplePalettes_KeepsEveryPaletteEntryAndRoundTrips`
- **LIB-IMAGE-IO-003** — The combined palette shall place each referenced palette back to back in the order the palettes first appear in row-major element order, each slot holding min(palette entries, 2^codec depth) colors, using the largest depth among the codecs that share the palette.
  - Tests: `ImageImporterTests.ExportThenImport_MultiplePalettes_KeepsEveryPaletteEntryAndRoundTrips`
- **LIB-IMAGE-IO-004** — When a palette has `ZeroIndexTransparent`, its slot's first entry shall be written fully transparent through tRNS; other entries keep their alpha.
  - Tests: `ImageImporterTests.ExportThenImport_ZeroIndexTransparent_WritesTransparentIndexZero`
- **LIB-IMAGE-IO-005** — The paletted PNG shall keep duplicate palette colors as distinct entries, so their indices survive.
  - Tests: `IndexedPngFileTests.WriteThenRead_DuplicatePaletteColors_PreservesIndices`
- **LIB-IMAGE-IO-006** — Pixels of empty cells shall export as combined index 0.
  - Tests: untested
- **LIB-IMAGE-IO-007** — If the palettes exceed 256 combined entries, no element has a palette, or any pixel's index lies past its palette's slot, then export shall write a 32-bit RGBA PNG instead.
  - Tests: untested
- **LIB-IMAGE-IO-008** — In RGBA export, each pixel shall take its palette's color; index 0 of a `ZeroIndexTransparent` palette and pixels of empty cells shall be transparent black.
  - Tests: untested
- **LIB-IMAGE-IO-009** — When a direct image is exported, the adapter shall write a 32-bit RGBA PNG of the image's own size.
  - Tests: `ScatteredArrangerReversibilityTests.ImageToDataToImage_RoundTrips`
- **LIB-IMAGE-IO-010** — Export shall replace any existing file at the path.
  - Tests: untested

### Paletted PNG writer and reader

- **LIB-IMAGE-IO-011** — The writer shall write a non-interlaced 8-bit color-type-3 PNG whose PLTE holds the given palette, padded with opaque black up to the highest index used (at most 256 entries), and a tRNS chunk only through the last translucent entry.
  - Tests: `IndexedPngFileTests.Write_ImageSharpLoadsExpectedColors`
- **LIB-IMAGE-IO-012** — If the width or height is below 1, or there are fewer indices than pixels, then the writer shall throw an out-of-range error.
  - Tests: untested
- **LIB-IMAGE-IO-013** — The reader shall return the indices and palette of a non-interlaced color-type-3 PNG at bit depth 1, 2, 4 or 8, taking each entry's alpha from tRNS or 255.
  - Tests: `IndexedPngFileTests.WriteThenRead_DuplicatePaletteColors_PreservesIndices`, `IndexedPngFileTests.TryRead_FourBitImageSharpPng_ReadsIndices`
- **LIB-IMAGE-IO-014** — If the file is not such a PNG, lacks PLTE, uses an index past the palette, has an unknown row filter, or cannot be read or decompressed, then the reader shall return false instead of throwing.
  - Tests: `IndexedPngFileTests.TryRead_RgbaPng_ReturnsFalse`
- **LIB-IMAGE-IO-015** (inherited) — The reader shall not verify chunk CRCs and shall ignore chunks other than IHDR, PLTE, tRNS, IDAT and IEND.
  - Tests: untested

### Loading

- **LIB-IMAGE-IO-016** — When an image file is loaded, the adapter shall return its pixels as row-major RGBA in any format the image library reads.
  - Tests: `ScatteredArrangerReversibilityTests.ImageToDataToImage_RoundTrips`
- **LIB-IMAGE-IO-017** — When the file has a `.png` extension (any case) and is paletted, the loaded image shall also carry its indices and palette.
  - Tests: `ImageImporterTests.ExportThenImport_SinglePalette_RoundTripsIndicesThroughPng`
- **LIB-IMAGE-IO-018** — If the file cannot be read, is not accessible, or is not a known image format, then loading shall fail with a message naming the path.
  - Tests: untested

### Import staging

- **LIB-IMAGE-IO-019** — When an import is prepared from a path, the importer shall load the file through the adapter and fail with the adapter's reason when loading fails.
  - Tests: untested
- **LIB-IMAGE-IO-020** — If the arranger is read-only, then preparing an import shall fail with "Arranger '<name>' is read-only because it <reason>" (LIB-ARRANGERS-049).
  - Tests: `ReadOnlyArrangerTests.Prepare_ReadOnly_Fails`, `ReadOnlyArrangerTests.Prepare_ReadOnlySource_Fails`
- **LIB-IMAGE-IO-021** — The prepared import shall hold the current image and the resulting image of the whole arranger, the offset, the effective bounds and a report, and shall write nothing until committed.
  - Tests: `ImageImporterTests.Commit_WritesResultIntoArranger`
- **LIB-IMAGE-IO-022** — The source's top-left pixel shall land at the given offset, which may be negative; source pixels outside the arranger are cropped and arranger pixels the source does not cover stay unchanged.
  - Tests: `ImageImporterTests.Prepare_Offset_PlacesImageInArrangerCoordinates`, `ImageImporterTests.Prepare_SmallerImage_LeavesUncoveredPixelsUnchanged`, `ImageImporterTests.Prepare_LargerImage_IsCropped`
- **LIB-IMAGE-IO-023** — When bounds are given, the import shall change only arranger pixels inside them, and report the bounds clipped to the arranger; without bounds the whole arranger may change.
  - Tests: `ImageImporterTests.Prepare_Bounds_ClipsChanges`
- **LIB-IMAGE-IO-024** — Pixels of empty cells, or of elements whose codec kind differs from the arranger's, shall be skipped and reported Unchanged.
  - Tests: `ImageImporterTests.Prepare_UndefinedElement_IsSkipped`

### Indexed matching

- **LIB-IMAGE-IO-025** — When the source carries paletted indices and its palette matches the arranger's combined palette on RGB (as a prefix, ignoring alpha), a pixel whose index falls in its own element palette's slot and fits the codec depth shall take that index without color matching.
  - Tests: `ImageImporterTests.Prepare_SourceIndices_UsedWhenPaletteMatches_EvenWithDuplicateColors`, `ImageImporterTests.ExportThenImport_SinglePalette_RoundTripsIndicesThroughPng`
- **LIB-IMAGE-IO-026** — A source index from another palette's slot shall be color matched against the pixel's own palette.
  - Tests: `ImageImporterTests.Prepare_MultiplePalettes_IndexFromAnotherPalettesRange_IsColorMatched`
- **LIB-IMAGE-IO-027** — When the source palette does not match, the importer shall ignore the source indices and color match every pixel.
  - Tests: `ImageImporterTests.Prepare_SourceIndices_IgnoredWhenPaletteDiffers`
- **LIB-IMAGE-IO-028** — When transparent mapping is on, a pixel not taken from source indices whose alpha is at or below the threshold shall become index 0 without color matching.
  - Tests: `ImageImporterTests.Prepare_MapTransparentToIndexZero_SkipsColorMatching`
- **LIB-IMAGE-IO-029** — Color matching shall use each pixel's own element palette, limited to the entries the codec's color depth can store.
  - Tests: `ImageImporterTests.Prepare_MultiplePalettes_MatchesPerElementPalette`, `ImageImporterTests.Prepare_LimitsEntriesToCodecColorDepth`
- **LIB-IMAGE-IO-030** — When the pixel's current index already has exactly the source color, the importer shall keep the current index.
  - Tests: `ImageImporterTests.Prepare_DuplicatePaletteColors_KeepsCurrentIndexOnExactTie`
- **LIB-IMAGE-IO-031** — With the Exact strategy, a color shall match only an entry with identical RGBA; colors with no such entry are unmatched.
  - Tests: `ImageImporterTests.Prepare_Exact_ColorsInPalette_ProducesIndicesAndChangedCount`, `ImageImporterTests.Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports`
- **LIB-IMAGE-IO-032** — With a nearest strategy, a color without an exact entry shall take the nearest entry and be reported Substituted, unless it is farther than the maximum distance, when it is unmatched.
  - Tests: `ImageImporterTests.Prepare_Nearest_SubstitutesNearestEntryAndReports`, `ImageImporterTests.Prepare_Nearest_BeyondMaxDistance_IsUnmatched`
- **LIB-IMAGE-IO-033** — An unmatched pixel shall keep its current index and be reported Unmatched, not Changed.
  - Tests: `ImageImporterTests.Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports`
- **LIB-IMAGE-IO-034** — A pixel whose resulting index differs from its current index shall be reported Changed.
  - Tests: `ImageImporterTests.Prepare_Exact_ColorsInPalette_ProducesIndicesAndChangedCount`

### Direct import

- **LIB-IMAGE-IO-035** — When importing into a direct arranger, each covered pixel on a direct element shall take the source color, reported Changed when it differs; matching options are ignored and nothing is substituted or unmatched.
  - Tests: `ImageImporterTests.Prepare_Direct_CopiesPixelsAndCountsChanged`

### Report

- **LIB-IMAGE-IO-036** — The report shall hold a state per arranger pixel and the arranger's size, and count changed, substituted and unmatched pixels.
  - Tests: `ImageImporterTests.Prepare_SmallerImage_LeavesUncoveredPixelsUnchanged`
- **LIB-IMAGE-IO-037** — The report shall list substitutions per source color, palette and chosen index with distance, count and first location, farthest first and then most frequent.
  - Tests: `ImageImporterTests.Prepare_Nearest_SubstitutesNearestEntryAndReports`
- **LIB-IMAGE-IO-038** — The report shall list unmatched colors per source color and palette with count, first location and the nearest entry as a hint, most frequent first.
  - Tests: `ImageImporterTests.Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports`, `ImageImporterTests.Prepare_Bounds_ClipsChanges`
- **LIB-IMAGE-IO-039** — The import shall be committable only when no pixel is unmatched.
  - Tests: `ImageImporterTests.Prepare_Exact_UnmatchedColor_KeepsCurrentPixelAndReports`
- **LIB-IMAGE-IO-040** — The report shall say whether any pixel took its index from the source PNG.
  - Tests: `ImageImporterTests.Prepare_SourceIndices_UsedWhenPaletteMatches_EvenWithDuplicateColors`
- **LIB-IMAGE-IO-041** — The summary shall read "N of M pixels change", followed when present by "K color(s) substituted (P pixels)", "K color(s) unmatched (P pixels)" and "palette indices read from PNG", joined by " · ".
  - Tests: `ImageImporterTests.Prepare_Nearest_SubstitutesNearestEntryAndReports`, `ImageImporterTests.Prepare_SourceIndices_UsedWhenPaletteMatches_EvenWithDuplicateColors`

### Commit

- **LIB-IMAGE-IO-042** — When an import is committed, the result image shall be saved into the arranger's sources (LIB-IMAGES saving).
  - Tests: `ImageImporterTests.Commit_WritesResultIntoArranger`, `ImageImporterTests.Prepare_Direct_CopiesPixelsAndCountsChanged`
- **LIB-IMAGE-IO-043** — If commit is called while the import is not committable, then the preview shall still save, with unmatched pixels keeping their current index; callers check committability first.
  - Tests: untested

### Defaults

- **LIB-IMAGE-IO-044** (inherited) — When no options are given, import shall use the Exact strategy, no transparent mapping, an alpha threshold of 0 and no maximum distance.
  - Tests: untested

## Invariants

- An indexed export followed by an import with default options leaves every pixel's index unchanged, for single- and multi-palette arrangers that fit in 256 combined entries.
- Preparing an import never writes to a data source.
- Report dimensions always equal the arranger's pixel size, whatever the source size.

## Edge cases

- An indexed image over a sub-rectangle exports with the arranger's size and fails because its pixel buffer is too small.
- Import does not skip elements past the end of their source: their pixels are reported Changed but the commit never writes them.
- In RGBA fallback, a pixel of a `ZeroIndexTransparent` palette exports as transparent black, which the Exact strategy cannot match back to the opaque entry unless transparent mapping is on.
- In RGBA fallback, a pixel whose index lies past its palette's last entry (a 4bpp element on a shorter palette) throws `IndexOutOfRangeException` instead of exporting (backlog).
- A source palette shorter than the combined palette still matches, as a prefix.
- A loader exception other than I/O, access or format errors propagates to the caller.

## Threading and lifetime

- Export and import run synchronously on the caller's thread and hold the file open only while reading or writing.
- A prepared import holds its own current and result images; it does not observe later changes to the arranger, so committing a stale preview overwrites them.

## Decisions

- **A built-in paletted PNG writer and reader.** ImageSharp cannot preserve indices when a palette has duplicate colors, nor expose indices on load, so the library writes and reads color-type-3 PNGs itself; tests cross-check against ImageSharp's encoder and decoder. Anything the reader does not handle falls back to ImageSharp RGBA.
- **Multi-palette arrangers export paletted.** Each referenced palette gets its own slot in a combined palette, so every color of every palette survives. Rejected: paletted export only for single-palette arrangers (the original plan).
- **PLTE holds the full codec color range.** Each slot holds the palette up to the codec's color count, not only the indices used, so external editors can paint with every color.
- **Index 0 is transparent when the palette says so.** tRNS marks index 0 transparent for `ZeroIndexTransparent` palettes, matching the editor; element-less cells export as index 0, so they are transparent only in that case.
- **Palette match ignores alpha.** External editors often drop tRNS, so the source palette is compared to the combined palette on RGB only.
- **A pixel's own slot is authoritative.** A PNG index inside the pixel's own palette slot is used as is; an index from another palette's slot was painted with a color the element cannot use, so it is color matched instead.
- **Keep the current index on an exact tie.** Duplicate palette colors make an exported pixel ambiguous, so a pixel whose current index already has the source color keeps it, avoiding spurious changes.
- **Exact mode still reports a nearest candidate.** Unmatched colors carry the perceptually nearest entry so callers can suggest a fix.
- **Partial and offset import.** Images need not match the arranger size: the source is placed at an offset, cropped, and optionally clipped to bounds (import into a selection), with the report in arranger coordinates. Rejected: requiring an exact-size image (the earlier behavior).
- **Commit is gated by the caller.** The library exposes committability and the UI and CLI refuse to commit while any color is unmatched; `Commit` itself does not check.

## Non-goals

- Export formats other than PNG (BMP, GIF, palette sidecars).
- Exporting a sub-rectangle or a sequential view (the arranger-size export assumes a whole-arranger image).
- Adding colors to a palette during import.

## Open items

- `ImageImportPreview.Commit` does not check `CanCommit` (LIB-IMAGE-IO-043); the UI and CLI do.
- Import reports past-end-of-source pixels as Changed though they are never written.
- Indexed export of a sub-rectangle image fails instead of exporting the rectangle; exporting sequential views and selections is an open roadmap item.
- RGBA fallback export and `CombinedPalette` slot limits have no direct tests (LIB-IMAGE-IO-006, -007, -008).
- RGBA fallback export throws for an index past its palette's last entry; LIB-IMAGE-IO-008 does not say what such a pixel exports as.
