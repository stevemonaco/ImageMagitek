---
id: LIB-PROJECT-FORMAT
title: XML project format and transactional writes
project: ImageMagitek
sources:
  - ImageMagitek/Project/Serialization
  - ImageMagitek/Project/SerializationModels
  - ImageMagitek/Project/ResourceFileLocator.cs
  - ImageMagitek/_schemas/ResourceSchema.xsd
  - ImageMagitek/Utility/Transaction
types:
  - XmlProjectReader
  - XmlProjectWriter
  - XmlProjectSerializerFactory
  - IProjectReader
  - IProjectWriter
  - IProjectSerializerFactory
  - ProjectTreeBuilder
  - SerializationMapperExtensions
  - ResourceModel
  - ImageProjectModel
  - ResourceFolderModel
  - DataFileModel
  - PaletteModel
  - ScatteredArrangerModel
  - ArrangerElementModel
  - IColorSourceModel
  - FileColorSourceModel
  - ProjectNativeColorSourceModel
  - ProjectForeignColorSourceModel
  - ResourceFileLocator
  - Utf8StringWriter
  - WriteAheadLogTransaction
  - WalJournal
  - WalOperation
  - WalOperationType
  - WalOperationState
tests:
  - WriteAheadLogTransactionTests
  - ProjectServiceTests
  - CodecFactoryTests
  - XmlProjectReaderTests
  - XmlProjectWriterTests
  - XmlProjectRoundTripTests
  - SerializationMapperTests
  - SerializationModelTests
depends:
  - LIB-PROJECT-TREE
  - LIB-DATASOURCE
  - LIB-CODECS
  - LIB-COLORS
  - LIB-PALETTES
  - LIB-ARRANGERS
---

# XML project format and transactional writes

## Purpose

How a project tree is stored on disk and read back: one XML file per resource, folders as directories, references between resources as tree path keys, validated against `ResourceSchema.xsd`. Multi-file writes go through a write-ahead-log transaction that is recovered on the next open. LIB-PROJECT-SERVICE decides when to read and write; this spec defines what is written and how it is read.

## Requirements

### Layout on disk

- **LIB-PROJECT-FORMAT-001** — The project file shall be `<project name>.xml` holding only a `project` element; each data file, palette and scattered arranger shall be its own `<resource name>.xml`, and each folder a directory, mirroring the tree under the project's base directory.
  - Tests: `ProjectServiceTests.RenameFolder_MovesDirectoryAndChildResource`, `ProjectServiceTests.MoveNode_RaisesSingleMoved`
- **LIB-PROJECT-FORMAT-002** — A resource's name shall come from its file name without `.xml`, and a folder's name from its directory name; no name is stored inside the XML.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-PROJECT-FORMAT-003** — Files shall be written as UTF-8 XML with a declaration, indented with tabs.
  - Tests: `XmlProjectRoundTripTests.WriteTwice_ByteIdentical`
- **LIB-PROJECT-FORMAT-004** — When reading, every subdirectory of the base directory shall become a folder, including empty directories and directories holding only non-resource files.
  - Tests: untested
- **LIB-PROJECT-FORMAT-005** — When reading, every `*.xml` file under the base directory other than the project file shall be read as a resource; another project file found there shall be ignored.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`, `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`

### Elements and attributes

- **LIB-PROJECT-FORMAT-006** — The writer shall write `version="0.9"` on the project element and `root` only when the project has a non-empty root.
  - Tests: `XmlProjectRoundTripTests.WriteTwice_ByteIdentical`, `XmlProjectRoundTripTests.RootRelative_SaveAndReopen_Unchanged`
- **LIB-PROJECT-FORMAT-007** — The reader shall parse `version` and never compare it; any decimal the schema accepts loads.
  - Tests: untested
- **LIB-PROJECT-FORMAT-008** — When the project has a `root`, the reader shall use it, absolute or relative to the project file's directory, as the base directory for resources and data file paths.
  - Tests: `XmlProjectRoundTripTests.RootRelative_SaveAndReopen_Unchanged`, `XmlProjectRoundTripTests.RootAbsolute_SaveAndReopen_Unchanged`
- **LIB-PROJECT-FORMAT-009** — A data file shall store its `location` relative to the project's base directory (LIB-PROJECT-FORMAT-008), and the reader shall resolve it against the base directory.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`, `XmlProjectRoundTripTests.RootRelative_SaveAndReopen_Unchanged`, `XmlProjectRoundTripTests.RootAbsolute_SaveAndReopen_Unchanged`
- **LIB-PROJECT-FORMAT-010** — A palette shall store its data file key, color model and zero-index transparency, followed by its color sources in order.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`, `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`
- **LIB-PROJECT-FORMAT-011** — Consecutive file color sources that form one run (LIB-PALETTES-010) shall be written as one `filesource` with a hex `fileoffset`, `bitoffset` only when non-zero, an `entries` count, and `endian="big"` only when big-endian, and read back as that many sources from that bit address.
  - Tests: `SerializationMapperTests.MapToModel_MixedEndianRun_SplitsAtEndianChange`, `SerializationMapperTests.MapToModel_BitOffsetRun_KeepsBitAddress`, `SerializationMapperTests.MapToModel_GapInRun_SplitsAtGap`, `XmlProjectReaderTests.FileSource_BitOffset_LoadsAtBitAddress`, `XmlProjectWriterTests.FileSource_BitOffset_Written`, `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`
- **LIB-PROJECT-FORMAT-012** — Native colors shall be written as `nativecolor` and foreign colors as `foreigncolor`, each with a hex `value`.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`, `SerializationModelTests.ForeignColorSourceModel_SameValue_Equal`, `SerializationModelTests.ForeignColorSourceModel_DifferentModel_NotEqual`, `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`
- **LIB-PROJECT-FORMAT-013** — If a `nativecolor` or `foreigncolor` value does not parse in its color model, then the load shall fail naming the file, the entry index and the value.
  - Tests: `XmlProjectReaderTests.NativeColor_Unparseable_FailsNamingEntry`, `XmlProjectReaderTests.ForeignColor_WrongWidthForModel_FailsNamingEntry`
- **LIB-PROJECT-FORMAT-014** — The schema shall accept at most 256 color sources per palette and the color models Bgr15, Abgr16, Rgba32, Nes, Bgr9 and Bgr6.
  - Tests: untested
- **LIB-PROJECT-FORMAT-015** — An arranger shall store its size in elements, element pixel size, layout (`tiled` or `single`) and color type (`indexed` or `direct`).
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`
- **LIB-PROJECT-FORMAT-016** — An arranger shall store the most frequent codec, data file key and, for indexed arrangers, palette key as defaults, and each element shall write `codec`, `datafile` or `palette` only where it differs from the default.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`, `XmlProjectRoundTripTests.WriteTwice_ByteIdentical`
- **LIB-PROJECT-FORMAT-017** — Each non-empty element shall store its hex `fileoffset`, `posx` and `posy`, `bitoffset` only when non-zero, and `mirror` and `rotation` only when not none; empty cells shall not be written.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`, `XmlProjectRoundTripTests.WriteTwice_ByteIdentical`
- **LIB-PROJECT-FORMAT-018** — If an arranger has no `defaultpalette`, then its elements without a `palette` shall use the global default palette.
  - Tests: `XmlProjectReaderTests.MissingDefaultPalette_UsesGlobalDefault`

### References

- **LIB-PROJECT-FORMAT-019** — References to data files and palettes shall be the target's tree path key (`/Folder/Name`); references to global palettes shall be the palette's name.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`, `XmlProjectReaderTests.PaletteKeyNamingGlobalPalette_ResolvesIgnoringCase`
- **LIB-PROJECT-FORMAT-020** — When a resource is renamed or moved, every resource whose references change shall be rewritten on the next project save.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-FORMAT-021** — When reading, data files shall be built first, then palettes, then arrangers, so references resolve whatever the folder order.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`
- **LIB-PROJECT-FORMAT-022** — If a palette's or element's data file key does not resolve, then the load shall fail naming the key and the referencing resource, and for an element, its position.
  - Tests: `XmlProjectReaderTests.UnresolvedDataFileKey_Palette_FailsNamingKeyAndPalette`, `XmlProjectReaderTests.UnresolvedDataFileKey_Element_FailsNamingKeyArrangerAndPosition`
- **LIB-PROJECT-FORMAT-023** — If an element's palette key does not resolve in the tree, then the reader shall use the global palette whose name equals the key ignoring case, and otherwise fail the load naming the key, the arranger and the element position.
  - Tests: `XmlProjectReaderTests.PaletteKeyNamingGlobalPalette_ResolvesIgnoringCase`, `XmlProjectReaderTests.UnresolvedPaletteKey_FailsNamingKeyArrangerAndPosition`
- **LIB-PROJECT-FORMAT-024** — When an element names a retired codec, the reader shall load the codec it was renamed to (LIB-CODECS), and the next save shall write the current name.
  - Tests: `CodecFactoryTests.LegacyName_ResolvesToXmlCodec`, `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`
- **LIB-PROJECT-FORMAT-025** — If an element names an unknown codec, or an indexed arranger names a direct codec, then the load shall fail naming the codec and the arranger.
  - Tests: `XmlProjectReaderTests.UnknownCodec_FailsNamingCodecAndArranger`, `XmlProjectReaderTests.IndexedArrangerWithDirectCodec_FailsNamingCodecAndArranger`

### Reading

- **LIB-PROJECT-FORMAT-026** — The reader shall validate every file against `ResourceSchema.xsd` and stop at the first file that fails, reporting its line numbers.
  - Tests: `XmlProjectReaderTests.SchemaError_FailsWithFileAndLine`
- **LIB-PROJECT-FORMAT-027** — If the project file's root element is not `project`, or a resource file's root is not `datafile`, `palette` or `arranger`, then the load shall fail.
  - Tests: `XmlProjectReaderTests.UnknownRootElement_Fails`
- **LIB-PROJECT-FORMAT-028** — If any resource fails to build, then the load shall fail with all collected reasons, at most one per distinct unresolved key per resource, and return no tree.
  - Tests: `XmlProjectReaderTests.UnresolvedKeyUsedByManyElements_ReportedOnce`
- **LIB-PROJECT-FORMAT-029** — If a transaction journal exists in the project file's directory, then the reader shall refuse to load until it is recovered.
  - Tests: `XmlProjectReaderTests.JournalInProjectFileDirectory_RefusesLoad`
- **LIB-PROJECT-FORMAT-030** — When a data file's target does not exist, the builder shall still attach the data file node, with the source reporting `IsMissing`.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-PROJECT-FORMAT-031** — After reading, every node shall carry the model it was read from and the file or directory it came from.
  - Tests: untested
- **LIB-PROJECT-FORMAT-054** — The reader shall load resource and folder names as they are on disk, including names the name rule (LIB-PROJECT-TREE-030) would refuse.
  - Tests: `ProjectServiceTests.OpenProject_NameBreakingRule_LoadsAndMoves`

### Writing

- **LIB-PROJECT-FORMAT-032** — A project write shall include each resource whose current model differs from its persisted model or whose expected location differs from its disk location, and nothing else.
  - Tests: `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`
- **LIB-PROJECT-FORMAT-033** — After a successful write, each written node's persisted model and disk location shall be updated; after a failed write, neither shall change.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-FORMAT-034** — While a palette node has a committed model, writers shall serialize it instead of the live palette.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`
- **LIB-PROJECT-FORMAT-035** — Folders and in-memory resources shall never be written as files; sequential arrangers shall never be saved.
  - Tests: `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`
- **LIB-PROJECT-FORMAT-036** — The writer shall fail on a palette without a data file, on a scattered color source, and on a foreign color whose model differs from the palette's, naming the palette and index.
  - Tests: `XmlProjectWriterTests.ForeignColor_ModelMismatch_FailsSave`
- **LIB-PROJECT-FORMAT-056** — When given models for several nodes, the writer shall write each to its node's disk location in one transaction and set the nodes' persisted models only on success.
  - Tests: `ProjectServiceTests.DeletePalette_ArrangerFileRewrittenWithFallbackKey`, `ProjectServiceTests.ApplyDeletion_TransactionFails_LeavesTreeResourcesAndDiskUnchanged`

### Write-ahead log

- **LIB-PROJECT-FORMAT-037** — A transaction shall write each new content to `<target>.tmp`, then write `_transaction.json` in the journal directory, then for each target back up an existing file to `<target>.bak`, replace the target and mark the operation completed in the journal.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_SingleNewFile_WritesContent`, `WriteAheadLogTransactionTests.ExecuteAsync_MultipleNewFiles_WritesAllContent`, `WriteAheadLogTransactionTests.ExecuteAsync_OverwritesExistingFile_WithNewContent`, `WriteAheadLogTransactionTests.ExecuteAsync_MixOfNewAndExistingFiles_WritesAll`
- **LIB-PROJECT-FORMAT-038** — When a transaction succeeds, its journal, staging and backup files shall be deleted.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_CleansUpStagingAndBackupFiles`, `WriteAheadLogTransactionTests.ExecuteAsync_CleansUpJournalOnSuccess`
- **LIB-PROJECT-FORMAT-039** — An empty transaction shall succeed without writing anything.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_NoPendingWrites_ReturnsSuccess`
- **LIB-PROJECT-FORMAT-040** — If writing staging files or the journal fails, then the transaction shall delete its staging files, leave every target unchanged and fail.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_StagingFailure_DoesNotModifyOriginal`
- **LIB-PROJECT-FORMAT-041** — If replacing a target fails, then the transaction shall restore replaced targets in reverse order from their backups, delete targets that did not exist before, delete the backups of operations whose target was not replaced, delete the journal and fail listing any rollback errors.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`, `WriteAheadLogTransactionTests.ExecuteAsync_ReplaceFails_LeavesNoBackupFiles`
- **LIB-PROJECT-FORMAT-042** — When recovering with no journal, recovery shall succeed and change nothing.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_NoJournal_ReturnsSuccess`
- **LIB-PROJECT-FORMAT-043** — When recovering a journal whose operations all completed, recovery shall delete the backups, staging files and journal.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_AllOperationsCompleted_CleansUpAndSucceeds`
- **LIB-PROJECT-FORMAT-044** — When recovering pending operations whose staging files exist, recovery shall roll them forward and clean up.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_PendingOperationWithStagingFile_RollsForward`, `WriteAheadLogTransactionTests.RecoverAsync_PendingNewFileWithStagingFile_RollsForwardNewFile`, `WriteAheadLogTransactionTests.RecoverAsync_MultipleOperations_MixedStates_RollsForwardPending`, `XmlProjectRoundTripTests.WalRecoveredSave_LoadsCommittedContent`
- **LIB-PROJECT-FORMAT-055** — When recovering a pending operation whose staging file is missing and whose target exists, recovery shall count it as completed.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_PendingMovedButUnmarked_CountsAsCompleted`, `WriteAheadLogTransactionTests.RecoverAsync_PendingStagingPresentAndMovedOp_RollsForwardBoth`
- **LIB-PROJECT-FORMAT-045** — If a pending operation's staging file and target are both missing, then recovery shall restore from its backup every operation counted as completed and every operation whose target is missing, delete targets that did not exist before, delete the remaining backups, the staging files and the journal, and succeed unless rollback reported errors.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_PendingOperationWithoutStagingFile_RollsBack`
- **LIB-PROJECT-FORMAT-046** — If the journal cannot be read, then recovery shall fail and leave the journal in place.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_CorruptJournal_ReturnsFailed`
- **LIB-PROJECT-FORMAT-047** — The project writer's journal directory shall be the directory of the project file being written.
  - Tests: `ProjectServiceTests.SaveProjectAs_NewDirectory_ProducesProjectThatLoads`

### Round trip

- **LIB-PROJECT-FORMAT-050** — When the project has a `root`, the writer shall write the project file in its own directory and every resource under the base directory.
  - Tests: `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`, `ProjectServiceTests.RenameProjectRoot_WithRoot_WritesProjectFileBesideOld`
- **LIB-PROJECT-FORMAT-051** — When a project is read, written and read again, every resource shall map to a model equal to the one mapped after the first read.
  - Tests: `XmlProjectRoundTripTests.ReadWriteRead_ResourcesEqual`, `XmlProjectRoundTripTests.RootRelative_SaveAndReopen_Unchanged`, `XmlProjectRoundTripTests.RootAbsolute_SaveAndReopen_Unchanged`
- **LIB-PROJECT-FORMAT-052** — When a written project is read and written again, the second write shall produce byte-identical files.
  - Tests: `XmlProjectRoundTripTests.WriteTwice_ByteIdentical`
- **LIB-PROJECT-FORMAT-053** — When a project that was just read is saved without changes, the writer shall write no file other than arrangers that named a retired codec.
  - Tests: `XmlProjectRoundTripTests.SaveUnchanged_WritesOnlyRetiredCodecArrangers`

### Defaults

- **LIB-PROJECT-FORMAT-048** (inherited) — The staging suffix shall be `.tmp`, the backup suffix `.bak`, and the journal file `_transaction.json`.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_CleansUpStagingAndBackupFiles`
- **LIB-PROJECT-FORMAT-049** (inherited) — The serializer shall require at least one global palette and treat the first as the global default.
  - Tests: untested

## Invariants

- After a successful write or read, each node's persisted model equals what is on disk for it.
- The writer never writes a standalone tree; LIB-PROJECT-SERVICE keeps it out of reach.
- A journal on disk means a transaction did not finish; the project is not loaded until it is gone.

## Edge cases

- Duplicate element positions: the last one read wins.
- An element position outside the arranger fails the load.
- A folder and a resource file with the same name in one directory fail the load.
- Any non-resource XML file under the project directory fails the load at schema validation.
- A relative project path resolves against the current directory.
- A `root` that is a parent of the project file's directory makes the project file part of the scan; it is ignored like any other project file (LIB-PROJECT-FORMAT-005).
- A crash during staging leaves orphan `.tmp` files that nothing removes; they are not `.xml`, so loads ignore them.

## Threading and lifetime

- Writes are serialized by the caller (LIB-PROJECT-SERVICE); the writer's own lock covers only a single writer.
- Writer internals continue on thread-pool threads; persisted models and disk locations are updated there.
- Reader and builder are single-use per read and hold no state afterwards.

## Decisions

- **One file per resource, folders as directories.** Projects stay hand-editable and diff-friendly, and a resource moves by moving its file.
- **References are tree path keys.** `datafile="/Roms/FF2"` and per-element `datafile=`/`palette=`. Consequence: renaming or moving anything rewrites every file that references it, and a hand rename in Explorer leaves references dangling. Rejected: GUID keys, because they make hand-editing impractical; an ID plus a path hint, because stale hints mislead anyone reading the files. A stable human-readable key is still open (Open items).
- **Atomic multi-file saves.** All files of one save go through one write-ahead-log transaction, recovered on the next open, so a crash mid-save cannot leave a mix of old and new references.
- **Recovery treats a moved-but-unmarked operation as completed.** Every staging file exists before the journal is written, so a pending operation whose staging file is gone and whose target exists was moved. Rolling forward keeps the user's save, which matches recovery's roll-forward-first rule. Rollback restores only replaced targets, plus any target that is missing while its backup exists. It never restores an operation whose staging file is still present, because that backup may be a partial copy from a crash during the backup copy. A backup whose restore failed is kept, because it is the only copy of the original.
- **Missing data files still attach.** The builder attaches the data file node whatever the disk state, so the project can be repaired (LIB-PROJECT-SERVICE).
- **Retired codec names keep loading.** Projects naming the retired C# SNES 3bpp, PSX 4bpp and PSX 8bpp codecs load the equivalent XML codec, and saving stores the XML codec's name.
- **Pending palette edits are not written.** `PaletteNode.CommittedModel` substitutes the committed state. Reason: palette edits live in the shared `Palette`, so a whole-project save after a tree operation would otherwise write another editor's unsaved state.
- **Sequential arrangers are not project resources.** Deferred to 1.1 as an additive schema change. The full element layout is not persisted either, because a data file is browsed with many layouts and has no arranger to store one on.
- **Standalone trees are never serialized.** No XML is read or written for them, and their nodes never get a model.
- **Round-trip contract: resources, then bytes.** For every resource, the model mapped after read → write → read equals the one mapped after the first read; a written project read and written again is byte-identical; and saving a just-loaded project writes no file except arrangers renamed from retired codecs. Reason: the writer normalizes (most frequent defaults, the `utf-16` declarations the samples carry, an added `defaultpalette`), so byte equality with hand-written input is not meaningful, but a writer that is not idempotent or a save that rewrites unchanged files is a bug. Rejected: byte comparison against the original fixtures; semantic XML diffing of original against saved files, which needs per-attribute normalization rules that duplicate the writer. The suite runs over the three sample zips (real projects with nested folders, three platforms, retired codec names and a global palette reference, no ROMs) and a hand-written `AllFeatures` fixture covering what they lack (`root`, `bitoffset`, `endian`, native and foreign colors, mirror, rotation, a direct arranger, per-element overrides). The fixture is hand-written XML so every attribute the reader must accept is visible in the diff; building it through `ProjectService` would only prove the writer agrees with itself.
- **Unresolved palette keys fail the load.** Same rule as data file keys. Reason: a silent fallback followed by an automatic whole-project write (any rename, move, add or save) rewrites the arranger with the fallback key, and restoring the palette file afterwards does not bring the references back; failing loses nothing and the message says what to restore or edit. Each distinct unresolved key is reported once per resource with its first element position, since one palette is often referenced by hundreds of elements. Rejected: loading with the fallback and preserving the original key for the writer (needs per-element unresolved state in `ArrangerElement`); loading the arranger as broken like a missing data file (no repair UI exists for a dangling reference).
- **Global palettes resolve by exact name.** The writer always stores a global palette's `Name`, so a palette key that is not a tree path key must equal a global name, ignoring case as before. Rejected: the former last-segment match, which only ever turned a dangling tree reference into a wrong palette.
- **Unparseable colors fail the load.** Mirrors LIB-PALETTES-031 for JSON palettes. Reason: dropping an entry shifts every later index on screen and on the next save. Rejected: substituting a placeholder color, which saves a value the user never chose.
- **The writer refuses output the reader would reject.** A foreign color whose model differs from the palette's fails the save, naming palette and index, so a project that saves always loads. `ChangeColorModelViewModel` already converts foreign sources; `SetForeignColor` accepting any model is a separate LIB-PALETTES item.
- **One base directory, two roles.** `ProjectNode.BaseDirectory` (the project file's directory, or `root` resolved against it and normalized) holds resources and anchors data file paths for both reader and writer; the project file and the journal stay in the project file's directory. Rejected: anchoring data file paths to the project file's directory in the reader too, which changes the meaning of every existing `root` project.
- **Existing names load.** The reader does not apply the name rule; a project with an on-disk name that breaks it opens, and the node keeps that name until renamed. Reason: the OS that wrote the names accepts them, and refusing the load would lock users out of their work. Rejected: failing or warning on load. New names are checked by LIB-PROJECT-SERVICE ("One rule for names, enforced by the service").

## Non-goals

- Migrating older formats; no migration hook exists.
- Protecting binary data writes (pixel commits, palette writes into data files) with the write-ahead log; it covers XML only.
- Persisting element layouts, per-file view state or sequential arrangers.

## Open items

- Version: `0.9` is written and never checked (LIB-PROJECT-FORMAT-007). The backlog plans to bump to `1.0`, reject newer versions with a clear message, and add a migration hook. `version` is parsed with the current culture.
- Stable keys: undecided. Option: a project-unique, human-readable `key` assigned from the name at creation and never changed, with references using it, so renames and moves touch only the resource's own file and hand renames stop breaking references. Costs: keys drift from display names, and a copied file duplicates its key, so the reader must report duplicates. Otherwise keep path keys and add a clear "unresolved reference" error tied to relink.
- Scattered color source: the schema has no element for it, the reader skips `scatteredcolor`, `import` and `export` elements that the schema already rejects, and mapping a palette that holds a `ScatteredColorSource` never advances its loop, so the save hangs. The backlog plans to remove it from the 1.0 format.
- The arranger `color` attribute is required by the schema, so the reader's default of `indexed` is unreachable.
- A direct arranger that names an indexed codec loads without a check.
