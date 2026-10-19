# Layered scenes

## Why

Consoles build a screen from several layers: two to four backgrounds plus sprites, drawn back to front, with color 0 of each tile transparent. TileShop shows one arranger at a time (layers are an ARCHITECTURE §1 non-goal; backlog P3 under UI-DRAWING). The FireRed test project shows the gap: its title screen is four arrangers (an 8bpp logo, Charizard, the copyright line and the border), each correct but none of them looking like the screen. The FF4 title screen and trainer cards have the same problem, and the [tilemaps](tilemaps.md) proposal keeps the priority bit but cannot show it because there is only one layer.

## What

A **scene** is a new project resource that stacks existing arrangers:

- A canvas size in pixels and a **backdrop**: a palette entry (SNES and GBA draw color 0 of palette 0 behind everything) or a fixed color, or none (transparent).
- An ordered list of **layers**, back to front. Each names an arranger (scattered arranger, or a tilemap once that lands), a pixel position on the canvas, and a visible flag.

Each layer renders exactly as its arranger does today, transparency included (palettes with `zeroindextransparent`, a direct codec's alpha). The scene composites the layer images over the backdrop in order. Layers can mix element sizes, codecs, bit depths and color types, because composition happens on rendered RGBA pixels, not on indexes.

A scene opens in the graphics editor:

- **View**: the composite, with a layer list beside it to show or hide layers and reorder them. Visibility and order are part of the scene and are saved with it.
- **Draw** (phase 2): one layer is active. Pixel tools edit that layer's arranger at the canvas position minus the layer offset; pixels outside the active layer are not edited. Save writes through the active layer's arranger, under that arranger's read-only rules, so a layer over compressed data stays read-only.
- **Arrange** is not available on a scene; arranging happens in a layer's own editor ("Open Layer" in the layer list).

Export writes the composite to PNG, in the UI and the CLI. Import into a scene is not supported.

What does not change: arrangers, palettes and their formats. A scene holds references only, so deleting or renaming a layer's arranger works like other linked resources. Projects without scenes keep their format version.

## Decisions

- **A composition of arrangers, not layers inside an arranger.** Every layer is already a resource the user can open, draw on and export on its own, and the FireRed and FF4 projects already hold them. Reason: no change to `Arranger`, its elements or its formats, and one arranger can appear in several scenes (a status bar over several screens). Rejected: a layer index on arranger elements (rewrites every arranging, selection and serialization path); a multi-layer arranger type (two ways to store the same thing).
- **Composite on rendered RGBA, back to front.** Hardware composites after palette lookup, and so does this. Reason: indexed layers with different palettes and bit depths cannot share one index space. Consequence: a composite PNG is always direct color.
- **Order is the layer list, with tilemap priority later.** Phase 1 draws layers strictly in list order. SNES and GBA split each BG by a per-tile priority bit, so once tilemaps exist, a layer may be marked "split by priority" and its high-priority tiles draw above the layers listed after it. Reason: scattered arrangers have no priority to sort by, and the tilemap work brings the bit. Rejected: per-element priority on scattered arrangers (no source of truth in the ROM).
- **No blending or effects.** Alpha blending, windows, mosaic, color math and HDMA scroll are runtime effects, not stored graphics. Scenes show what the data holds. Rejected: modeling GBA `BLDCNT` or SNES color math; they belong to an emulator.
- **Offsets, no scroll wrap.** A layer sits at a pixel position on the canvas and is clipped to it. Reason: the use is laying out stored screens, not reproducing scroll registers. Wrapping can be added later as a layer flag.
- **Draw edits one active layer.** Reason: a pixel on the canvas belongs to exactly one layer's data, and picking it implicitly (the topmost opaque pixel) makes it impossible to paint into transparent areas. Rejected: auto-picking the layer under the cursor.
- **No import into a scene.** Splitting a composite PNG back into layers is ambiguous wherever layers overlap. Users import into a layer's arranger as today.
- **Name: scene.** It describes a screen built from parts without implying an image-editor layer stack. Rejected: "composition" (vague), "layered arranger" (suggests an arranger subtype, which it is not). See Open questions.

## Spec changes

- **New spec `docs/specs/lib/scenes.md` (LIB-SCENES):** `Scene`, `SceneLayer`, backdrop rules, composite rendering (order, transparency, clipping, mixed color types), active-layer coordinate mapping, read-only rule (the active layer's arranger).
- **New spec `docs/specs/ui/scene-editor.md` (UI-SCENE-EDITOR), or new sections in UI-GRAPHICS-EDITOR:** layer list (visibility, order, active layer, Open Layer), modes available on a scene, Draw on the active layer.
- **LIB-PROJECT-FORMAT:** the `scene` resource (XSD, model, reader, writer, mapper); load order after arrangers; version under the tilemaps proposal's "lowest sufficient version" rule.
- **LIB-PROJECT-SERVICE / LIB-PROJECT-TREE:** add, rename, move and delete scenes; deleting or moving an arranger updates or removes the layers that name it, listed in the deletion plan.
- **LIB-IMAGE-IO:** composite export to a direct-color PNG.
- **CLI-COMMANDS:** `export` and `exportall` include scenes; `import` and `importall` skip them and report it.
- **UI-PROJECT-TREE:** "New Scene..." and "Add to Scene" on arrangers.
- **ARCHITECTURE:** §1 drops layers from the non-goals; §3 adds Scene to the domain list.
- **Tilemaps proposal:** the "Priority is kept, not shown" decision points here for priority-split layers.

## Tasks

1. **Core.** `Scene` and `SceneLayer`, composite rendering into a `DirectImage`. Tests: two 4bpp layers with transparent index 0 composite in order; an 8bpp layer under a 4bpp layer; a hidden layer is skipped; a layer partly off the canvas is clipped; the backdrop fills only fully transparent pixels; a direct-color layer's alpha is respected.
2. **Project format and service.** Tests: a scene round-trips; a scene naming a missing arranger fails the load naming it, like other references; deleting a layer's arranger removes the layer and the plan lists it; renaming the arranger keeps the scene's reference.
3. **Export.** UI and CLI composite export. Tests: CLI `export` of a scene matches the composite rendered in the core test; `importall` skips scenes and reports them.
4. **UI, View.** Scene editor with the layer list. Manual: build the FireRed title screen from `/Title Screen/Title Border`, `Title Copyright`, `Title Charizard` and `Title Logo`, back to front, and compare with the game.
5. **UI, Draw (phase 2).** Active layer, coordinate mapping, read-only banner from the active layer. Tests: a pencil stroke at canvas (x, y) changes the active layer's pixel at (x, y) minus its offset and no other layer; Draw is disabled when the active layer is read-only, with its reason. Manual: draw on the FF4 title screen's logo layer through the scene and save.
6. **Close.** Make LIB-SCENES current, update the specs above, and remove the resolved backlog lines.

## Open questions

- Name: "scene", or something closer to what users call it ("screen", "layout")?
- Should sprites (OAM) be a layer kind with its own list of positioned arrangers and per-sprite flip, or is one arranger per sprite at a position enough for now?
- Should the backdrop also come from a palette entry that a layer's palette names implicitly (palette 0, entry 0), or always be set explicitly?
- Does the layer list belong in the graphics editor's side panel, or in a dedicated scene editor?
- Should "split by priority" wait for the tilemaps proposal, or should scattered arrangers gain an optional per-element priority so baked maps can use it?
