# Tilemaps

**Status:** read-only first phase implemented on `dev-experiment` (draft spec [LIB-TILEMAPS](../specs/lib/tilemaps.md)). Done: Task 1 (formats and decode; no encode), Task 2, Task 4 (the lowest-version rule also covers `compresseddata`), the export half of Task 5, and Task 6 without the rendered preview (the dialog shows a text summary) and with bake. Remaining: Task 3 (Arrange entry writes, Save, encode, empty entry), the import and shared-tile conflict half of Task 5 (Draw and Import are blocked by a temporary read-only reason), the rendered dialog preview, editing tilemap properties after creation, and Task 7 (ARCHITECTURE §1 and §3, spec status, backlog line).

## Why

Most full-screen graphics are not stored as images. Title screens, menus, battle backgrounds, maps and large bosses are a tile set plus a tilemap: one entry per cell holding a tile index, a palette number and flip bits. TileShop has no tilemap support (a non-goal for 1.0; backlog P3 "tilemap/nametable resources"). The workaround is to bake the map into a scattered arranger: one element per cell, with the tile's address, palette and mirror. That is how FF4's title screen was added to the FF2 test project, by a script that read the map and wrote the arranger XML.

A baked arranger renders correctly, and drawing on it edits the real tiles, but it is a snapshot of the map, not the map. Rearranging cells, changing a cell's palette or flipping it edits the arranger, never the map bytes in the ROM, so none of it reaches the game. If a hack edits the map, the arranger goes stale. And it takes a script for every screen.

## What

A **tilemap** is a new project resource that reads its layout from data:

- **Map data:** a data file and offset, a width and height in cells, a screen layout (`linear`, or `snes32` for 32×32-entry screens laid out left to right, then top to bottom), and a **map format** that decodes each entry.
- **Tile set:** a data file, offset, codec and first tile index, so entry index *n* reads the tile at `offset + (n − first) × tile size`. Indexes outside the tile set render as blank cells.
- **Palette slots:** the project palette for each value of the entry's palette field. An entry naming an empty slot uses slot 0.

**Map formats** are declarative XML in `_mapformats/`, like the codecs: entry size in bits, endianness, and bit ranges for tile index, palette, horizontal flip, vertical flip and priority. Built-in formats: SNES BG (16-bit), SNES Mode 7 (8-bit, no attributes), GBA text BG (16-bit), GBA affine (8-bit), Genesis plane (16-bit), Game Boy BG (8-bit) and a plain 8-bit index map.

A tilemap opens in the graphics editor as an arranger whose elements are derived from the entries, so rendering, zoom, the grid, export and pixel drawing work unchanged. Drawing writes the tile, so every cell that uses the tile changes with it, as in the game. **Arrange mode edits the map:** moving, copying, pasting and deleting cells, applying a palette and mirroring rewrite entries instead of element references. A pasted element is accepted when its address is a tile in the tile set with the tile set's codec, and is otherwise refused with the reason. Deleting a cell writes the format's empty entry (a configured tile index, 0 by default). Save writes the changed map entries and tiles in place. The map never grows.

"Bake to Scattered Arranger" keeps today's workaround available as a command: it writes the current map as an ordinary arranger.

What does not change: scattered arrangers, codecs, palettes and their formats. Projects without tilemaps keep format version 1.0.

## Decisions

- **A derived arranger, not a new editor.** A `TilemapArranger` computes its elements from the map, so every renderer, image, export and import path that takes an `Arranger` works on a tilemap. Only Arrange-mode operations and Save have tilemap branches. Rejected: a separate tilemap editor (duplicates the graphics editor); storing the map as a scattered arranger with a "map" flag (two sources of truth for one cell).
- **Map formats are data.** Entry layouts differ only in widths and bit positions across SNES, GBA, Genesis and Game Boy, so XML covers them, as it covers flow and pattern codecs. Game-specific maps (FF4's boss layouts, a byte per cell with `0xFE n` and `0xFF` skip codes) are run-length encoded and become read-only tilemaps over a compressed source once [compression-support](compression-support.md) lands, or a plugin format. Rejected: hard-coding the built-in formats in C#.
- **Arrange mode, not a fourth mode.** Arrange already means "edit which tile goes where" on scattered arrangers. On a tilemap the same gestures write entries. Rotation has no map representation, so it is disabled on tilemaps with a tooltip. Rejected: a separate Tilemap mode (more toolbar for the same gestures).
- **Shared tiles stay shared.** Drawing on a cell edits the tile for every cell that uses it. Import reports a tile that is used in several cells and has different pixels in the PNG as a conflict, and does not apply it. Reason: this is what the hardware does, and silently picking one cell's pixels loses work. Rejected: copy-on-write into new tiles (needs free tile slots and map rewrites; a later "re-tile" feature, see Open questions).
- **Tile set fields live on the tilemap.** Data file, offset, codec and first index are attributes of the tilemap resource, not a separate `tileset` resource. Reason: one resource per screen is simpler to create and read, and two maps that share tiles just name the same range. Rejected for now: a tile set resource (an additive later change if sharing becomes common).
- **Write the lowest format version that can express the project.** A project with no tilemap is written as 1.0, so it still opens in 1.0 builds; adding a tilemap writes 1.1. Reason: older readers refuse any newer version (LIB-PROJECT-FORMAT, "Reject every newer version"), so stamping 1.1 on every save would lock 1.0 users out of projects that do not use the feature. This changes LIB-PROJECT-FORMAT-006. Rejected: always writing the current version.
- **Priority is kept, not shown.** The priority bit round-trips through edits and an overlay can show it, but rendering ignores it, since TileShop shows one layer. Layers stay a non-goal.
- **NES and Game Boy Color attributes are later.** Their palette and flip data live in a separate attribute table (NES: 2 bits per 16×16 area; CGB: a second VRAM bank). The first version supports formats whose attributes sit in the entry, and NES nametables load with palette slot 0. See Open questions.

## Spec changes

- **New spec `docs/specs/lib/tilemaps.md` (LIB-TILEMAPS):** map format schema (`MapFormatSchema.xsd`) and the built-in formats, screen layouts, entry decode and encode, `TilemapArranger`, out-of-range indexes, empty entries, writing entries back.
- **LIB-PROJECT-FORMAT:** the `tilemap` resource (XSD, model, reader, writer, mapper); load order after data files and palettes; LIB-PROJECT-FORMAT-006 changed to write the lowest sufficient version; 1.1 added to the accepted versions.
- **LIB-PROJECT-SERVICE / LIB-PROJECT-TREE:** add, rename, move and delete tilemaps; deleting a data file or palette cascades to tilemaps that reference it.
- **LIB-ARRANGERS:** `TilemapArranger` and its read-only rules (a compressed map is read-only; a codec that cannot encode makes tiles read-only).
- **LIB-IMAGE-IO:** export of tilemaps; the shared-tile conflict status in the import report.
- **UI-ARRANGING:** Arrange operations on tilemaps write entries; paste acceptance and refusal reasons; rotation disabled.
- **UI-PROJECT-TREE / UI-EDITORS:** "New Tilemap..." (data file, offset, format, size, tile set, palette slots, with a live preview); "Bake to Scattered Arranger".
- **CLI-COMMANDS:** export and import include tilemaps; import reports shared-tile conflicts with its existing exit codes.
- **ARCHITECTURE:** §1 drops tilemaps from the non-goals; §3 adds Tilemap to the domain list.

## Tasks

1. **Map formats.** XSD, reader and the built-in formats. Tests: decode and encode round-trip every field for each built-in format; fields wider than the entry or overlapping refuse at load.
2. **TilemapArranger.** Derive elements from the map, with `linear` and `snes32` layouts. Tests: a synthetic 64×32 SNES map renders the same as a hand-built scattered arranger; out-of-range tile indexes render blank; mirror bits map to element mirror.
3. **Writing entries.** Arrange operations become entry writes; Save writes entries and tiles in place. Tests: moving, pasting, applying a palette and mirroring change only the targeted entry bytes (sentinel bytes around the map); pasting a tile from outside the tile set refuses with a reason; deleting writes the empty entry; a tile drawn through one cell changes every cell that uses it.
4. **Project format.** `tilemap` resource and the version rule. Tests: a project with a tilemap round-trips and writes 1.1; a project without one writes 1.0 and opens in a reader that accepts only 1.0; a tilemap naming a missing data file or palette loads with a relink error.
5. **Import and export.** Tests: export matches the rendered map; import writes non-conflicting tiles and reports conflicting shared tiles without applying them.
6. **UI.** New Tilemap dialog, editor integration, bake command. Manual: recreate FF4's title screen as a tilemap (tiles `0x44000`, map `0x46000`, palettes `0x46800`, SNES BG format, 32×28), move a cell, save, and check the map bytes in a hex editor and the screen in an emulator.
7. **Close.** Make LIB-TILEMAPS current, update the specs above, and remove the backlog lines resolved.

## Open questions

- How do NES nametables and GBC maps carry their separate attribute data: a second data range on the tilemap ("attribute source" with its own format), or plugin formats?
- Is 1.1 the right home? Tilemaps need one additive format change; the backlog's 1.1 list already includes sequential arrangers as resources, which could share the version bump.
- Should a tilemap be able to read its tile set and palettes from the [table-driven resources](table-resources.md) definitions, so a generated record can produce a tilemap instead of a baked arranger?
- Is a "re-tile" import (split a PNG into tiles, deduplicate them with flips, write new tiles into free slots and rewrite the map) in scope for a later phase? It is what users ask for when they redraw a title screen, and it needs free-space declaration like table relocation.
- How should the editor show priority: an overlay toggle, a tint, or nothing until layers exist?
