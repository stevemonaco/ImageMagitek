---
id: LIB-SCENES
title: Layer compositing and scenes
status: draft
project: ImageMagitek
sources:
  - ImageMagitek/Scenes
types:
  - LayerCompositor
  - CompositorLayer
  - LayerImage
tests:
  - LayerCompositorTests
depends:
  - LIB-ARRANGERS
  - LIB-IMAGES
  - LIB-PALETTES
---

# Layer compositing and scenes

## Purpose

Draws several rendered layers back to front into one 32-bit image, the way consoles stack backgrounds. `LayerImage` renders an arranger, or any arranger view (`IArrangerView`, LIB-ARRANGERS-053) such as a metatile map layer, to 32-bit color, and `LayerCompositor` stacks layer images over a backdrop. Metatile maps (LIB-METATILE-MAPS) composite their own metatile layers with it. The scene resource of the [layers proposal](../../changes/layers.md) builds on it later; this draft specifies only the compositing core, and the scene requirements are added with that proposal's Task 2.

## Requirements

### Layer images

- **LIB-SCENES-001** — Rendering an arranger to a layer image shall give an image of the arranger's pixel size in which each pixel of an indexed element is its element's palette color for the pixel's index, including that color's alpha, so a palette entry marked transparent (LIB-PALETTES `zeroindextransparent`) is fully transparent. When asked to (`forceZeroTransparent`), index 0 shall be fully transparent whatever the palette's flag says.
  - Tests: `LayerCompositorTests.LayerImage_IndexedArranger_UsesElementPalettes_ZeroTransparent`, `LayerCompositorTests.LayerImage_ForceZeroTransparent_IgnoresPaletteFlag`
- **LIB-SCENES-002** — A pixel of a direct-color element shall keep its decoded color and alpha, and a pixel in an empty cell (no element) shall be fully transparent.
  - Tests: `LayerCompositorTests.LayerImage_DirectArranger_KeepsAlpha`, `LayerCompositorTests.LayerImage_EmptyCell_IsTransparent`
- **LIB-SCENES-004** — A pixel of an indexed element that does not lie wholly within its source, or whose index is at or past its palette's entry count, shall be fully transparent, as the graphics editor shows it.
  - Tests: `LayerCompositorTests.LayerImage_OutsideSourceOrPastPalette_IsTransparent`
- **LIB-SCENES-003** — Rendering a layer image shall read the arranger's current data each time, so it reflects data written since the last render.
  - Tests: `LayerCompositorTests.LayerImage_RereadsAfterWrite`

### Compositing

- **LIB-SCENES-010** — Compositing shall produce an image of the given canvas size by drawing each visible layer, in list order from back to front, at its pixel position on the canvas. A canvas of zero or negative width or height shall be refused with `ArgumentOutOfRangeException`.
  - Tests: `LayerCompositorTests.Composite_DrawsLayersBackToFront`, `LayerCompositorTests.Composite_ZeroCanvas_Throws`
- **LIB-SCENES-011** — A fully transparent layer pixel shall leave the pixel below it unchanged, and a fully opaque one shall replace it. A partly transparent pixel shall be blended over the pixel below by its alpha (source-over).
  - Tests: `LayerCompositorTests.Composite_TransparentPixelShowsLayerBelow`, `LayerCompositorTests.Composite_PartialAlpha_BlendsSourceOver`
- **LIB-SCENES-012** — A hidden layer shall not be drawn.
  - Tests: `LayerCompositorTests.Composite_HiddenLayerSkipped`
- **LIB-SCENES-013** — A layer that extends past the canvas, including at a negative position, shall be clipped to it, and a layer wholly outside the canvas shall draw nothing.
  - Tests: `LayerCompositorTests.Composite_LayerPartlyOffCanvas_IsClipped`
- **LIB-SCENES-014** — The canvas shall start filled with the backdrop color, or fully transparent when there is no backdrop, so the backdrop shows only where every layer is transparent.
  - Tests: `LayerCompositorTests.Composite_BackdropShowsOnlyThroughTransparentPixels`, `LayerCompositorTests.Composite_NoBackdrop_IsTransparent`
- **LIB-SCENES-015** — Layers of different sizes, element sizes, codecs, bit depths and color types shall composite together, since compositing works on layer images only.
  - Tests: `LayerCompositorTests.Composite_8bppUnder4bpp`
- **LIB-SCENES-016** — Compositing with no layers shall give the backdrop alone.
  - Tests: `LayerCompositorTests.Composite_NoLayers_IsBackdrop`

## Invariants

- Compositing never changes a layer image, an arranger or its data.
- The composite's size is the canvas size, whatever the layers' sizes and positions.

## Edge cases

- A canvas of zero width or height is refused when the compositor is asked for it (`ArgumentOutOfRangeException`, LIB-SCENES-010).
- A layer with an empty arranger (no elements) composites as fully transparent (`LayerCompositorTests.LayerImage_EmptyCell_IsTransparent`). An arranger of zero pixel size renders as a 0×0 layer image.
- Source-over uses straight (not premultiplied) alpha, rounded to the nearest integer: out alpha = sa + da × (255 − sa) / 255, and each channel = (sc × sa × 255 + dc × da × (255 − sa)) / (out alpha × 255).

## Threading and lifetime

- `LayerImage` and `LayerCompositor` hold no state between calls, so the caller decides when to re-render; they are safe to call from any one thread at a time.

## Decisions

- **Forced color 0 transparency is an option of the layer image.** `forceZeroTransparent` serves metatile maps, whose upper layers treat color 0 as transparent regardless of the palette flag (LIB-METATILE-MAPS "Upper layers treat color 0 as transparent regardless of the palette flag"). Reason: the rule belongs to the caller that knows the layer order, and the compositor stays unaware of hardware rules.
- **Composite rendered colors, not indexes.** Layers are rendered to 32-bit color first and stacked after palette lookup, as the hardware does. Reason: layers with different palettes and bit depths share no index space. Consequence: a composite PNG is direct color. Rejected: compositing indexes into a shared palette.
- **The compositor is separate from the scene resource.** It takes layer images with a position and a visible flag, and knows nothing about project resources. Reason: metatile maps composite their own layers and must not need a scene, and compositing is testable without a project. Rejected: compositing inside the scene editor.
- **Layer images are plain 32-bit buffers, not `DirectImage`.** `LayerImage` holds a width, a height and a row-major `ColorRgba32[]`, and the composite is a `LayerImage` too. Reason: `DirectImage` needs a direct-color arranger and its `SaveImage` writes back to that arranger, which a composite must not do; a buffer passes straight to `IImageFileAdapter.SaveImage(ColorRgba32[], width, height, path)` for PNG export; and a composite can be fed back in as a layer, which a metatile map inside a scene needs. This differs from the layers proposal's Task 1 wording ("composite rendering into a `DirectImage`"). Rejected: a `DirectImage` over a synthetic arranger (an arranger with no data behind it); returning `ColorRgba32[,]` (the export path and `DirectImage` use flat row-major arrays).
- **A static compositor.** `LayerCompositor.Composite` is a static method taking the canvas size, the layers and the backdrop. Reason: it holds no state between calls (Threading and lifetime), so callers own re-rendering and caching. Rejected: a compositor instance holding its layers (a second copy of the scene's or metatile map's layer list).
- **Source-over for partial alpha.** Fully transparent and fully opaque pixels cover the console cases; direct-color codecs with alpha (PSX semi-transparency flags, PNG-derived data) need a defined blend, and source-over is the common one. Rejected: treating any alpha below 255 as transparent (loses direct-color alpha).

## Non-goals

- Hardware blending, color math, windows, mosaic and scroll registers (see the layers proposal Decisions).
- Priority splitting of tilemap layers; a later phase of the layers proposal.

## Open items

- The scene resource (`Scene`, `SceneLayer`), its backdrop from a palette entry, active-layer drawing and priority split are specified with the layers proposal's later tasks.
