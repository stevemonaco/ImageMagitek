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
  - Tests: untested
- **LIB-PROJECT-FORMAT-004** — When reading, every subdirectory of the base directory shall become a folder, including empty directories and directories holding only non-resource files.
  - Tests: untested
- **LIB-PROJECT-FORMAT-005** — When reading, every `*.xml` file under the base directory other than the project file shall be read as a resource; another project file found there shall be ignored.
  - Tests: untested

### Elements and attributes

- **LIB-PROJECT-FORMAT-006** — The writer shall write `version="0.9"` on the project element and `root` only when the project has a non-empty root.
  - Tests: untested
- **LIB-PROJECT-FORMAT-007** — The reader shall parse `version` and never compare it; any decimal the schema accepts loads.
  - Tests: untested
- **LIB-PROJECT-FORMAT-008** — When the project has a `root`, the reader shall use it, absolute or relative to the project file's directory, as the base directory for resources and data file paths.
  - Tests: untested
- **LIB-PROJECT-FORMAT-009** — A data file shall store its `location` relative to the directory of the project file being written, and the reader shall resolve it against the base directory.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-PROJECT-FORMAT-010** — A palette shall store its data file key, color model and zero-index transparency, followed by its color sources in order.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`
- **LIB-PROJECT-FORMAT-011** — Consecutive file color sources at contiguous offsets with the same endianness shall be written as one `filesource` with a hex `fileoffset` and an `entries` count, `endian="big"` only when big-endian, and read back as that many sources.
  - Tests: untested
- **LIB-PROJECT-FORMAT-012** — Native colors shall be written as `nativecolor` and foreign colors as `foreigncolor`, each with a hex `value`.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`
- **LIB-PROJECT-FORMAT-013** — If a `nativecolor` or `foreigncolor` value does not parse, then the reader shall drop that source silently, shifting later indices.
  - Tests: untested
- **LIB-PROJECT-FORMAT-014** — The schema shall accept at most 256 color sources per palette and the color models Bgr15, Abgr16, Rgba32, Nes, Bgr9 and Bgr6.
  - Tests: untested
- **LIB-PROJECT-FORMAT-015** — An arranger shall store its size in elements, element pixel size, layout (`tiled` or `single`) and color type (`indexed` or `direct`).
  - Tests: untested
- **LIB-PROJECT-FORMAT-016** — An arranger shall store the most frequent codec, data file key and, for indexed arrangers, palette key as defaults, and each element shall write `codec`, `datafile` or `palette` only where it differs from the default.
  - Tests: untested
- **LIB-PROJECT-FORMAT-017** — Each non-empty element shall store its hex `fileoffset`, `posx` and `posy`, `bitoffset` only when non-zero, and `mirror` and `rotation` only when not none; empty cells shall not be written.
  - Tests: untested
- **LIB-PROJECT-FORMAT-018** — If an arranger has no `defaultpalette`, then its elements without a `palette` shall use the global default palette.
  - Tests: untested

### References

- **LIB-PROJECT-FORMAT-019** — References to data files and palettes shall be the target's tree path key (`/Folder/Name`); references to global palettes shall be the palette's name.
  - Tests: untested
- **LIB-PROJECT-FORMAT-020** — When a resource is renamed or moved, every resource whose references change shall be rewritten on the next project save.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-FORMAT-021** — When reading, data files shall be built first, then palettes, then arrangers, so references resolve whatever the folder order.
  - Tests: untested
- **LIB-PROJECT-FORMAT-022** — If a palette's or element's data file key does not resolve, then the load shall fail.
  - Tests: untested
- **LIB-PROJECT-FORMAT-023** — If an element's palette key does not resolve in the tree, then the reader shall use the global palette whose name matches the key's last segment ignoring case, or else the global default palette, without reporting it.
  - Tests: untested
- **LIB-PROJECT-FORMAT-024** — When an element names a retired codec, the reader shall load the codec it was renamed to (LIB-CODECS), and the next save shall write the current name.
  - Tests: `CodecFactoryTests.LegacyName_ResolvesToXmlCodec`
- **LIB-PROJECT-FORMAT-025** — If an element names an unknown codec, or an indexed arranger names a direct codec, then the open shall fail.
  - Tests: untested

### Reading

- **LIB-PROJECT-FORMAT-026** — The reader shall validate every file against `ResourceSchema.xsd` and stop at the first file that fails, reporting its line numbers.
  - Tests: untested
- **LIB-PROJECT-FORMAT-027** — If the project file's root element is not `project`, or a resource file's root is not `datafile`, `palette` or `arranger`, then the load shall fail.
  - Tests: untested
- **LIB-PROJECT-FORMAT-028** — If any resource fails to build, then the load shall fail with all collected reasons and return no tree.
  - Tests: untested
- **LIB-PROJECT-FORMAT-029** — If a transaction journal exists in the base directory, then the reader shall refuse to load until it is recovered.
  - Tests: untested
- **LIB-PROJECT-FORMAT-030** — When a data file's target does not exist, the builder shall still attach the data file node, with the source reporting `IsMissing`.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-PROJECT-FORMAT-031** — After reading, every node shall carry the model it was read from and the file or directory it came from.
  - Tests: untested

### Writing

- **LIB-PROJECT-FORMAT-032** — A project write shall include each resource whose current model differs from its persisted model or whose expected location differs from its disk location, and nothing else.
  - Tests: untested
- **LIB-PROJECT-FORMAT-033** — After a successful write, each written node's persisted model and disk location shall be updated; after a failed write, neither shall change.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-FORMAT-034** — While a palette node has a committed model, writers shall serialize it instead of the live palette.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`
- **LIB-PROJECT-FORMAT-035** — Folders and in-memory resources shall never be written as files; sequential arrangers shall never be saved.
  - Tests: untested
- **LIB-PROJECT-FORMAT-036** — The writer shall fail on a palette without a data file and on a scattered color source.
  - Tests: untested

### Write-ahead log

- **LIB-PROJECT-FORMAT-037** — A transaction shall write each new content to `<target>.tmp`, then write `_transaction.json` in the journal directory, then for each target back up an existing file to `<target>.bak`, replace the target and mark the operation completed in the journal.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_SingleNewFile_WritesContent`, `WriteAheadLogTransactionTests.ExecuteAsync_MultipleNewFiles_WritesAllContent`, `WriteAheadLogTransactionTests.ExecuteAsync_OverwritesExistingFile_WithNewContent`, `WriteAheadLogTransactionTests.ExecuteAsync_MixOfNewAndExistingFiles_WritesAll`
- **LIB-PROJECT-FORMAT-038** — When a transaction succeeds, its journal, staging and backup files shall be deleted.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_CleansUpStagingAndBackupFiles`, `WriteAheadLogTransactionTests.ExecuteAsync_CleansUpJournalOnSuccess`
- **LIB-PROJECT-FORMAT-039** — An empty transaction shall succeed without writing anything.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_NoPendingWrites_ReturnsSuccess`
- **LIB-PROJECT-FORMAT-040** — If writing staging files or the journal fails, then the transaction shall delete its staging files, leave every target unchanged and fail.
  - Tests: `WriteAheadLogTransactionTests.ExecuteAsync_StagingFailure_DoesNotModifyOriginal`
- **LIB-PROJECT-FORMAT-041** — If replacing a target fails, then the transaction shall restore completed operations in reverse order from their backups, delete targets that did not exist before, delete the journal and fail listing any rollback errors.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-FORMAT-042** — When recovering with no journal, recovery shall succeed and change nothing.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_NoJournal_ReturnsSuccess`
- **LIB-PROJECT-FORMAT-043** — When recovering a journal whose operations all completed, recovery shall delete the backups, staging files and journal.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_AllOperationsCompleted_CleansUpAndSucceeds`
- **LIB-PROJECT-FORMAT-044** — When recovering pending operations whose staging files exist, recovery shall roll them forward and clean up.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_PendingOperationWithStagingFile_RollsForward`, `WriteAheadLogTransactionTests.RecoverAsync_PendingNewFileWithStagingFile_RollsForwardNewFile`, `WriteAheadLogTransactionTests.RecoverAsync_MultipleOperations_MixedStates_RollsForwardPending`
- **LIB-PROJECT-FORMAT-045** — If a pending operation's staging file is missing, then recovery shall roll back the completed operations, delete the journal, and succeed unless rollback reported errors.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_PendingOperationWithoutStagingFile_RollsBack`
- **LIB-PROJECT-FORMAT-046** — If the journal cannot be read, then recovery shall fail and leave the journal in place.
  - Tests: `WriteAheadLogTransactionTests.RecoverAsync_CorruptJournal_ReturnsFailed`
- **LIB-PROJECT-FORMAT-047** — The project writer's journal directory shall be the directory of the project file being written.
  - Tests: untested

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
- A relative project path with no directory part gives an empty base directory.
- A crash during staging leaves orphan `.tmp` files that nothing removes; they are not `.xml`, so loads ignore them.
- A crash after replacing a target but before marking it completed leaves it pending with no staging file; recovery rolls back only completed operations, so that target keeps the new content and its `.bak` is left behind.
- A failed replace after its backup was made leaves that `.bak` behind.

## Threading and lifetime

- Each writer serializes its own writes with a lock, but a writer is created per call, so writes from separate calls are not serialized.
- Writer internals continue on thread-pool threads; persisted models and disk locations are updated there.
- Reader and builder are single-use per read and hold no state afterwards.

## Decisions

- **One file per resource, folders as directories.** Projects stay hand-editable and diff-friendly, and a resource moves by moving its file.
- **References are tree path keys.** `datafile="/Roms/FF2"` and per-element `datafile=`/`palette=`. Consequence: renaming or moving anything rewrites every file that references it, and a hand rename in Explorer leaves references dangling. Rejected: GUID keys, because they make hand-editing impractical; an ID plus a path hint, because stale hints mislead anyone reading the files. A stable human-readable key is still open (Open items).
- **Atomic multi-file saves.** All files of one save go through one write-ahead-log transaction, recovered on the next open, so a crash mid-save cannot leave a mix of old and new references.
- **Missing data files still attach.** The builder attaches the data file node whatever the disk state, so the project can be repaired (LIB-PROJECT-SERVICE).
- **Retired codec names keep loading.** Projects naming the retired C# SNES 3bpp, PSX 4bpp and PSX 8bpp codecs load the equivalent XML codec, and saving stores the XML codec's name.
- **Pending palette edits are not written.** `PaletteNode.CommittedModel` substitutes the committed state. Reason: palette edits live in the shared `Palette`, so a whole-project save after a tree operation would otherwise write another editor's unsaved state.
- **Sequential arrangers are not project resources.** Deferred to 1.1 as an additive schema change. The full element layout is not persisted either, because a data file is browsed with many layouts and has no arranger to store one on.
- **Standalone trees are never serialized.** No XML is read or written for them, and their nodes never get a model.

## Non-goals

- Migrating older formats; no migration hook exists.
- Protecting binary data writes (pixel commits, palette writes into data files) with the write-ahead log; it covers XML only.
- Persisting element layouts, per-file view state or sequential arrangers.

## Open items

- Version: `0.9` is written and never checked (LIB-PROJECT-FORMAT-007). The backlog plans to bump to `1.0`, reject newer versions with a clear message, and add a migration hook. `version` is parsed with the current culture.
- Stable keys: undecided. Option: a project-unique, human-readable `key` assigned from the name at creation and never changed, with references using it, so renames and moves touch only the resource's own file and hand renames stop breaking references. Costs: keys drift from display names, and a copied file duplicates its key, so the reader must report duplicates. Otherwise keep path keys and add a clear "unresolved reference" error tied to relink. Today an unresolved palette key falls back silently (LIB-PROJECT-FORMAT-023) and an unresolved data file key fails the load with a message naming neither key nor arranger.
- Scattered color source: the schema has no element for it, the reader skips `scatteredcolor`, `import` and `export` elements that the schema already rejects, and mapping a palette that holds a `ScatteredColorSource` never advances its loop, so the save hangs. The backlog plans to remove it from the 1.0 format.
- `ProjectForeignColorSourceModel` equality checks for a native model, so a palette with foreign colors always compares as changed and is rewritten on every save.
- The reader reads a `filesource` `bitoffset` from the palette element, not the source, so a file source with a bit offset fails the load; the writer never writes one.
- `root`: the writer stores data file paths relative to the project file's directory and locates the project file under the base directory, while the reader resolves both against `root`, so a project with `root` does not round-trip.
- The arranger `color` attribute is required by the schema, so the reader's default of `indexed` is unreachable. The color patterns use `^…$`, which XSD treats as literal characters.
- No project XML round-trip tests exist. Planned coverage: every resource type, nested folders, palettes with mixed color sources, element mirror and rotation, legacy codec names, a WAL-recovered save, using `ImageMagitek/_xmlprojectsamples/*.zip` as fixtures.
- Dead code: `XmlProjectWriter.AddResourceToXmlTree`, `XmlProjectReader.LocateResourceOnDisk` and `LocatePathKey`, `ResourceModel.Parent` and `ChildResources`, `IProjectReader.Version` and `IProjectWriter.Version`.
