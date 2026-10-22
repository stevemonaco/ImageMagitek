---
id: LIB-PROJECT-SERVICE
title: Project service
project: ImageMagitek.Services
sources:
  - ImageMagitek.Services/ProjectService.cs
  - ImageMagitek.Services/IProjectService.cs
  - ImageMagitek/Project/ResourceChange.cs
types:
  - ProjectService
  - IProjectService
  - ResourceChange
  - ResourceDeletionPlan
tests:
  - ProjectServiceTests
  - CompressedDataProjectTests
  - TilemapProjectTests
  - MetatileProjectTests
  - TableGenerationProjectTests
depends:
  - LIB-PROJECT-TREE
  - LIB-PROJECT-FORMAT
  - LIB-DATASOURCE
  - LIB-PALETTES
  - LIB-ARRANGERS
  - LIB-TABLES
---

# Project service

## Purpose

Owns the set of open trees (projects and standalone data files) and every operation that changes them on disk: create, open, save, close, add, rename, move, delete, relink a missing data file, and generate resources from a table definition (`PlanGeneration`, `FindGenerationConflicts`, `GenerateResourcesAsync`, LIB-TABLES). It forwards tree and content events from every open tree so consumers subscribe once. It builds on LIB-PROJECT-TREE for the in-memory tree and LIB-PROJECT-FORMAT for files; the UI and CLI call it.

## Requirements

### Lifetime and events

- **LIB-PROJECT-SERVICE-001** — When a project is created or opened, or a data file is opened standalone, the service shall raise `ProjectOpened` with the new tree after it is tracked.
  - Tests: `ProjectServiceTests.CreateNewProject_RaisesProjectOpened`, `ProjectServiceTests.OpenDataFile_RaisesProjectOpened_AndTreeContainsSource`
- **LIB-PROJECT-SERVICE-002** — When an open tree is closed, the service shall dispose every disposable resource in it, stop tracking it, then raise `ProjectClosed`.
  - Tests: `ProjectServiceTests.CloseProject_RaisesProjectClosed`, `ProjectServiceTests.CloseProject_StandaloneFile_RaisesProjectClosedAndReleasesFile`
- **LIB-PROJECT-SERVICE-003** — When asked to close a tree it does not track, the service shall do nothing and raise nothing.
  - Tests: `ProjectServiceTests.CloseProjects_ClosesEveryTreeOnce_AndReleasesFiles`
- **LIB-PROJECT-SERVICE-004** — When all trees are closed, the service shall dispose all their resources, untrack all of them, then raise `ProjectClosed` once per tree.
  - Tests: `ProjectServiceTests.CloseProjects_ClosesEveryTreeOnce_AndReleasesFiles`
- **LIB-PROJECT-SERVICE-005** — While a tree is open, the service shall re-raise its `Changed` as `TreeChanged` and its `ResourceChanged` as `ResourceChanged`, with the tree as sender.
  - Tests: `ProjectServiceTests.PaletteChange_RaisesServiceResourceChangedFromTree`
- **LIB-PROJECT-SERVICE-006** — After a tree is closed, its changes shall raise no service events.
  - Tests: `ProjectServiceTests.ClosedProject_RaisesNoServiceEvents`

### Create and open

- **LIB-PROJECT-SERVICE-007** — When a new empty project is created, the service shall write the project file at the given path's full path in a write-ahead-log transaction, name the project after the file name without extension, and use the file's directory as the base directory.
  - Tests: `ProjectServiceTests.CreateNewProject_RaisesProjectOpened`
- **LIB-PROJECT-SERVICE-060** — If the project file already exists, an open tree has that disk location (full path, ignoring case), its directory does not exist, or the directory holds a `*.xml` file a project load would read or fail on: outside files and directories whose names start with `.` (LIB-PROJECT-FORMAT-004, -005), and either not well-formed or with root `project`, `datafile`, `palette` or `arranger`, then creating an empty project shall fail without writing.
  - Tests: `ProjectServiceTests.CreateNewProject_ExistingFile_FailsAndKeepsFile`, `ProjectServiceTests.CreateNewProject_PathOfOpenProject_Fails`, `ProjectServiceTests.CreateNewProject_MissingDirectory_Fails`, `ProjectServiceTests.CreateNewProject_DirectoryHoldsResourceXml_Fails`, `ProjectServiceTests.CreateNewProject_DirectoryHoldsMalformedXmlOrOtherProject_Fails`, `ProjectServiceTests.CreateNewProject_DirectoryHoldsOnlyIgnoredXml_Succeeds`
- **LIB-PROJECT-SERVICE-008** — When a project is created from an existing data file, the service shall add a data file node named after the data file without extension at the root, save the project, and raise `ProjectOpened` only if the save succeeds.
  - Tests: `ProjectServiceTests.CreateNewProjectWithExistingFile_Succeeds_RaisesProjectOpenedAndLoads`
- **LIB-PROJECT-SERVICE-009** — If the project file already exists, an open tree has that disk location (full path, ignoring case), the data file does not exist, an open project already holds a data file at the same full path (ignoring case), or the project or data file name derived from the file names breaks the name rule (LIB-PROJECT-TREE-030) or matches the other (LIB-PROJECT-TREE-031), then creating a project from an existing data file shall fail without writing.
  - Tests: `ProjectServiceTests.CreateNewProjectWithExistingFile_FileInOpenProject_Fails`, `ProjectServiceTests.CreateNewProjectWithExistingFile_ProjectFileExists_Fails`, `ProjectServiceTests.CreateNewProjectWithExistingFile_DataFileMissing_Fails`
- **LIB-PROJECT-SERVICE-010** — If saving a project created from an existing data file fails, then the service shall untrack it, dispose the data source and return the save's reason.
  - Tests: untested
- **LIB-PROJECT-SERVICE-011** — When a project is opened, the service shall first run write-ahead-log recovery in the project file's directory and fail the open if recovery fails (LIB-PROJECT-FORMAT).
  - Tests: `ProjectServiceTests.OpenProject_LeftoverJournal_RollsForwardBeforeReading`, `ProjectServiceTests.OpenProject_CorruptJournal_FailsAndTracksNothing`
- **LIB-PROJECT-SERVICE-012** — If the project file does not exist, or a tree whose disk location is the same full path ignoring case is already open, then opening shall fail without raising `ProjectOpened`.
  - Tests: `ProjectServiceTests.OpenProject_AlreadyOpenUnderOtherSpelling_Fails`
- **LIB-PROJECT-SERVICE-013** — If reading the project fails or throws, then opening shall fail with the reader's reasons or the exception's type and message, and nothing shall be tracked.
  - Tests: untested
- **LIB-PROJECT-SERVICE-014** — When a project references a data file that is missing on disk, opening shall succeed with that data source reporting `IsMissing`, and palettes on it shall still load.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-PROJECT-SERVICE-015** — If open or save-as is given an empty or whitespace path, or save, save-as, save-resource or close is given a null tree, then the service shall throw instead of returning a failure.
  - Tests: untested

### Standalone data files

- **LIB-PROJECT-SERVICE-016** — When a data file is opened standalone, the service shall create a tree rooted at a data file node named after the file name with extension, whose disk location is the full path.
  - Tests: `ProjectServiceTests.OpenDataFile_RaisesProjectOpened_AndTreeContainsSource`
- **LIB-PROJECT-SERVICE-017** — When the same file is already open standalone, compared by full path ignoring case, opening it shall return the existing tree and raise no `ProjectOpened`.
  - Tests: `ProjectServiceTests.OpenDataFile_SamePathTwice_ReturnsExistingTree`
- **LIB-PROJECT-SERVICE-018** — If the file does not exist, then opening it standalone shall fail.
  - Tests: `ProjectServiceTests.OpenDataFile_MissingFile_Fails`
- **LIB-PROJECT-SERVICE-019** — While a tree is standalone, rename, deletion preview, save-as and save-resource shall fail with "'<name>' is a standalone file, not a project", and add, create folder and move shall fail on their own checks, all without touching disk.
  - Tests: `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`
- **LIB-PROJECT-SERVICE-020** — While a tree is standalone, saving the project shall succeed without writing anything.
  - Tests: `ProjectServiceTests.SaveProjectAsync_StandaloneFile_SucceedsWithoutWriting`

### Save

- **LIB-PROJECT-SERVICE-021** — When a project is saved, the service shall write, in one write-ahead-log transaction, every resource whose current model differs from its persisted model or whose file location changed (LIB-PROJECT-FORMAT).
  - Tests: untested
- **LIB-PROJECT-SERVICE-022** — If writing throws, then save and save-as shall return a failure carrying the exception message instead of throwing.
  - Tests: `ProjectServiceTests.SaveAndSaveAs_WriterThrows_FailWithoutThrowing`
- **LIB-PROJECT-SERVICE-023** — When save-as succeeds, every resource shall have been written under the new project file's directory with folders as directories, data file locations shall be relative to that directory, and the project shall be named after the new file name with no `root`. The root's disk location and base directory shall be the new ones. The old project's files shall be unchanged.
  - Tests: `ProjectServiceTests.SaveProjectAs_NewDirectory_ProducesProjectThatLoads`, `ProjectServiceTests.SaveProjectAs_ThenSave_WritesToNewLocation`
- **LIB-PROJECT-SERVICE-061** — If the save-as target file exists, or its directory holds a `*.xml` file a project load would read or fail on: outside files and directories whose names start with `.` (LIB-PROJECT-FORMAT-004, -005), and either not well-formed or with root `project`, `datafile`, `palette` or `arranger`, then save-as shall fail without writing.
  - Tests: `ProjectServiceTests.SaveProjectAs_TargetDirectoryHoldsResourceXml_FailsWithoutWriting`
- **LIB-PROJECT-SERVICE-062** — If the save-as write fails, then the service shall restore the base directory, name and every node's disk location, remove the directories it created when empty, and return the failure; the old project's files shall be unchanged.
  - Tests: `ProjectServiceTests.SaveProjectAs_WriteFails_RestoresLocationsAndName`, `ProjectServiceTests.SaveAndSaveAs_WriterThrows_FailWithoutThrowing`
- **LIB-PROJECT-SERVICE-024** — When a single resource is saved, the service shall write only that resource's file, when its model changed or overwrite is forced, and shall not rewrite resources that reference it.
  - Tests: `ProjectServiceTests.SaveResource_WritesOnlyThatResource`
- **LIB-PROJECT-SERVICE-025** — If a single resource has no disk location, then saving it shall fail.
  - Tests: `ProjectServiceTests.SaveResource_NoDiskLocation_Fails`
- **LIB-PROJECT-SERVICE-026** — While a palette node has a committed model, project and resource saves shall write the committed state, not the live palette's pending edits.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`

### Add

- **LIB-PROJECT-SERVICE-027** — When a data file, compressed data block, palette, tilemap, metatile set, metatile map or scattered arranger is added under a node of an open project, the service shall write its resource file under the parent's directory in a write-ahead-log transaction, then attach the node and set its disk location and persisted model. It shall then write the project file if its required format version changed (LIB-PROJECT-FORMAT-006); a failure of that write does not fail the add and is retried by the next project save.
  - Tests: `ProjectServiceTests.AddResource_WritesFileWithoutLeftovers`, `TilemapProjectTests.AddingTilemap_RaisesVersion_DeletingLastReturnsTo10OnSave`
- **LIB-PROJECT-SERVICE-028** — If the parent is not in an open tree or cannot contain children, or the resource's name breaks the name rule (LIB-PROJECT-TREE-030), matches a sibling's name (LIB-PROJECT-TREE-031), or (directly under the project root) matches the project's name, then adding shall fail with the rule's message without writing. A name starting with `.` breaks the rule, since the reader would skip it.
  - Tests: `ProjectServiceTests.AddResource_InvalidName_FailsWithoutWriting`, `ProjectServiceTests.AddResource_CaseVariantOfSibling_FailsAndKeepsSiblingFile`, `ProjectServiceTests.AddResource_RootResourceNamedLikeProject_FailsAndKeepsProjectFile`, `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`
- **LIB-PROJECT-SERVICE-029** — If the resource is a folder, an in-memory data source, a sequential arranger or any other type the format cannot write, then adding shall fail.
  - Tests: untested
- **LIB-PROJECT-SERVICE-030** — When a folder is created, the service shall create its directory under the parent's directory, attach the node and set its disk location.
  - Tests: `ProjectServiceTests.RenameFolder_UpdatesNestedFolderLocations`
- **LIB-PROJECT-SERVICE-031** — If the parent cannot contain children or the name breaks the name rule or matches a sibling's name, then creating a folder shall fail without writing; the service shall not choose another name.
  - Tests: `ProjectServiceTests.CreateNewFolder_ExistingName_Fails`, `ProjectServiceTests.CreateNewFolder_InvalidName_CreatesNoDirectory`

### Rename

- **LIB-PROJECT-SERVICE-032** — If the node is not in an open tree, or the new name breaks the name rule, matches a sibling's name other than the node's own, or (for a non-folder directly under the root) matches the project's name, or (for the root) matches a root-level non-folder resource's name, then rename shall fail without changes.
  - Tests: `ProjectServiceTests.RenameResource_CaseVariantOfSibling_Fails`, `ProjectServiceTests.RenameProject_ToRootResourceName_Fails`, `ProjectServiceTests.RenameResource_LeadingDot_Fails`
- **LIB-PROJECT-SERVICE-033** — When a folder is renamed, the service shall move its directory, update the disk locations of it and every nested folder, and save the project so every resource under it and every reference to them is rewritten.
  - Tests: `ProjectServiceTests.RenameFolder_MovesDirectoryAndChildResource`, `ProjectServiceTests.RenameFolder_UpdatesNestedFolderLocations`
- **LIB-PROJECT-SERVICE-034** — If moving the folder's directory fails, or the project save fails, then the service shall restore the name and directory and return a failure; the tree raises the reverse `Renamed`.
  - Tests: `ProjectServiceTests.RenameFolder_DirectoryMoveFails_RaisesReverseRename`
- **LIB-PROJECT-SERVICE-035** — When a resource with a file is renamed, the service shall save the project, which writes the resource under its new file name and rewrites resources that reference it, then delete the old file.
  - Tests: `ProjectServiceTests.RenameFile_LeavesOnlyNewResourceFile`
- **LIB-PROJECT-SERVICE-036** — When a rename changes only the letter case, the resource file shall end up with the new casing and no file with the old casing shall remain.
  - Tests: `ProjectServiceTests.RenameFile_CaseOnly_KeepsResourceFile`
- **LIB-PROJECT-SERVICE-037** — If the project save fails during a resource rename, then the service shall restore the name and keep the old file.
  - Tests: `ProjectServiceTests.RenameFile_ProjectWriteFails_RestoresNameAndKeepsOldFile`
- **LIB-PROJECT-SERVICE-038** — When a node without a disk location is renamed, the service shall rename it in memory only.
  - Tests: untested
- **LIB-PROJECT-SERVICE-039** — When the project root is renamed, the project file shall be written under the new name and the old project file deleted.
  - Tests: untested

### Move

- **LIB-PROJECT-SERVICE-040** — The move check shall fail when the nodes are in different trees, the node has no parent, the target is the node or its current parent, the target has a child whose name matches the node's (ignoring case), the node is a non-folder resource moving to the project root with a name matching the project's, the target cannot contain children, the target folder is inside the moved folder, or the node has no disk location.
  - Tests: `ProjectServiceTests.MoveNode_NodeWithoutDiskLocation_FailsWithoutThrowing`, `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`, `ProjectServiceTests.MoveNode_TargetHasCaseVariant_Fails`
- **LIB-PROJECT-SERVICE-041** — If the node is in no open tree, then the move check shall throw an argument error rather than fail.
  - Tests: untested
- **LIB-PROJECT-SERVICE-042** — When a move passes the check, the service shall move the file or directory, move the node with a single `Moved` change, update disk locations, and save the project.
  - Tests: `ProjectServiceTests.MoveNode_RaisesSingleMoved`
- **LIB-PROJECT-SERVICE-043** — If the file or directory cannot be moved (for example the destination exists), then the move shall fail with the tree, the disk and both files unchanged and no `Moved` change.
  - Tests: `ProjectServiceTests.MoveNode_DestinationFileExists_FailsAndLeavesBothFiles`
- **LIB-PROJECT-SERVICE-044** — If the project save fails after the move, then the service shall move the file or directory back, restore disk locations and move the node back with a reverse `Moved` change.
  - Tests: `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`

### Delete

- **LIB-PROJECT-SERVICE-045** — When a deletion is previewed, the plan shall mark as removed the node, all its descendants, every compressed data source whose parent is among them (repeatedly, so nested compressed data goes too), every palette elsewhere whose data source is among them, then every tilemap whose map source, tile source or slot palette is among them, then every metatile set whose metatile, tile or attribute source or slot palette is among them, and then every metatile map whose block source or listed metatile set is among them (LIB-METATILE-MAPS-019). Tilemaps, metatile sets and metatile maps are not checked by LIB-PROJECT-SERVICE-046 and -047, and a tilemap's tile palette arranger is not a cascading link (LIB-PROJECT-SERVICE-066).
  - Tests: `ProjectServiceTests.DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce`, `CompressedDataProjectTests.DeleteParent_RemovesCompressedDataAndItsArranger`, `TilemapProjectTests.DeletingReferencedResource_RemovesTilemap`, `MetatileProjectTests.DeletingSet_RemovesMapsThatListIt`, `MetatileProjectTests.DeletingDataSource_RemovesSetsAndMaps`
- **LIB-PROJECT-SERVICE-046** — When a surviving element of a surviving arranger uses a removed palette, the plan shall mark the arranger as losing its palette.
  - Tests: `ProjectServiceTests.DeletePalette_OnlyElementsOnItGetFallback`
- **LIB-PROJECT-SERVICE-047** — When every non-empty element of an arranger reads a removed data source, the plan shall mark it removed; when only some do, the plan shall mark it as losing elements.
  - Tests: `ProjectServiceTests.DeleteDataFile_ArrangerOnlyOnIt_IsRemoved`, `ProjectServiceTests.DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements`
- **LIB-PROJECT-SERVICE-048** — When a plan is applied, the service shall write the changed resources in one write-ahead-log transaction, dropping elements that read a removed data source and giving the fallback palette only to indexed elements whose palette was removed. Only after the commit shall it apply the same changes to the live resources and update their persisted models.
  - Tests: `ProjectServiceTests.DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements`, `ProjectServiceTests.DeletePalette_OnlyElementsOnItGetFallback`, `ProjectServiceTests.DeletePalette_ArrangerFileRewrittenWithFallbackKey`
- **LIB-PROJECT-SERVICE-066** — When the plan removes the scattered arranger a surviving tilemap names as its tile palette (LIB-TILEMAPS-072), including one removed by LIB-PROJECT-SERVICE-047, the plan shall mark the tilemap as losing its tile palette and never remove it. Applying the plan shall rewrite the tilemap without `tilepalette` in the same transaction as LIB-PROJECT-SERVICE-048 and, after the commit, clear the live tilemap's tile palette.
  - Tests: `TilemapProjectTests.DeletingTilePaletteArranger_KeepsTilemap_AndClearsTilePalette`
- **LIB-PROJECT-SERVICE-064** — If the transaction for a plan's changed resources fails, then applying the plan shall fail with the tree, every resource and the disk unchanged.
  - Tests: `ProjectServiceTests.ApplyDeletion_TransactionFails_LeavesTreeResourcesAndDiskUnchanged`
- **LIB-PROJECT-SERVICE-049** — After the changed resources are committed, every removed non-folder node shall be removed from the tree and its resource file deleted, and the data source of every removed data file node, every removed tilemap and every removed metatile map shall be disposed. Then removed folders shall be removed deepest first and their directories deleted when empty.
  - Tests: `ProjectServiceTests.DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce`, `ProjectServiceTests.DeleteFolder_WithNestedFolder_RemovesBothDirectories`, `ProjectServiceTests.DeleteDataFile_ReleasesFileHandle`
- **LIB-PROJECT-SERVICE-065** — If a removed resource's file cannot be deleted, then its node shall still be removed and the result shall fail listing the file.
  - Tests: `ProjectServiceTests.ApplyDeletion_ResourceFileLocked_RemovesNodeAndReportsFile`
- **LIB-PROJECT-SERVICE-050** — If a removed folder's directory still holds other files, then the directory shall be left on disk, the node still removed, and the result shall fail listing the kept directories.
  - Tests: `ProjectServiceTests.DeleteFolder_WithUnmanagedFile_KeepsDirectoryAndReportsIt`
- **LIB-PROJECT-SERVICE-051** — Deleting a data file node shall delete its resource file, never the data file itself.
  - Tests: `ProjectServiceTests.DeleteDataFile_KeepsDataFileOnDisk`

### Relink

- **LIB-PROJECT-SERVICE-052** — If the data source is not missing or the replacement file does not exist, then relink shall fail without copying.
  - Tests: `ProjectServiceTests.Relink_SourceNotMissing_Fails`
- **LIB-PROJECT-SERVICE-053** — When a missing data source is relinked, the service shall copy the chosen file to the source's expected location (creating its directory), leaving the chosen file in place and every project reference unchanged.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`
- **LIB-PROJECT-SERVICE-054** — After the copy, the service shall reopen the source, reload every palette in its project that reads from it, and raise `DataWritten` on it so the tree raises `ResourceChanged`.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`

### Names

- **LIB-PROJECT-SERVICE-057** — When a node is renamed to its current name (ordinal), rename shall succeed without writing and without a `Renamed` change.
  - Tests: `ProjectServiceTests.RenameResource_SameName_SucceedsWithoutRenamed`
- **LIB-PROJECT-SERVICE-058** — If the name derived from the project file name breaks the name rule, then creating a new project shall fail without writing.
  - Tests: `ProjectServiceTests.CreateNewProject_InvalidName_FailsWithoutWriting`
- **LIB-PROJECT-SERVICE-059** — `CanAddResource` and `CanRenameResource` shall return the same failure the add, create-folder or rename operation would return for that name, without changing anything.
  - Tests: `ProjectServiceTests.CanRenameResource_MatchesRenameFailure`

### Generate

- **LIB-PROJECT-SERVICE-067** — When generation is planned (`PlanGeneration`), the service shall resolve the data file key to a data source in the tree and the target folder key to a folder, the project root, or a path whose existing part ends in one, then plan with LIB-TABLES using the serializer factory's map formats, and write nothing. If the tree is standalone, the data file key does not name a data source, or the target key names something other than a folder or has a new segment that is not a valid folder name (LIB-PROJECT-TREE-030), or the manifest beside the definition exists and does not read (LIB-TABLES-134), then planning shall fail. Planning takes `overwriteEdited` for a regeneration (LIB-PROJECT-SERVICE-075).
  - Tests: `TableGenerationProjectTests.PlanGeneration_WritesNothing`, `TableGenerationProjectTests.Generate_StandaloneTree_Fails`, `TableGenerationProjectTests.PlanGeneration_UnreadableManifest_Fails`
- **LIB-PROJECT-SERVICE-068** — If the tree is standalone or not open, the plan's data file key no longer names a data source, any planned resource key already exists other than a resource regeneration matched (LIB-PROJECT-SERVICE-073), any planned name matches an existing sibling (LIB-PROJECT-TREE-031) other than an existing folder the plan reuses, a planned resource directly under the root matches the project's name, or the manifest beside the definition exists and does not read (LIB-TABLES-134), then generation shall fail without writing, listing every conflicting key.
  - Tests: `TableGenerationProjectTests.Generate_KeyExists_FailsWritingNothing`, `TableGenerationProjectTests.Generate_StandaloneTree_Fails`
- **LIB-PROJECT-SERVICE-069** — When generation runs, the service shall create the planned folders that are missing, with their directories, build every planned model into the tree through the project builder in plan order (the same resolution and messages as a project load, LIB-PROJECT-FORMAT-021 to -070), and write every generated resource, the project file when its required version changes, and the manifest (LIB-TABLES-130, with this run's generation merged by LIB-TABLES-133) in one write-ahead-log transaction. It shall write no other resource, even one whose saved form would differ from its file, such as a legacy codec name (LIB-CODECS-037), unless the caller opts in (LIB-PROJECT-SERVICE-072).
  - Tests: `TableGenerationProjectTests.Generate_WritesResourcesAndManifestInOneTransaction`, `TableGenerationProjectTests.Generate_SavesAndReloadsWithoutDefinition`, `TableGenerationProjectTests.Generate_LeavesOtherChangedResourcesUnwritten`
- **LIB-PROJECT-SERVICE-070** — If building a model or the transaction fails, then the service shall detach every generated node and created folder, dispose the generated resources, delete the directories it created when they are empty, and return the reasons, leaving the disk as it was.
  - Tests: `TableGenerationProjectTests.Generate_TransactionFails_RollsBackTreeAndDirectories`
- **LIB-PROJECT-SERVICE-071** — When generation conflicts are checked (`FindGenerationConflicts`), the service shall return every key and name conflict LIB-PROJECT-SERVICE-068 lists for the plan, empty when there is none, writing nothing.
  - Tests: `TableGenerationProjectTests.FindGenerationConflicts_ListsClashWithoutWriting`
- **LIB-PROJECT-SERVICE-072** — When generation runs with `saveProject`, the service shall also write every other changed resource in the same transaction, as a project save does (LIB-PROJECT-SERVICE-021).
  - Tests: `TableGenerationProjectTests.Generate_SaveProject_WritesOtherChangedResources`
- **LIB-PROJECT-SERVICE-073** — When generation is planned and the manifest beside the definition has a generation for this project and target folder (LIB-TABLES-133), the plan shall be a regeneration: each entry's resource shall be found at its key with its `type`, or else as the only resource of that type in the project whose file's SHA-256 equals the entry's `hash` (a resource moved or renamed without edits), and the plan matched to the entries (LIB-TABLES-161 to -164) against the found resources' current models and keys. An entry whose resource is not found shall be missing. If the generation's `datafile` differs from the plan's data file key, then planning shall fail naming both.
  - Tests: `TableGenerationProjectTests.Regenerate_FindsMovedResourceByHash`, `TableGenerationProjectTests.Regenerate_DataFileDiffers_Fails`
- **LIB-PROJECT-SERVICE-074** — A changed resource shall keep its current key when it is not at its entry's key (the user moved or renamed it), and otherwise take its planned key, so a changed name or folder template renames or moves it. Added resources shall be named as LIB-PROJECT-SERVICE-068 checks, against every sibling in the tree, the user's own resources included. Each written resource's hash shall be the SHA-256 of the bytes this transaction writes for it.
  - Tests: `TableGenerationProjectTests.Regenerate_MovedResourceKeepsKey`, `TableGenerationProjectTests.Regenerate_RenamedTemplateRenamesUnmovedResource`
- **LIB-PROJECT-SERVICE-075** — A found resource shall be edited when its entry has no `hash`, its current model differs from its persisted one, or its file's SHA-256 differs from the entry's, unless the file exists, its SHA-256 equals that of the writer's serialization of its current model, and that model, with every reference to a found generated resource rewritten to the resource's entry key, serializes to the entry's hash (its file was rewritten only because a resource it references was moved or renamed). An edited resource that is changed or removed shall be kept as it is and reported as "Kept edited <key>", unless the caller passes `overwriteEdited`.
  - Tests: `TableGenerationProjectTests.Regenerate_EditedResourceKept`, `TableGenerationProjectTests.Regenerate_OverwriteEdited_Writes`, `TableGenerationProjectTests.Regenerate_MovedPalette_DependentsNotEdited`, `TableGenerationProjectTests.Regenerate_MovedSetThenEditedMap_StillEdited`, `TableGenerationProjectTests.Regenerate_UnchangedResourceFileDeleted_WritesEntryWithoutHash`
- **LIB-PROJECT-SERVICE-076** — A missing resource shall not be recreated, and shall be reported as "Kept missing <key>"; the user deleted it, or moved and edited it.
  - Tests: `TableGenerationProjectTests.Regenerate_DeletedResourceNotRecreated`
- **LIB-PROJECT-SERVICE-077** — A removed resource shall be deleted as LIB-PROJECT-SERVICE-045 to -048 plan it, unless that plan would remove or change a resource other than this regeneration's removed and replaced resources that uses a resource the plan removes and this regeneration does not replace, or a resource this regeneration writes would still reference it; such a resource shall be kept and reported as "Kept referenced <key>: used by <key>, …".
  - Tests: `TableGenerationProjectTests.Regenerate_RemovedPaletteUsedByUserArranger_Kept`, `TableGenerationProjectTests.Regenerate_RemovedRecord_DeletesResource`
- **LIB-PROJECT-SERVICE-078** — If a regeneration would delete any resource and the caller does not pass `confirmRemovals` to generation (`GenerateResourcesAsync`), then it shall fail without writing, listing every resource it would delete.
  - Tests: `TableGenerationProjectTests.Regenerate_RemovalsUnconfirmed_FailsWritingNothing`
- **LIB-PROJECT-SERVICE-079** — When a regeneration runs, the service shall build the added and written changed models through the project builder, replace each changed resource's node in the tree, and write the added and changed resources, the project file when its version changes and the manifest (LIB-TABLES-166) in one write-ahead-log transaction, writing no unchanged, kept or skipped resource (LIB-TABLES-167), so a resource that references a deleted one does not fail the run. After the commit it shall point every other resource that used a replaced resource, written resources included, at the resource that now has the replaced one's old key when it is of the same type, and otherwise at its replacement (rebuilding compressed data, palettes, metatile sets, tilemaps and metatile maps from their models, changing arrangers' elements and tilemaps' tile palettes in place), write those whose saved form changed, dispose every replaced data source, tilemap and metatile map once all their users point elsewhere, delete the files left at the changed resources' old locations, and delete the removed resources as LIB-PROJECT-SERVICE-048 applies a deletion. If building or the transaction fails, then the tree and the disk shall be as they were (LIB-PROJECT-SERVICE-070).
  - Tests: `TableGenerationProjectTests.Regenerate_WritesOnlyChangedResources`, `TableGenerationProjectTests.Regenerate_TransactionFails_RollsBack`, `TableGenerationProjectTests.Regenerate_WrittenMapReferencesRebuiltSet`, `TableGenerationProjectTests.Regenerate_DependentReferencesResourceNowAtOldKey`, `TableGenerationProjectTests.Regenerate_PaletteOnReplacedCompressedData_StillReadsColors`, `TableGenerationProjectTests.Regenerate_UnchangedResourceFileDeleted_WritesEntryWithoutHash`, `TableGenerationProjectTests.Regenerate_ArrangerReferencingDeletedPalette_KeptAsIs`
### Serialization

- **LIB-PROJECT-SERVICE-063** — The service shall run create, save, save-as, save-resource, add, create-folder, rename, move, delete-apply and generate one at a time; each shall wait for the previous one to finish, including its rollback.
  - Tests: `ProjectServiceTests.ConcurrentSaveAndSaveResource_BothSucceed_NoLeftoverFiles`

### Lookups

- **LIB-PROJECT-SERVICE-055** — The service shall find the open tree containing a node or resource; the `Get` lookups shall throw an argument error when none does, and `FindContainingProject` shall return null.
  - Tests: `ProjectServiceTests.OpenDataFile_RaisesProjectOpened_AndTreeContainsSource`
- **LIB-PROJECT-SERVICE-056** — Two resources shall count as in the same project only when the same open tree contains both.
  - Tests: `ProjectServiceTests.AreResourcesInSameProject_ByContainingTree`

## Invariants

- Every tracked tree has exactly one subscription from the service to each of its two events.
- A node added, created or moved by the service has a disk location that matches where the format would locate it.
- Tree operations write to disk as they happen; no tree-level unsaved state exists.

## Edge cases

- Opening a project by a different path string (relative, other casing) opens a second tree on the same files.
- An arranger whose elements all read removed data files is removed rather than left empty.
- Applying a stale plan after the tree changed is not detected.
- A crash between a delete's commit and its file deletes leaves orphan resource files that nothing references; they reappear on reopen.
- Save-as into a subdirectory of the old project's base directory passes the `*.xml` guard, but the old project loads the copy's files the next time it opens (LIB-PROJECT-FORMAT-005).
- If moving a file back during move rollback throws, the exception escapes.
- If deleting the old file after a successful rename throws, the exception escapes.
- Relink copies on the calling thread before its task completes.
- A failed generation raises `Added` and then `Removed` for the generated nodes and folders, because they are attached before the write so the builder can resolve their keys.
- Generating with a stale plan (the tree changed after planning) is caught only by the key conflict check (LIB-PROJECT-SERVICE-068).
- A resource the user creates later at a missing entry's key is found by that key; its hash differs, so it counts as edited and is kept (LIB-PROJECT-SERVICE-075).
- A user resource that references a generated resource a regeneration renames is written after the commit in a second transaction (LIB-PROJECT-SERVICE-079); a crash between the two leaves its file with the old key.
- A failed regeneration raises `Removed` and `Added` for the changed resources' nodes, since they are detached and reattached, so editors on them close.

## Threading and lifetime

- Not thread-safe: the set of open trees is unsynchronized; call from one thread.
- Events are raised synchronously on the thread that made the change; after an awaited write, rollback changes are raised on the awaiting context.
- The service holds open trees until closed; closing disposes their data sources and releases file handles.
- Disk-writing operations are serialized by one service-wide async lock; the set of open trees is still unsynchronized.
- Service operations do not use `ConfigureAwait(false)`, so tree changes after an awaited write are raised on the caller's context.

## Decisions

- **Project lifetime events.** `ProjectOpened`/`ProjectClosed` replace the UI's project-loaded message; the tree view and recent list follow them.
- **The service forwards tree and content events.** Hooked on open and unhooked on close, with the tree as sender, so consumers subscribe once and closed trees cannot leak handlers. Rejected: a UI-side bridge that republished domain events as messages; it was built and removed because it had one consumer.
- **Immutable deletion plan.** Preview returns a `ResourceDeletionPlan` of immutable `ResourceChange` records that apply consumes; applying removes nodes, which raises the events views need. Preview returns a result so it can refuse a standalone tree. Rejected: a mutable change class passed back and forth.
- **Generation writes only what it generates, unless asked.** A project load can change resources in memory without the user doing anything, such as legacy codec names mapped to their current names (LIB-CODECS-037), and a whole-project save would rewrite those files as a side effect of generating into another folder. Generation therefore writes the generated resources, the manifest and the project file when its version changes; the `saveProject` option (UI checkbox, CLI `--save-project`) restores the whole-project write. Reason: a user generating monsters into `/Monsters` found 27 unrelated arrangers rewritten. Rejected: always saving the project (the side effect above); refusing to generate while other resources differ from their files (blocks generation on a harmless rename).
- **Regeneration never deletes what something else uses, nor without confirmation.** A removed record's resource is kept when deleting it would cascade past this regeneration's own removed resources, such as a user's arranger using a generated palette, and any deletion needs `confirmRemovals`. Reason: regeneration owns only what it created, and a ROM change should never cost the user their own resources. Rejected: cascading as an ordinary delete does (silently removes user resources); never deleting (stale resources pile up in the folder).
- **Regeneration replaces changed nodes and retargets their users.** A changed resource's node is detached and its regenerated model built in its place by the project builder. After the commit, every resource that used the old object is pointed at the new one: arrangers' elements and codec palettes and tilemaps' tile palettes in place, and immutable dependents (compressed data, palettes, metatile sets, tilemaps, metatile maps) rebuilt from their models in memory, written only when their saved form changed (a generated resource they use was renamed). Removed resources are deleted after the commit, as any deletion is ("Resource file deletes happen after the commit, outside the WAL"). Reason: most resource types (compressed data, metatile sets and maps, tilemaps) are immutable, so changed contents are new objects, and a failed commit then only has to reattach the old nodes. Rejected: in-place mutation APIs for every type (a second construction path beside the builder); leaving stale references (users would read the old contents until the project is reopened).
- **`overwriteEdited` is a planning option, `confirmRemovals` a generation option.** Reason: the preview must show what is kept, and the confirmation follows the preview. Rejected: both on generation (the preview could not show what an overwrite keeps); both on planning (a plan could delete without asking after the preview).
- **Removal checks ignore what regeneration replaces.** A removed resource's deletion plan counts only resources that are neither removed nor replaced and that use a resource the plan removes and this regeneration does not replace. Reason: a secondary metatile set that loses a palette is replaced, and its maps, which the deletion plan would cascade to through the old set, would otherwise block every palette removal. Rejected: the deletion plan as it is (blocks the common case above).
- **A deleted generated resource stays deleted.** Regeneration does not recreate a resource the user removed, and keeps its entry so later runs do not either. Reason: deleting a generated resource is how a user says they do not want it. Rejected: recreating it (undoes the user); dropping the entry (the next run would add it again).
- **Tree operations write immediately.** Add writes the resource file, create folder makes the directory, rename/move/delete save as they go. Reason: a tree-level "modified" flag only produced pointless save prompts.
- **Pending palette edits stay out of saves.** The palette editor sets `PaletteNode.CommittedModel` while it has pending edits and finds the node with the non-throwing `FindContainingProject`.
- **Folders holding other files are kept.** A folder being deleted may hold files the user placed there, such as ROMs, so a non-empty directory stays on disk and is reported.
- **Move check comes first.** `MoveNodeAsync` returns the `CanMoveNode` failure before resolving locations, so a node without a disk location fails instead of throwing.
- **Missing data files load.** A missing file no longer fails the load; the source reports `IsMissing` so the project can be repaired.
- **Relink copies the file in.** The chosen file is copied to the expected location and name, so no project reference changes; the source is reopened because its lazy stream caches the failed open. `IsMissing` and the relink key on `FileDataSource`, so standalone trees can reuse them.
- **Standalone trees refuse project-only work.** Rename, delete preview, save-as and save-resource fail with a fixed reason; add, folder and move already fail because a data source has no parent and cannot contain children. These guards keep the project-only file locator and writer out of reach. Saving the project is a successful no-op, so editor-save, close and exit paths need no checks.
- **Opening an open standalone file returns it.** No second `ProjectOpened`, no second file handle. A missing file fails: there is nothing to relink into.
- **Standalone files are never serialized.** No XML is read or written and nothing outlives the session except bytes saved into the file.
- **One rule for names, enforced by the service.** `ResourceName` (LIB-PROJECT-TREE-030, -031) validates a name; the service adds the sibling and project-name checks and is the only enforcer, and dialogs ask it through `CanAddResource`/`CanRenameResource`. Reason: the old defects came from the UI holding a partial copy of the check, or none. Rejected: validation only in dialogs (the CLI, messages and future callers bypass it); validation in tree attach (the reader must keep loading existing names).
  - The Windows character set, reserved device names and trailing dot/space apply on every OS, since projects are shared between OSes and kept in git; the Windows set is a superset of the Linux and macOS sets. Rejected: `Path.GetInvalidFileNameChars()`, which differs per OS.
  - Uniqueness ignores case and compares NFC forms on every OS, since NTFS and APFS are case-insensitive by default and APFS treats composed and decomposed accents as one name. Rejected: OS-dependent comparison.
  - Length is 100 characters and 240 UTF-8 bytes, so `<name>.xml` fits a 255-byte component on ext4/APFS and NTFS while covering No-Intro ROM names. Rejected: 255 characters (fails on Linux for non-ASCII); 60 characters (rejects real ROM names).
  - The project file shares the base directory with root-level resource files, so the project's name is a reserved root-level non-folder name, even when a `root` attribute moves resources elsewhere. Rejected: moving the project file (a format change for 1.0).
  - Folder names cannot end in `.xml`, because folder `x.xml` and resource `x` map to the same disk entry under different node names. Rejected: comparing disk entry names (harder to explain in an error).
  - Names are rejected, never sanitized, so the name on disk is the name typed. Rejected: trimming or replacing characters on accept.
  - Existing names load (LIB-PROJECT-FORMAT): a node with a name that breaks the rule keeps it until renamed, and moving it checks only uniqueness and the project name at the target.
  - Derived names follow the rule: project and data file names taken from file names fail creation with the rule's message, since the picker already chose the file.
  - An unchanged rename is a no-op success in the service, so every caller gets it. Rejected: disabling ✓ in the dialog when unchanged (Enter on the prefilled dialog should just close it).
  - `CreateNewFolder` takes the name verbatim; the UI picks a free default. Rejected: a service that silently changes the requested name.
  - One data file, one project: creating a project from a file an open project already holds fails, because two `FileDataSource`s on one file make saves from one invisible to the other.
- **Save As copies the project's XML and switches to the copy.** A project is a directory of files, so "save as" can only mean writing all of them somewhere else. Setting `ProjectNode.BaseDirectory` and the root name before the write makes every node's located path differ from its disk location, so the existing writer writes every resource without a new code path; the root's persisted model is cleared so the project file is written even when its name and `root` are unchanged. Rejected: removing Save As for 1.0, because copying a project folder by hand loses the relative data file paths when the folder moves. Rejected: keeping the old project open and opening the copy beside it, because open editors would stay bound to the old project's resources.
- **Data files are referenced, not copied.** ROMs and disc images can be hundreds of MB, and a project is metadata over the ROM being hacked; graphics edits after Save As keep going to the same file. A ROM inside the old project folder stays referenced there, which its location shows. Rejected: copying data files, which duplicates large ROMs and silently splits future edits from the ROM the user is working on.
- **Refuse a target directory that holds `*.xml` a load would see.** The reader loads every resource `*.xml` under the base directory and fails on XML that does not parse (LIB-PROJECT-FORMAT-005), so writing into such a directory merges the copy with whatever is there or makes it fail to load. The guard follows the reader's scan rules (`XmlProjectReader.FindLoadableXml`), so files under `.git`, `.idea` or `.vs`, dot-prefixed files and XML with a foreign root never block. Malformed XML blocks because the reader fails on it. A `project` root blocks although the reader ignores it, because the other project's later resources would merge with the new one. The same guard applies to a new empty project. Rejected: requiring an empty directory, because users keep ROMs beside projects; refusing any `*.xml` recursively, which blocked git and IDE folders. Not applied to a project from an existing file: the UI derives that path beside the ROM and offers no other location, and the guard would block the flow; the existing-file check stays.
- **Deletion edits models, then commits, then mutates.** Changed arrangers' new contents come from their `ScatteredArrangerModel`: element models on removed data file keys are dropped and removed palette keys are replaced by the fallback palette's key. Those models are written in one transaction, and only after the commit are the live arrangers unlinked and re-paletted, so a failed commit leaves nothing half-applied. Rejected: mutate first and restore on failure, because restoring a codec's palette in place is not reversible while codecs may be shared (LIB-ARRANGERS). Rejected: mutating a cloned arranger, which depends on the same codec-sharing fix.
- **Resource file deletes happen after the commit, outside the WAL.** The WAL only writes files. A crash between the commit and the deletes leaves orphan resource files that nothing references; they reappear on reopen as harmless leftovers. Rejected: a delete operation in the journal, which is more format surface for a case that does no damage.
- **Single-file writes go through the WAL too.** Add and create each write one file through the writer's one-resource transaction, which gives the atomic replace and recovery already in place for one staging file and one journal write. Rejected: a hand-written temp-then-rename, a second mechanism for the same guarantee.
- **One service-wide write lock.** Serializing per writer does nothing because writers are per call, and serializing per tree misses two projects in one directory, which share `_transaction.json`. Writes are short and user-driven, so a single lock costs nothing noticeable. Every locked operation is async and waits with `WaitAsync`, because a sync wait on the UI thread would deadlock against a holder whose continuation needs that thread. Public operations take the lock and private cores do not, so creating a project from an existing file calls the save core without deadlocking. Rejected: documenting "call one at a time", which the UI cannot guarantee across awaits.
- **Generation attaches, then writes, then rolls back.** Generated nodes are built into the live tree by the project builder, their persisted models cleared, and they are written once with the manifest as an extra file in the same transaction. Reason: the builder resolves keys against the tree, so building into it reuses the load path, and one transaction keeps resources and manifest in step. Rejected: building into a scratch tree and grafting the nodes (the builder would resolve references to existing resources in the wrong tree); writing resources one at a time with `AddResourceAsync` (hundreds of transactions, no single rollback).
- **Dispose a removed data source after the tree raises `Removed`.** Editors on the data file close on `Removed` (UI-PROJECT-TREE decision "Removal prompts belong to the editors"), and the graphics editors' shared clipboard drops element copies on it (UI-ARRANGING-045), so nothing reads the source after it is disposed, and the file is released at once.

## Non-goals

- Persisting per-file view state (codec, offset, palette, element layout) for standalone files; state worth keeping belongs in a project.
- Recent-list entries for standalone files (the UI skips them).
- Adding a standalone file to an existing project; converting it to a new project covers the common case.
- Checking full path length or data file locations on disk; an over-long path fails with the OS's message.

## Open items

- Save-as into a subdirectory of the old project's base directory leaves files the old project will load on reopen; foreign XML under a project directory is LIB-PROJECT-FORMAT's.
- A resource moved after a resource it references was also moved has a file the first move rewrote, so it is not found by hash and counts as missing (LIB-PROJECT-SERVICE-073, -076).
- Manual: rename, move and delete in the project tree with editors open; relink after renaming a ROM and reopening; Save Project As... to a new folder, then close and reopen from there.
