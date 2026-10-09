---
id: LIB-IMAGES
title: Indexed and direct images
project: ImageMagitek
sources:
  - ImageMagitek/Image/ImageBase.cs
  - ImageMagitek/Image/IndexedImage.cs
  - ImageMagitek/Image/DirectImage.cs
  - ImageMagitek/Image/Extensions/IndexedImageExtensions.cs
  - ImageMagitek/Image/Extensions/DirectImageExtensions.cs
  - ImageMagitek/Image/ImageCopier.cs
  - ImageMagitek/Image/ImageColorAdapter.cs
  - ImageMagitek/Arranger/ArrangerCopy.cs
  - ImageMagitek/ExtensionMethods/RectangularArrayExtensions.cs
types:
  - ImageBase
  - IndexedImage
  - DirectImage
  - IndexedImageExtensions
  - DirectImageExtensions
  - ImageCopier
  - PixelRemapOperation
  - IndexedPixelCopy
  - DirectPixelCopy
  - ImageColorAdapter
  - RectangularArrayExtensions
tests:
  - IndexedImageTests
  - DirectArrangerRoundTripTests
  - ElementIsolationTests
  - ScatteredArrangerReversibilityTests
  - SequentialArrangerRoundTripTests
  - ReadOnlyArrangerTests
  - GraphicsEditHistoryTests
  - ImageCopierTests
  - MirrorArray2DTests
  - RotateArray2DTests
  - TransposeArray2DTests
depends:
  - LIB-ARRANGERS
  - LIB-CODECS
  - LIB-PALETTES
  - LIB-DATASOURCE
---

# Indexed and direct images

## Purpose

An image is an editable pixel buffer over an arranger, or a rectangle of one: palette indices for indexed arrangers, RGBA colors for direct ones. It decodes elements through their codecs (LIB-CODECS), applies element mirror and rotation, and saves by encoding back into the data sources (LIB-DATASOURCE). Pixel edits, flood fill, color remap, palette assignment and pixel copy/paste live here; file export and import are LIB-IMAGE-IO.

## Requirements

### Creation and rendering

- **LIB-IMAGES-001** — When an image is created over an arranger, whole or by a pixel rectangle, it shall decode immediately and hold one pixel per covered arranger pixel, row-major.
  - Tests: `IndexedImageTests.PartialUnalignedEdit_LeavesOtherPixelsAndBytesUnchanged`
- **LIB-IMAGES-002** — If the arranger is null or its color type does not match the image kind, then creation shall fail.
  - Tests: untested
- **LIB-IMAGES-003** — If the image width or height is not positive, then rendering shall fail.
  - Tests: untested
- **LIB-IMAGES-004** — When rendering, the image shall clear every pixel to 0 (index 0, or transparent black) and then draw each element of the matching kind that overlaps the image, clipped to the image.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`
- **LIB-IMAGES-005** — When an element's encoded data extends past the end of its source, rendering shall leave the whole element blank instead of failing or showing partial data.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`, `IndexedImageTests.DirectArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`
- **LIB-IMAGES-006** — Rendering shall apply an element's rotation and then its mirror to the decoded pixels; rotate left is counter-clockwise.
  - Tests: `IndexedImageTests.MirrorAndRotation_RoundTrip`, `RotateArray2DTests.RotateArray2D_Left_AsExpected`, `MirrorArray2DTests.MirrorArray2D_HorizontalMirroring_AsExpected`
- **LIB-IMAGES-007** — Empty cells and elements whose codec kind differs from the image kind shall render as blank.
  - Tests: untested

### Pixel access

- **LIB-IMAGES-008** — If a pixel or row outside the image is read or written, then the image shall throw an out-of-range error.
  - Tests: untested
- **LIB-IMAGES-009** — Element lookups by image pixel shall offset by the image's left and top edge into arranger coordinates.
  - Tests: untested
- **LIB-IMAGES-010** — When an indexed pixel is set by color, the image shall store the first palette index whose color equals it exactly, and fail when the pixel's element has no indexed codec or the palette lacks the color.
  - Tests: untested
- **LIB-IMAGES-011** — The can-set check for an indexed pixel color shall fail when the pixel is outside the image, its cell is empty, its codec is not indexed, it has no palette, the palette lacks the color, or the color's index is beyond the codec's color depth.
  - Tests: untested
- **LIB-IMAGES-012** — When an indexed pixel's color is read, the image shall return its element palette's color for the stored index, and fail when the cell has no indexed codec.
  - Tests: untested
- **LIB-IMAGES-032** — When an indexed pixel is painted with an index of a source palette, the image shall store that index if the pixel's element uses that palette and the index fits the codec's color depth; otherwise it shall store the first index of the element's palette whose color equals the source palette's color at that index, and fail as LIB-IMAGES-010 does. It reports the index it stored.
  - Tests: `IndexedImageTests.PaintIndex_DuplicateColor_WritesChosenIndex`, `IndexedImageTests.PaintIndex_OtherPalette_WritesExactColorIndex`

### Saving

- **LIB-IMAGES-013** — If the arranger is read-only, then saving shall fail before writing anything.
  - Tests: `ReadOnlyArrangerTests.SaveImage_ReadOnly_ThrowsAndLeavesSourceUnchanged`, `ReadOnlyArrangerTests.SaveImage_ReadOnlySource_ThrowsBeforeWriting`
- **LIB-IMAGES-014** — When an indexed image covering part of the arranger is saved, the save shall merge it into the arranger's current pixels and re-encode the whole arranger, leaving pixels and bytes outside the rectangle unchanged.
  - Tests: `IndexedImageTests.PartialUnalignedEdit_LeavesOtherPixelsAndBytesUnchanged`
- **LIB-IMAGES-015** — When saving, the image shall encode every element of its kind that lies within its source, undoing mirror and then rotation first, and write only that element's bits, at byte-aligned and unaligned addresses.
  - Tests: `IndexedImageTests.MirrorAndRotation_RoundTrip`, `ElementIsolationTests.SaveElement_ByteAligned_ChangesOnlyElementBits`, `ElementIsolationTests.SaveElement_NotByteAligned_ChangesOnlyElementBits`, `DirectArrangerRoundTripTests.SaveElement_ByteAligned_ChangesOnlyElementBits`
- **LIB-IMAGES-016** — When saving, the image shall skip elements past the end of their source, so a source never grows and its trailing bytes are unchanged.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`, `IndexedImageTests.DirectArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`
- **LIB-IMAGES-017** — After writing, the save shall flush each distinct data source the arranger's elements reference and raise its data-written notification once.
  - Tests: untested
- **LIB-IMAGES-018** — Decoding then saving an unmodified image shall leave the source bytes unchanged, and saving pixels then decoding shall return them, for every shipped codec.
  - Tests: `ScatteredArrangerReversibilityTests.DataToImageToData_PreservesRom`, `ScatteredArrangerReversibilityTests.ImageToDataToImage_RoundTrips`, `DirectArrangerRoundTripTests.DataToImageToData_PreservesRom`, `DirectArrangerRoundTripTests.ImageToDataToImage_RoundTrips`, `SequentialArrangerRoundTripTests.RoundTrips_AtNonzeroOffset_AndAfterCodecResize`, `IndexedImageTests.FileDataSource_RoundTrip`

### Palette assignment

- **LIB-IMAGES-019** — The can-set-palette check shall succeed when the pixel's element already uses that palette or every index in the element is below the palette's entry count, and fail outside the arranger, on an empty cell, or on a non-indexed codec.
  - Tests: untested
- **LIB-IMAGES-020** — When a palette is assigned at a pixel, the image shall give that element a clone of its codec carrying the palette, leaving every other element and arranger that shared the old codec unchanged.
  - Tests: `IndexedImageTests.TrySetPalette_ClonedArranger_LeavesOriginalPalettes`, `IndexedImageTests.TrySetPalette_CodecSharedByTwoCells_ChangesOnlyTarget`, `IndexedImageTests.TrySetPalette_ElementsCopiedFromSequential_LeavesSequentialPalette`, `GraphicsEditHistoryTests.ApplyPalette_Undo_DoesNotModifySharedCodec`

### Flood fill

- **LIB-IMAGES-021** — When an indexed image is flood filled from a pixel, every 4-connected pixel with the start index, in elements within their source that use the start element's palette, shall take the fill index; the fill reports whether anything changed.
  - Tests: `GraphicsEditHistoryTests.FloodFill_IndexedWithClip_UndoRedo`
- **LIB-IMAGES-022** — If the fill index equals the start index or the start element has no palette, then the indexed fill shall change nothing and return false.
  - Tests: untested
- **LIB-IMAGES-023** — When a direct image is flood filled, every 4-connected pixel with the start color, in direct elements within their source, shall take the fill color; a fill color equal to the start color changes nothing.
  - Tests: `GraphicsEditHistoryTests.FloodFill_DirectWithClip_UndoRedo`
- **LIB-IMAGES-024** — When clip bounds are given, the fill shall not change pixels outside them.
  - Tests: `GraphicsEditHistoryTests.FloodFill_IndexedWithClip_UndoRedo`, `GraphicsEditHistoryTests.FloodFill_DirectWithClip_UndoRedo`

### Color remap

- **LIB-IMAGES-025** — When an indexed image's colors are remapped, each pixel within the optional bounds (clipped to the image) shall take the remap entry for its current index.
  - Tests: `GraphicsEditHistoryTests.ColorRemap_WithBounds_UndoRedo`

### Pixel copy and paste

- **LIB-IMAGES-026** — When pixels are copied from an arranger, the copy shall render that pixel rectangle into a new image of the arranger's kind; copying indexed pixels from a direct arranger, or direct pixels from an indexed one, shall fail.
  - Tests: `GraphicsEditHistoryTests.PixelPaste_UndoRedo`
- **LIB-IMAGES-027** — If a pixel paste overruns the source or the destination, or either region touches an empty cell, then the paste shall fail without change.
  - Tests: untested
- **LIB-IMAGES-028** — When pasting indexed into indexed, the copier shall try the requested remap operations in order and apply the first whose check passes: exact index passes only when every source index is below `1 << ` the destination element's codec color depth, and copies indices; exact palette colors writes each source color's exact index in the destination palette.
  - Tests: `GraphicsEditHistoryTests.PixelPaste_UndoRedo`, `ImageCopierTests.CopyPixels_ExactIndex_IndexAboveDestDepth_Fails`, `ImageCopierTests.CopyPixels_ExactIndexThenColors_IndexAboveDestDepth_UsesColors`, `ImageCopierTests.CopyPixels_ExactIndex_DestHoldsHighIndex_Succeeds`
- **LIB-IMAGES-029** — When pasting direct into indexed, both exact operations shall write the exact palette index of each source color and fail if any color is missing from the destination palette.
  - Tests: untested
- **LIB-IMAGES-030** — When pasting indexed into direct, the copier shall write each source pixel's palette color; direct into direct copies colors.
  - Tests: untested
- **LIB-IMAGES-031** — If no requested remap operation succeeds, then the paste shall fail with "no suitable copy method"; `RemapByAnyIndex` never succeeds.
  - Tests: untested

## Invariants

- `Image.Length == Width × Height` after every render.
- Saving never changes a source's length.
- Bits outside the elements being written are never changed by a save.

## Edge cases

- Creating an image with a rectangle outside the arranger throws an index error during the first render; there is no bounds check.
- `DirectImage.SaveImage` and the direct save-conflict analysis read the image as if it started at the arranger origin, so a direct image over a sub-rectangle saves the wrong pixels or throws.
- A save writes every element, including ones the edit did not touch; overlapping elements are resolved by write order (see LIB-ARRANGERS save conflicts).
- The can-set-palette check reads the element's pixels at arranger coordinates through the image's own pixel accessor, which is wrong for an image over a sub-rectangle.
- A flood fill whose clip bounds extend past the image throws when it reaches the image edge.
- Remap throws when a pixel's index has no remap entry.
- `CopyPixelsDirect` on an indexed arranger throws `NotImplementedException`, unlike the indexed variant's argument error.
- Indexed flood fill does not check that the fill index fits the codec's color depth.

## Threading and lifetime

- Images are not thread-safe. Rendering and saving use each codec's shared read and write buffers, so two images over arrangers that share a codec instance must not render or save concurrently.
- An image holds its arranger; it does not observe the arranger or its sources. Callers re-render after the arranger or data changes, typically on the source's data-written notification.

## Decisions

- **Reads past end of file render empty.** An arranger near or over the end of a truncated file shows the missing elements as blank, and a save skips them so the file never grows. Rejected: throwing, or showing whatever bytes are available.
- **Read-only arrangers refuse to save.** A codec that cannot encode or a read-only data source makes the arranger read-only, checked once at the arranger and enforced in `SaveImage` (and import, LIB-IMAGE-IO), so failures happen before any byte is written rather than mid-save.
- **A partial edit re-encodes the whole arranger.** Image rectangles need not be element-aligned, so the edit is merged into a full render and every element is re-encoded, leaving pixels outside the rectangle as they were.
- **Flood fill stays within one palette.** An indexed fill does not cross into elements with a different palette, because the same index would mean a different color there.

## Non-goals

- Converting between indexed and direct images (`ImageColorAdapter` is a stub whose members all throw).
- Write-ahead-log protection for pixel writes; saves write sources in place.
- Undo history; see the UI history spec.

## Open items

- `ImageColorAdapter` (every member throws) and `PixelRemapOperation.RemapByAnyIndex` (branches commented out) are dead code (backlog).
- `ImageBase.Right` and `Bottom` are documented as inclusive but are exclusive (`Left + Width`).
