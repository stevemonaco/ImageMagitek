# Project XML round-trips exactly

## Why

A 1.0 user never loses work (ARCHITECTURE §1). Today the project reader and writer disagree in several places, so opening a project and saving it can change or drop what it holds, and nothing tests the round trip. Backlog items, each confirmed in the code:

- **1.0** [LIB-PROJECT-FORMAT] `XmlProjectReader.TryDeserializePalette` reads a `filesource`'s `bitoffset` from the palette element (`element`, not `item`), so a file source with a bit offset throws a `NullReferenceException` and the load fails with "An exception occurred while reading". The writer never writes `bitoffset` for a file source, so a non-zero bit offset is also lost on save.
- **1.0** [LIB-PROJECT-FORMAT] A project with a `root` attribute does not round-trip. The reader resolves resources and data file paths against `root`; the writer stores data file paths relative to the project file's directory, and `ResourceFileLocator.Locate` puts the project file under `root`, so a save writes a second project file inside `root`. The reader also looks for the transaction journal under `root`, while the writer and `ProjectService` recovery use the project file's directory (LIB-PROJECT-FORMAT-047, LIB-PROJECT-SERVICE-011).
- **1.0** [LIB-PROJECT-FORMAT] No round-trip or reader-failure tests exist.
- (promoted) [LIB-PALETTES] `SerializationMapperExtensions.MapToModel` collapses evenly spaced file sources into one `filesource` without comparing endianness, so a mixed-endian run is saved with the first source's endianness. 1.0: silent palette corruption on save, then written back into the ROM on the next palette save.
- (promoted) [LIB-PROJECT-FORMAT] An unparseable `nativecolor`/`foreigncolor` is dropped (LIB-PROJECT-FORMAT-013), shifting every later index; the next save writes the shorter palette. 1.0: silent palette corruption.
- (promoted) [LIB-PROJECT-FORMAT] An unresolved data file key fails the load with "'DataFileKey' could not be found in the ProjectTree", naming neither key nor arranger. An unresolved palette key falls back to a global palette matching the key's last segment, else the global default (LIB-PROJECT-FORMAT-023). Confirmed data loss: the element's codec now holds the fallback palette, `MapToModel` maps it to that palette's name, the arranger model no longer equals its persisted model, and the next whole-project write (any rename, move, add or Save, LIB-PROJECT-SERVICE "Tree operations write immediately") rewrites the arranger with the fallback key. Restoring the palette file afterwards does not bring the references back. 1.0: work lost without any user action on the arranger.
- (promoted) [LIB-PROJECT-FORMAT] `ProjectForeignColorSourceModel.ResourceEquals` tests for `ProjectNativeColorSourceModel`, so every palette with a foreign color compares as changed and is rewritten by every project write. 1.0: needless writes widen every save's blast radius and break the "unchanged project writes nothing" check this change adds.
- [LIB-PROJECT-FORMAT] Dead code: `XmlProjectWriter.AddResourceToXmlTree` (and the unreachable `Serialize(ResourceFolderModel)`), `XmlProjectReader.LocateResourceOnDisk`/`LocatePathKey`, `ResourceModel.Parent`/`ChildResources`, `IProjectReader.Version`/`IProjectWriter.Version` (no callers outside the classes), `ProjectTreeBuilder.CreateElement`'s `palette is null` branch (`ResolvePalette` never returns null). The schema's `nativecolor`/`foreigncolor` patterns are `^…$`: XSD regexes have no anchors, so a conforming validator requires literal `^` and `$`. .NET's validator anchors patterns itself and accepts `^`/`$` as anchors (checked: `#FF00FFFF` validates, `^#FF00FFFF$` does not), so TileShop is unaffected; the fix is for external tools and correctness.

Found while confirming, folded in: `ProjectTreeBuilder.CreateElement` throws `InvalidOperationException` ("contains non-indexed codec") both for an indexed arranger naming a direct codec and for an unknown codec, because `null is not IIndexedCodec`. The exception escapes `XmlProjectReader.ReadProject`; only `ProjectService`'s catch turns it into a failure, with a misleading message for the unknown-codec case.

The sibling proposal [project-format-freeze.md](project-format-freeze.md) owns versioning, stable keys, removing the scattered color source (including the reader's `scatteredcolor`/`import`/`export` branches and the commented-out mapping from the same dead-code backlog line), directory scanning and mirror/rotate semantics. This proposal does not touch them.

## What

Reading a project and writing it back preserves everything the format stores. After the change:

- A `filesource` with `bitoffset` loads with that bit offset, and the writer writes `bitoffset` when it is non-zero.
- A project with `root` reads and writes against one base directory: resources and data file paths are relative to `root`, the project file stays where it is, and the journal check looks in the project file's directory.
- The palette mapper splits file-source runs where endianness, spacing or bit alignment changes, using the same rule as `Palette.GetFileRunLength` (LIB-PALETTES-010).
- An unparseable `nativecolor` or `foreigncolor` fails the load, naming the file, entry index and value. The writer refuses to save a foreign color whose model differs from the palette's, so a project that saves always loads.
- An unresolved data file key or palette key fails the load, naming the key, the referencing resource's path key and, for arrangers, the first element position that uses it. A palette key that names a global palette (exactly, ignoring case) still resolves to it; the last-segment fallback is removed.
- Unknown codecs and direct codecs in indexed arrangers fail the load with a reason naming the codec and arranger; no exception escapes the reader.
- An unchanged foreign-color palette is not rewritten.
- Opening a project and saving it without changes writes nothing, except arrangers that name a retired codec (LIB-PROJECT-FORMAT-024).
- The dead code is removed and the schema color patterns lose `^`/`$`.

A round-trip suite pins this, using the three sample projects in `ImageMagitek/_xmlprojectsamples/*.zip` and a hand-written fixture that covers what the samples lack.

Not changed: the file layout, every element and attribute name, `version="0.9"`, path keys, the scattered color source, directory scanning, the meaning of mirror and rotation, the write-ahead log itself, and Save As (separate 1.0 backlog item, LIB-PROJECT-SERVICE). Every project that loads today and has no dangling palette reference or unparseable color loads identically.

## Decisions

- **Round-trip contract: resources, then bytes.** For every resource, the model mapped from the loaded resource after read → write → read equals the one mapped after the first read (`ResourceEquals`); a second write of the same tree is byte-identical to the first; and saving a just-loaded project writes no file except arrangers renamed from retired codecs. Reason: the writer normalizes (picks the most frequent defaults, rewrites the `utf-16` declarations the samples carry, adds `defaultpalette`), so byte equality with hand-written input is not meaningful, but a writer that is not idempotent or a save that rewrites unchanged files is a bug. Rejected: byte comparison against the original fixtures; semantic XML diffing of original vs. saved files, which would need per-attribute normalization rules that duplicate the writer.
- **Real samples plus one synthetic fixture.** The zips are real projects (FF2, Crystalis, CT BOSSX): nested folders, three platforms, the retired codec names `SNES 3bpp` and `PSX 8bpp`, a global palette referenced by name (`DefaultRgba32`), and no ROMs, so every data file loads missing (LIB-PROJECT-FORMAT-030). They have no `root`, `bitoffset`, `endian`, native or foreign colors, mirror, rotation, direct arranger or per-element overrides, so a checked-in `AllFeatures` fixture covers those, written by hand as XML so each attribute the reader must accept is visible in the diff. Rejected: building the synthetic project through `ProjectService`, which only proves the writer agrees with itself.
- **Unresolved palette keys fail the load.** Same rule as data file keys. Reason: a silent fallback followed by an automatic whole-project write destroys the reference (see Why); failing loses nothing and the message says what to restore or edit. Rejected: loading with the fallback and preserving the original key for the writer (needs per-element unresolved state in `ArrangerElement`, which the freeze's stable-key work may make obsolete); loading the arranger as broken like a missing data file (no repair UI exists for a dangling reference before 1.0).
- **Global palettes resolve by exact name only.** The writer always stores a global palette's `Name`, and no sample uses a path ending in a global name, so the last-segment match only ever turns a dangling tree reference into a wrong palette. Exact match stays case-insensitive, as today.
- **Report every dangling key, once.** The builder collects one reason per distinct unresolved key per resource, naming the first element position that uses it, instead of stopping at the first element. Reason: FF2 arrangers reference one palette from hundreds of elements; one line per element would bury the message, and stopping at the first key hides the others.
- **Unparseable colors fail the load.** Mirrors LIB-PALETTES-031 for JSON palettes. Reason: dropping an entry shifts indices, so every later color moves on screen and on the next save. Rejected: substituting a placeholder color, which saves a value the user never chose.
- **The writer refuses output the reader would reject.** A foreign color whose model differs from the palette's fails the save naming palette and index. Reason: now that the reader is strict, writing such a value would make the project unopenable. `ChangeColorModelViewModel` already converts foreign sources; `SetForeignColor` accepting any model stays a separate backlog item (LIB-PALETTES).
- **One base directory, two roles.** `ProjectNode.BaseDirectory` (project file directory, or `root` resolved against it and normalized with `Path.GetFullPath`) holds resources and anchors data file paths for both reader and writer. The project file and the journal stay in the project file's directory. Rejected: anchoring data file paths to the project file's directory in the reader too, which changes the meaning of every existing `root` project.
- **Run splitting reuses `Palette.GetFileRunLength`.** One rule for "these sources form a range" across the palette editor, the color model dialog and the writer.
- **This change lands before the freeze; the freeze extends the suite.** These fixes are format-neutral and 0.9-compatible, and the suite is the safety net the freeze's migration needs: the 0.9 zips become the migration hook's input, and the freeze adds its own cases (version rejection, stable keys, scattered source removal, any mirror/rotate change). The suite pins today's mirror/rotation attributes surviving a round trip, not their rendering semantics.

## Spec changes

**LIB-PROJECT-FORMAT**

Changed:

- **LIB-PROJECT-FORMAT-009** — A data file shall store its `location` relative to the project's base directory (LIB-PROJECT-FORMAT-008), and the reader shall resolve it against the base directory.
- **LIB-PROJECT-FORMAT-011** — Consecutive file color sources that form one run (LIB-PALETTES-010) shall be written as one `filesource` with a hex `fileoffset`, `bitoffset` only when non-zero, an `entries` count, and `endian="big"` only when big-endian, and read back as that many sources from that bit address.
- **LIB-PROJECT-FORMAT-013** — If a `nativecolor` or `foreigncolor` value does not parse in its color model, then the load shall fail naming the file, the entry index and the value.
- **LIB-PROJECT-FORMAT-022** — If a palette's or element's data file key does not resolve, then the load shall fail naming the key and the referencing resource, and for an element, its position.
- **LIB-PROJECT-FORMAT-023** — If an element's palette key does not resolve in the tree, then the reader shall use the global palette whose name equals the key ignoring case, and otherwise fail the load naming the key, the arranger and the element position.
- **LIB-PROJECT-FORMAT-025** — If an element names an unknown codec, or an indexed arranger names a direct codec, then the load shall fail naming the codec and the arranger.
- **LIB-PROJECT-FORMAT-028** — If any resource fails to build, then the load shall fail with all collected reasons, at most one per distinct unresolved key per resource, and return no tree.
- **LIB-PROJECT-FORMAT-029** — If a transaction journal exists in the project file's directory, then the reader shall refuse to load until it is recovered.
- **LIB-PROJECT-FORMAT-036** — The writer shall fail on a palette without a data file, on a scattered color source, and on a foreign color whose model differs from the palette's, naming the palette and index.

Added (Reading, Writing, or a new "Round trip" group):

- When the project has a `root`, the writer shall write the project file in its own directory and every resource under the base directory.
- When a project is read, written and read again, every resource shall map to a model equal to the one mapped after the first read.
- When the same tree is written twice, the second write shall produce byte-identical files.
- When a project that was just read is saved without changes, the writer shall write no file other than arrangers that named a retired codec.

Tests lines: the suite pins 003, 005, 006, 008–012, 015–019, 021, 024–029, 032 and 035 as well as the changed and added requirements above; each gets its test names when the tests exist.

Edge cases: add "A `root` that is a parent of the project file's directory makes the project file part of the scan; it is ignored like any other project file (LIB-PROJECT-FORMAT-005)."

Decisions: add the round-trip contract, unresolved palette keys fail, global palettes by exact name, unparseable colors fail, writer refuses unreadable output, and one base directory.

Open items, stale: the `ProjectForeignColorSourceModel` bullet, the `filesource` `bitoffset` bullet, the `root` bullet, "No project XML round-trip tests exist", the dead-code bullet, the "color patterns use `^…$`" sentence (the `color` attribute sentence stays), and the stable-keys bullet's last sentence about unresolved keys.

**LIB-PALETTES**

Open items, stale: "The project writer collapses consecutive evenly spaced file sources … without comparing endianness".

## Tasks

1. **Model equality and run mapping.** Fix `ProjectForeignColorSourceModel.ResourceEquals` (same type, same model, same value). `MapToModel` uses `Palette.GetFileRunLength`. Tests in `SerializationMapperTests`: `MapToModel_MixedEndianRun_SplitsAtEndianChange`, `MapToModel_BitOffsetRun_KeepsBitAddress`, `MapToModel_GapInRun_SplitsAtGap`; in `SerializationModelTests`: `ForeignColorSourceModel_SameValue_Equal`, `ForeignColorSourceModel_DifferentModel_NotEqual`.
2. **Palette reader and writer.** Read `bitoffset` from the `filesource`; write it when non-zero; unparseable native/foreign values fail naming file, index and value; the writer fails on a foreign color in another model. Remove `^`/`$` from the schema patterns. Tests in `XmlProjectReaderTests`: `FileSource_BitOffset_LoadsAtBitAddress`, `NativeColor_Unparseable_FailsNamingEntry`, `ForeignColor_WrongWidthForModel_FailsNamingEntry`; in `XmlProjectWriterTests`: `FileSource_BitOffset_Written`, `ForeignColor_ModelMismatch_FailsSave`; schema: `Schema_ColorPatterns_HaveNoAnchors` (pattern strings contain no `^`/`$`).
3. **Reference and codec failures.** `ProjectTreeBuilder` returns failures, never throws; messages name key, resource path key and first element position; one reason per distinct key per resource; global palettes resolve by exact name; drop the last-segment fallback and the dead null branch. Tests in `XmlProjectReaderTests`: `SchemaError_FailsWithFileAndLine`, `UnknownRootElement_Fails`, `UnknownCodec_FailsNamingCodecAndArranger`, `IndexedArrangerWithDirectCodec_FailsNamingCodecAndArranger`, `UnresolvedDataFileKey_Palette_FailsNamingKeyAndPalette`, `UnresolvedDataFileKey_Element_FailsNamingKeyArrangerAndPosition`, `UnresolvedPaletteKey_FailsNamingKeyArrangerAndPosition`, `UnresolvedKeyUsedByManyElements_ReportedOnce`, `PaletteKeyNamingGlobalPalette_ResolvesIgnoringCase`, `MissingDefaultPalette_UsesGlobalDefault`, `JournalInProjectFileDirectory_RefusesLoad`.
4. **`root`.** Writer anchors data file paths to `ProjectNode.BaseDirectory`; `ResourceFileLocator.Locate` puts the project file in the project file's directory; the reader normalizes the base directory and checks the journal in the project file's directory. Tests in `XmlProjectRoundTripTests`: `RootRelative_SaveAndReopen_Unchanged`, `RootAbsolute_SaveAndReopen_Unchanged`, `Root_Save_WritesNoProjectFileUnderRoot`; in `ProjectServiceTests`: `RenameProjectRoot_WithRoot_WritesProjectFileBesideOld`.
5. **Round-trip suite.** Copy `ImageMagitek/_xmlprojectsamples/*.zip` to the test output; tests extract each to a temp directory and open it through `ProjectService` with `DefaultRgba32` loaded as a global palette. Add the `AllFeatures` fixture under `ImageMagitek.UnitTests/Fixtures/Projects/`: `root`, nested folders, two data files (the test writes them), a palette mixing little- and big-endian file runs, a bit-offset file source, native and foreign colors, every mirror and rotation value, element `bitoffset`, per-element codec/data file/palette overrides, a global palette reference, a direct arranger, empty cells, a retired codec name. Tests in `XmlProjectRoundTripTests` (theories over the four fixtures): `ReadWriteRead_ResourcesEqual`, `WriteTwice_ByteIdentical`, `SaveUnchanged_WritesOnlyRetiredCodecArrangers` (file contents and timestamps of everything else unchanged), `SaveUnchanged_ForeignColorPalette_NotRewritten`; plus `WalRecoveredSave_LoadsCommittedContent` (a hand-built pending journal with staging files for a changed palette and arranger; open recovers it and loads the new content). The crash-after-`File.Move` recovery bug stays its own backlog item.
6. **Dead code.** Remove `XmlProjectWriter.AddResourceToXmlTree` and `Serialize(ResourceFolderModel)`, `XmlProjectReader.LocateResourceOnDisk`/`LocatePathKey`, `ResourceModel.Parent`/`ChildResources`, `IProjectReader.Version`/`IProjectWriter.Version` (the writer keeps a private version constant until the freeze replaces it). Tests: the full suite passes.
7. **Close.** Update LIB-PROJECT-FORMAT and LIB-PALETTES as listed in Spec changes, with real test names on every `Tests:` line. Delete the resolved backlog lines: the `bitoffset` and `root` 1.0 items, the foreign `ResourceEquals` item, the endianness-collapse item, the unresolved-key and unparseable-color items, and the round-trip test item. Trim the dead-code line to what the freeze still owns (`scatteredcolor`/`import`/`export` reader branches, commented-out mapping) or delete it if the freeze already landed.

No manual tasks: nothing here is UI.

## Open questions

- Should an unresolved palette key fail the load (recommended, and what this proposal specifies), or load with the fallback while preserving the original key on save? Failing makes a project with a deleted palette file refuse to open until the file is restored or the XML edited; the alternative needs per-element unresolved state that stable keys may replace.
- Keep `root` in the 1.0 format? Nothing in the UI or CLI creates one; only hand-edited projects use it. Recommended: fix it here (task 4 is small and keeps those projects working) and let the freeze decide whether 1.0 drops it, with the migration hook folding `root` projects into the standard layout.
