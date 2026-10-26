# Layered scenes

**Status:** Task 1 (the compositor) is implemented in `ImageMagitek/Scenes` against draft [LIB-SCENES](../specs/lib/scenes.md), compositing into a 32-bit `LayerImage` buffer rather than a `DirectImage` (see LIB-SCENES Decisions). Tasks 2–7 are not started.

## Why

Consoles build a screen from several layers: two to four backgrounds plus sprites, drawn back to front, with color 0 of each tile transparent. TileShop shows one arranger at a time (layers are an ARCHITECTURE §1 non-goal; backlog P3 under UI-DRAWING). The FireRed test project shows the gap: its title screen is now four [tilemaps](../specs/lib/tilemaps.md) (an 8bpp logo, Charizard, the copyright line and the border), each correct but none of them looking like the screen. Its trainer cards, battle screens and minigames have the same problem, and tilemaps keep each entry's priority bit but cannot show it because there is only one layer.

[Metatile maps](../specs/lib/metatile-maps.md) need the same compositing inside a single resource: a FireRed map block has a bottom and a top layer, and the map is unreadable with one of them. So the compositing core is needed before scenes are, and is built first.

## What

A **scene** is a new project resource that stacks existing arrangers:

- A canvas size in pixels and a **backdrop**: a palette entry (SNES and GBA draw color 0 of palette 0 behind everything) or a fixed color, or none (transparent).
- An ordered list of **layers**, back to front. Each names an arranger (a scattered arranger, a tilemap, or a metatile map, which brings its own composited layers), a pixel position on the canvas, and a visible flag.

Each layer renders exactly as its arranger does today, transparency included (palettes with `zeroindextransparent`, a direct codec's alpha). The scene composites the layer images over the backdrop in order. Layers can mix element sizes, codecs, bit depths and color types, because composition happens on rendered RGBA pixels, not on indexes.

The compositing itself is a library type, `LayerCompositor`, independent of the scene resource: it takes rendered layer images with positions and visibility, and a backdrop, and produces the composite. Scenes use it, and so do metatile maps for their internal layers.

A scene opens in the graphics editor:

- **View**: the composite, with a layer list beside it to show or hide layers and reorder them. Visibility and order are part of the scene and are saved with it.
- **Draw** (phase 2): one layer is active. Pixel tools edit that layer's arranger at the canvas position minus the layer offset; pixels outside the active layer are not edited. Save writes through the active layer's arranger, under that arranger's read-only rules, so a layer over compressed data stays read-only.
- **Arrange** is not available on a scene; arranging happens in a layer's own editor ("Open Layer" in the layer list).

Export writes the composite to PNG, in the UI and the CLI. Import into a scene is not supported.

What does not change: arrangers, palettes and their formats. A scene holds references only, so deleting or renaming a layer's arranger works like other linked resources. Projects without scenes keep their format version.

## Decisions

- **A composition of arrangers, not layers inside an arranger.** Every layer is already a resource the user can open, draw on and export on its own, and the FireRed and FF4 projects already hold them. Reason: no change to `Arranger`, its elements or its formats, and one arranger can appear in several scenes (a status bar over several screens). Rejected: a layer index on arranger elements (rewrites every arranging, selection and serialization path); a multi-layer arranger type (two ways to store the same thing).
- **Composite on rendered RGBA, back to front.** Hardware composites after palette lookup, and so does this. Reason: indexed layers with different palettes and bit depths cannot share one index space. Consequence: a composite PNG is always direct color.
- **The compositor is separate from the scene resource.** `LayerCompositor` works on rendered layer images and knows nothing about resources. Reason: metatile maps composite their own layers and must not need a scene resource to look right, and tests of compositing need no project. Rejected: compositing inside the scene editor only.
- **Order is the layer list, then tilemap priority.** Phase 1 draws layers strictly in list order. SNES and GBA split each BG by a per-tile priority bit, and tilemaps now decode and keep that bit (LIB-TILEMAPS), so a later phase lets a tilemap layer be marked "split by priority": its high-priority tiles draw above the layers listed after it. Reason: scattered arrangers have no priority to sort by, and the bit has a source of truth only in a tilemap's entries. Rejected: per-element priority on scattered arrangers (no source of truth in the ROM).
- **No blending or effects.** Alpha blending, windows, mosaic, color math and HDMA scroll are runtime effects, not stored graphics. Scenes show what the data holds. Rejected: modeling GBA `BLDCNT` or SNES color math; they belong to an emulator.
- **Offsets, no scroll wrap.** A layer sits at a pixel position on the canvas and is clipped to it. Reason: the use is laying out stored screens, not reproducing scroll registers. Wrapping can be added later as a layer flag.
- **Draw edits one active layer.** Reason: a pixel on the canvas belongs to exactly one layer's data, and picking it implicitly (the topmost opaque pixel) makes it impossible to paint into transparent areas. Rejected: auto-picking the layer under the cursor.
- **No import into a scene.** Splitting a composite PNG back into layers is ambiguous wherever layers overlap. Users import into a layer's arranger as today.
- **Name: scene.** It describes a screen built from parts without implying an image-editor layer stack. Rejected: "composition" (vague), "layered arranger" (suggests an arranger subtype, which it is not). See Open questions.

## Spec changes

- **New spec `docs/specs/lib/scenes.md` (LIB-SCENES):** `LayerCompositor` (order, transparency, clipping, mixed color types, backdrop), used by LIB-METATILE-MAPS; `Scene`, `SceneLayer`, active-layer coordinate mapping, read-only rule (the active layer's arranger); priority split for tilemap layers (later phase).
- **New spec `docs/specs/ui/scene-editor.md` (UI-SCENE-EDITOR), or new sections in UI-GRAPHICS-EDITOR:** layer list (visibility, order, active layer, Open Layer), modes available on a scene, Draw on the active layer.
- **LIB-PROJECT-FORMAT:** the `scene` resource (XSD, model, reader, writer, mapper); load order after arrangers; version under LIB-PROJECT-FORMAT's "Write the lowest version that can hold the project" rule.
- **LIB-PROJECT-SERVICE / LIB-PROJECT-TREE:** add, rename, move and delete scenes; deleting or moving an arranger updates or removes the layers that name it, listed in the deletion plan.
- **LIB-IMAGE-IO:** composite export to a direct-color PNG.
- **CLI-COMMANDS:** `export` and `exportall` include scenes; `import` and `importall` skip them and report it.
- **UI-PROJECT-TREE:** "New Scene..." and "Add to Scene" on arrangers.
- **ARCHITECTURE:** §1 drops layers from the non-goals; §3 adds Scene to the domain list.
- **LIB-TILEMAPS:** the "Priority is kept, not shown" decision points here for priority-split layers.

## Tasks

1. **Compositor.** Done (see Status). `LayerCompositor`; this task is shared with [metatile maps](../specs/lib/metatile-maps.md) and is built first. Tests: two 4bpp layers with transparent index 0 composite in order; an 8bpp layer under a 4bpp layer; a hidden layer is skipped; a layer partly off the canvas is clipped; the backdrop fills only fully transparent pixels; a direct-color layer's alpha is respected.
2. **Scene and project format and service.** `Scene` and `SceneLayer` over the compositor. Tests: a scene round-trips; a scene naming a missing arranger fails the load naming it, like other references; deleting a layer's arranger removes the layer and the plan lists it; renaming the arranger keeps the scene's reference.
3. **Export.** UI and CLI composite export. Tests: CLI `export` of a scene matches the composite rendered in the core test; `importall` skips scenes and reports them.
4. **UI, View.** Scene editor with the layer list. Manual: build the FireRed title screen from the tilemaps `/Title Screen/Border Tilemap`, `Copyright Tilemap`, `Charizard Tilemap` and `Logo Tilemap` in the game's BG order, and compare with the game; build a trainer card from `/Trainer Card/Background Tilemap` and `Front Tilemap`.
5. **UI, Draw (phase 2).** Active layer, coordinate mapping, read-only banner from the active layer. Tests: a pencil stroke at canvas (x, y) changes the active layer's pixel at (x, y) minus its offset and no other layer; Draw is disabled when the active layer is read-only, with its reason. Manual: draw on the FF4 title screen's logo layer through the scene and save.
6. **Priority split (later phase).** A tilemap layer marked "split by priority" draws its high-priority tiles above the layers listed after it. Tests: a two-tilemap scene with mixed priority bits composites in the hardware order. Manual: a FireRed screen whose BGs use the priority bit.
7. **Close.** Make LIB-SCENES current, update the specs above, and remove the resolved backlog lines.

## Open questions

- Name: "scene", or something closer to what users call it ("screen", "layout")?
- Should sprites (OAM) be a layer kind with its own list of positioned arrangers and per-sprite flip, or is one arranger per sprite at a position enough for now?
- Should the backdrop also come from a palette entry that a layer's palette names implicitly (palette 0, entry 0), or always be set explicitly?
- Does the layer list belong in the graphics editor's side panel, or in a dedicated scene editor?
- Settled: "split by priority" applies to tilemap layers only; scattered arrangers do not gain a per-element priority (see Decisions).
- Should Tiles mode work on a tilemap layer from inside the scene (plotting on the active layer), or only in the layer's own editor through Open Layer?
