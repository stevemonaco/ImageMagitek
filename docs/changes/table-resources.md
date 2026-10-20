# Table-driven resources

## Why

Games rarely store a sprite as a fixed run of tiles at a known offset. They store a table: one record per monster, character or item, whose fields give a graphics pointer, a palette index, a size class and sometimes a layout. To expose those graphics today, a user finds the table, works out each record by hand, and builds one scattered arranger and one palette per entry. FF4 has 223 monster records and FF5 384, so in practice this needs a script. The FF5 samples (`Samples/FF5MonsterSpritesViewer`, `FF5MonsterSpritesCLI`) are such a script, written in C# against the library. A session building FF4's monsters needed another one, in Python, writing project XML directly.

Those scripts produce snapshots. When a hack repoints a record, gives a monster another palette or moves its graphics, the generated arrangers still point at the old bytes, and nothing in the project records which record they came from. Moving data is the other half of the problem: making a sprite larger means copying its bytes to free space and rewriting the pointer that leads to them. TileShop knows nothing about pointers, so users do that step in a hex editor.

## What

Three phases. Each one is useful on its own and ships separately.

**1. Table definitions and generation.** A table definition is a declarative XML file, data rather than code like the XML codecs ([LIB-CODECS](../specs/lib/codecs.md)). It describes:

- **Address space.** How the game's pointers map to file offsets: built-in mappers for SNES LoROM and HiROM (with or without a copier header), GBA (`0x08000000`), NES and Game Boy banked ROM, and linear.
- **Record table.** Base address, record count and stride, and fields as bit ranges with endianness. Example: FF4's graphics record is `size:8 palette:8 pointer:16le`; FF5's is `bpp:1 tileset:15 large:1 …` read MSB-first.
- **Lookups.** Secondary tables indexed by a field: a size-class table of (width, height) pairs, a sub-record table for bosses, a name table with a text encoding.
- **Expressions.** Small integer expressions over fields and lookups that compute the graphics address, palette address, codec, palette entry count and dimensions, for example `0x48000 + (pointer & 0x7FFF) * 8`. A record can have **variants** selected by a predicate (FF4's `size & 0x80` selects the boss path).
- **Layout.** How cells are filled: `rect` (row-major sequential tiles), `mask` (a presence bitmask that consumes sequential tiles, as in FF5) or `indexmap` (one tile index per cell with skip codes, as in FF4's bosses).

"Generate Resources from Table..." on a data file loads a definition and lists the records with a thumbnail, name, size and palette. Generating creates ordinary resources in a chosen folder: one scattered arranger per record, one palette per distinct palette address, and optionally an overview arranger with every record shelf-packed and per-element palettes. The CLI gets a `generate <project> <datafile> <definition> <folder>` verb. A project built this way loads without the definition.

**2. Regeneration.** Running the same definition into the same folder again rebuilds the resources it created there and reports what changed (records added, removed, repointed, re-paletted). Resources the user created in that folder are left alone.

**3. Relocation.** For a record field the definition marks as a `pointer`, "Move Data..." copies the record's graphics (or palette) to a destination range and rewrites the pointer through the inverse of the definition's expression. The destination must be inside a free-space range the user declares, and must not overlap any generated resource. The file never grows. The copy, the pointer rewrite and the regeneration of the affected resources form one operation that can be undone until Save.

What does not change: codecs, arrangers, palettes and the project format in phases 1 and 2. Phase 3 changes one non-goal in ARCHITECTURE §1 (see Decisions).

## Decisions

- **Definitions are data first; plugins cover the rest.** A declarative definition covers fixed-stride records, bit fields, lookups, expressions, variants and the three layouts. Those are everything the FF4 and FF5 monster tables needed. A game whose layout logic is genuinely procedural uses a **generator plugin**: a contract that takes the data file's bytes and returns record descriptions (cells with address, codec name, palette address and mirror), with no core types. Reason: this follows the codec decision (XML first, C# when a format cannot be expressed), and the FF5 samples can become such a plugin. Rejected: plugins only (every game needs C#); a general scripting language (scripting is a non-goal and a security surface).
- **A small expression grammar, not fixed attributes.** Integer literals in decimal or hex, field and lookup references, `+ - * / % & | ^ << >> ~`, comparisons and `?:`. Reason: real pointers need masks, scales, bank arithmetic and conditions (FF4's pointer bit 15 selects 4bpp tiles), and a fixed `base`/`scale`/`mask` attribute set cannot express them. Rejected: embedding C# or Lua expressions.
- **Generated resources are ordinary resources.** Phase 1 writes scattered arrangers and palettes with no reference to the definition. Reason: the project format stays frozen at 1.0, and a project loads without the definition, as with [container scanners](container-scanners.md). Rejected: a live `table` resource type rendered from the current bytes on every open, which is the most faithful option but a format change, and a dependency at load time on a definition that may be missing. It stays possible later as an additive 1.x resource (see Open questions).
- **Definitions are files, not project resources.** A definition lives anywhere, typically next to the project as `<game>.table.xml`. Reason: the project reader skips XML whose root is not a resource (LIB-PROJECT-FORMAT-005), so definitions can sit inside the project directory without affecting load, and definitions can be shared between projects for the same game. A shipped `_tables` folder is not planned: tables are per game and per revision, not per platform.
- **Regeneration owns only what it created.** A generation run writes a manifest beside the definition (`<game>.table.state.json`) listing the resource keys it created and the record each came from. Regeneration replaces those resources, and deletes the ones whose record no longer exists, after a confirmation that lists them. Reason: the project format has no "generated by" marker, and adding one would be a format change. Rejected: regenerating a whole folder (deletes the user's own resources); tracking by name pattern (breaks on rename).
- **Relocation is narrow and declared.** Only fields marked `pointer` with an invertible expression can move. Only into user-declared free ranges, never growing the file. Never for compressed data. Reason: this is the hex-editor step users do today, made safe by the definition, and nothing outside it is assumed about the game. It amends the ARCHITECTURE non-goal "relocating blocks … ever", which was written for compressed data, where the new size is unknown and archives must be rebuilt. Rejected: automatic free-space detection (runs of `0xFF` or `0x00` are often live data); growing the ROM (header and checksum rules differ per platform).
- **Names come from the ROM where a definition says so.** A lookup can be a fixed-width text table with a character map, as with FF4's monster names. Records without names use `<table> <index:X2>`. Name characters that are invalid in file names are dropped, and duplicate names get the record index as a prefix.

## Spec changes

- **New spec `docs/specs/lib/tables.md` (LIB-TABLES):** definition schema (`TableSchema.xsd`), address mappers, field extraction, the expression grammar and its errors, variants, layouts, the generator plugin contract, generation into resources, the regeneration manifest, and relocation (phase 3).
- **LIB-PLUGINS:** the generator plugin kind, added to `ImageMagitek.Plugins.Contracts` as a minor version.
- **LIB-PROJECT-SERVICE:** generate into a folder in one write-ahead-log transaction; regeneration (replace, delete with confirmation); relocation's pointer write and resource update as one operation.
- **UI-PROJECT-TREE:** "Generate Resources from Table..." on a data file; regeneration from the folder's context menu.
- **New spec or UI-EDITORS section:** the generation dialog (definition picker, record list with thumbnails, target folder, overview option) and the Move Data dialog.
- **CLI-COMMANDS:** the `generate` verb, its exit codes, and `--dry-run` printing the records.
- **ARCHITECTURE §1:** in phase 3, narrow the relocation non-goal to compressed data and archives.

## Tasks

1. **Definition format.** XSD, reader, validation and address mappers. Tests: each mapper round-trips addresses; malformed definitions fail naming the element and attribute; unknown field references fail at load, not at evaluation.
2. **Evaluation.** Fields, lookups, expressions, variants and the three layouts, run over a `DataSource`. Tests: synthetic fixtures for bit fields at every alignment and endianness; expression precedence and division by zero; a `mask` layout and an `indexmap` layout with skip codes match hand-built arrangers; a record whose address falls past the end of the file is reported, not thrown.
3. **Generation.** Records become resources through `ProjectService`, plus the overview arranger. Tests: generated resources save and reload with no definition present; palettes are deduplicated by address and entry count; names are sanitized and unique.
4. **Fixtures for real games.** FF4 (FF2 US) and FF5 definitions as documentation and manual checks. ROMs are not in the repo, so automated tests use synthetic data shaped like these tables. Manual: generate FF4's 209 monster arrangers and compare them with the game in an emulator, including palette swaps and Zeromus (4bpp); port the FF5 samples onto the definition, or onto a generator plugin if the definition cannot express FF5's large-form endian swap.
5. **UI and CLI.** Generation dialog with preview; `generate` verb. Manual: generate into a folder, regenerate after hex-editing one record's palette byte, and check that only that resource changes.
6. **Regeneration.** Manifest, diff and confirmation. Tests: added, removed and changed records; user-created resources in the folder survive.
7. **Relocation (phase 3).** Pointer inversion, free-range declaration, Move Data, undo. Tests: the pointer field is rewritten through the inverse expression for LoROM and GBA; a non-invertible expression refuses; overlapping and out-of-range destinations refuse; the source bytes are left in place.
8. **Close.** Make LIB-TABLES current, update the specs above, and remove the backlog lines resolved.

## Open questions

- Does phase 2 justify a `table` resource type in 1.1, rendered live from the current bytes, instead of a manifest beside the definition? It would make repointing visible without regenerating, at the cost of a format change and a load-time dependency.
- Should generation be able to produce tilemap resources ([tilemaps](tilemaps.md)) for `indexmap` layouts, so FF4's boss layouts stay editable as layouts and are not baked into element lists? The same question applies to [metatile maps](metatile-maps.md) and metatile sets, whose Decisions leave generating a game's whole map list (FireRed's 364 layouts) to table definitions.
- Is relocation in scope at all, or is phase 3 a separate proposal once phases 1 and 2 have users? It is the part that changes a project-wide non-goal.
- Should free ranges be declared in the definition (shared by everyone hacking that revision) or per project (each hack has its own free space)?
- Text encodings for name tables: a simple per-definition character map, or a separate table file format shared with future text tooling?
