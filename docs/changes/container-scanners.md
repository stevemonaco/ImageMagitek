# Container scanners

## Why

Many games keep graphics in self-describing files: PSX TIM images inside CD images and `.BIN`/`.PAK` archives, NDS NARC/NCGR/NCLR, and engine-specific packs. A TIM carries its own bit depth, dimensions and CLUT, yet today a user has to find its offset by hand, choose the codec, compute the size and build the palette. Users have asked for "find the TIMs in this archive". The same machinery, a plugin that understands a container format, would let them browse an archive's entries by name instead of by offset.

This is separate from [compression-support](compression-support.md). A scanner finds and describes graphics that are already in a data file. A compressor produces new bytes. The two meet only where a container entry is compressed.

## What

Two plugin kinds, both added to `ImageMagitek.Plugins.Contracts` ([LIB-PLUGINS](../specs/lib/plugins.md)) as a minor version.

**Scanners** search a data file for graphics with a recognizable header and return hits. Each hit has a label, offset and length, plus suggestions: codec name, element or image size, and the palette's location, color model and entry count. In TileShop, "Scan for Graphics..." on a data file runs the chosen scanners with progress and cancel, and lists the hits. Picking hits adds them to the project as ordinary resources: an arranger at the offset with the suggested codec and size, and a palette over the CLUT bytes. The CLI gets a `scan` verb that prints the hits.

**Container readers** parse an archive's table of contents into entries (name, offset, length, and optionally a compression name). In TileShop, a data file opened with a container reader shows its entries as browsable children. Opening an entry opens a sequential editor over that byte range, and adding one to a project stores the resolved range.

The first built-in scanner is PSX TIM: 4bpp, 8bpp, 16bpp and 24bpp, with and without a CLUT. Its codecs and palette color model already ship (PSX 4bpp/8bpp Flow, PSX 16bpp/24bpp, BGR15).

What does not change: codecs, arrangers and the project format's existing resources. A project built from scanner hits loads without the scanner plugin.

## Decisions

- **Separate from compression.** Scanners and container readers locate and describe data. Compressors transform it. Reason: they have different contracts, ship independently, and a TIM scanner needs no compression support. Where an entry is compressed, the container reader names the compressor, and the entry becomes a compressed source once compression-support lands.
- **Scanners suggest; the user chooses.** Hits are listed and nothing is added until the user picks them. Reason: header signatures give false positives (a TIM header is eight bytes with few constraints), so the user is the filter.
- **Results become ordinary resources.** A picked hit is saved as an arranger and a palette, with no scanner reference in the project. Reason: a project must load without the plugin that helped build it, and the format stays frozen. Rejected: a `scanresult` resource type (a new format element and a plugin dependency at load).
- **Container entries are stored as resolved ranges.** An entry added to a project is saved by parent and offset, not by entry name. Reason: as above, the reader is needed to browse, never to load. Rejected: name references resolved at load through the plugin, which break when the plugin is missing or the archive is rebuilt.
- **An uncompressed entry is a window over its parent, and writable.** Editing it writes the parent's bytes in place: same size, no relocation. Reason: this matches editing at an offset today. A compressed entry is read-only, as in compression-support.
- **Stream-based, cancellable scanning.** Scanners take a `Stream`, a progress callback and a `CancellationToken`, not a span of the whole file. Reason: CD images reach 700 MB. Rejected: loading the file into memory.
- **Built-ins for well-specified formats only.** The PSX TIM scanner ships first; NDS NARC/NCGR/NCLR follow if wanted. Game-specific packs are left to plugins, as with compressors.

## Spec changes

- **New spec `docs/specs/lib/scanning.md` (LIB-SCANNING):** scanner and container-reader contracts (signatures, hit and entry fields, progress, cancellation, malformed-input behavior: skip and continue, never hang), the PSX TIM scanner (header validation, CLUT handling, 24bpp width), and hits mapped to resources.
- **LIB-PLUGINS:** discovery and registration of scanner and container-reader types; the contracts minor version bump.
- **LIB-DATASOURCE:** a range data source (parent, offset, length) for container entries, if the existing element addressing cannot express it. See Open questions.
- **LIB-PROJECT-FORMAT:** only if range sources become a resource type (see Open questions).
- **UI-PROJECT-TREE / UI-EDITORS:** "Scan for Graphics..." on a data file; a results dialog with label, offset, size, codec and a thumbnail; add selected hits; container entries as browsable children.
- **CLI-COMMANDS:** a `scan <datafile> [--scanner <name>]` verb printing hits.

## Tasks

1. **Contracts.** Scanner and container-reader interfaces in the contracts assembly (minor version), plus discovery in `PluginService`. Tests: discovery and registration; name collisions refused.
2. **PSX TIM scanner.** Tests: known TIM files at every bit depth with and without a CLUT are found with the right size and palette; truncated headers, CLUT or image sizes past EOF, and random data give no hits and never throw; cancellation stops promptly.
3. **Hits to resources.** Map a hit to an arranger plus a palette through `ProjectService`. Tests: the created resources render the TIM's pixels with its CLUT; the project saves and reloads with no scanner registered.
4. **UI.** Scan command, results dialog, add selected. Manual: scan a PSX disc image, add several TIMs, and compare them with a reference viewer.
5. **CLI.** `scan` verb. Tests: output format and exit codes.
6. **Container readers.** Entry browsing and range sources; one built-in reader if a well-specified format is chosen (Open questions).
7. **Close.** Make LIB-SCANNING current, update the specs above, and remove the backlog lines resolved.

## Open questions

- Does a container entry need a range data source and a new resource type, or is "data file plus offset" enough, with the entry name used only for display? The second keeps the format frozen.
- Which container format, if any, gets a built-in reader first: NDS NARC (well specified), or none, with only the plugin contract and a sample?
- Do scanners run over decompressed sources too, so that "find TIMs in a compressed block" works once compression-support lands? This affects whether a scanner takes a `Stream` or a `DataSource`-backed stream adapter.
- Is the auto-detection item in the backlog (P3, "codec auto-detection across codecs at an offset") a scanner, or a separate feature?
