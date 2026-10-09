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
depends:
  - LIB-PROJECT-TREE
  - LIB-PROJECT-FORMAT
  - LIB-DATASOURCE
  - LIB-PALETTES
  - LIB-ARRANGERS
---

# Project service

## Purpose

Owns the set of open trees (projects and standalone data files) and every operation that changes them on disk: create, open, save, close, add, rename, move, delete, and relink a missing data file. It forwards tree and content events from every open tree so consumers subscribe once. It builds on LIB-PROJECT-TREE for the in-memory tree and LIB-PROJECT-FORMAT for files; the UI and CLI call it.

## Requirements

### Lifetime and events

- **LIB-PROJECT-SERVICE-001** — When a project is created or opened, or a data file is opened standalone, the service shall raise `ProjectOpened` with the new tree after it is tracked.
  - Tests: `ProjectServiceTests.CreateNewProject_RaisesProjectOpened`, `ProjectServiceTests.OpenDataFile_RaisesProjectOpened_AndTreeContainsSource`
- **LIB-PROJECT-SERVICE-002** — When an open tree is closed, the service shall dispose every disposable resource in it, stop tracking it, then raise `ProjectClosed`.
  - Tests: `ProjectServiceTests.CloseProject_RaisesProjectClosed`, `ProjectServiceTests.CloseProject_StandaloneFile_RaisesProjectClosedAndReleasesFile`
- **LIB-PROJECT-SERVICE-003** — When asked to close a tree it does not track, the service shall do nothing and raise nothing.
  - Tests: untested
- **LIB-PROJECT-SERVICE-004** — When all trees are closed, the service shall dispose all their resources, untrack all of them, then raise `ProjectClosed` once per tree.
  - Tests: untested
- **LIB-PROJECT-SERVICE-005** — While a tree is open, the service shall re-raise its `Changed` as `TreeChanged` and its `ResourceChanged` as `ResourceChanged`, with the tree as sender.
  - Tests: `ProjectServiceTests.PaletteChange_RaisesServiceResourceChangedFromTree`
- **LIB-PROJECT-SERVICE-006** — After a tree is closed, its changes shall raise no service events.
  - Tests: `ProjectServiceTests.ClosedProject_RaisesNoServiceEvents`

### Create and open

- **LIB-PROJECT-SERVICE-007** — When a new empty project is created, the service shall write the project file at the given path's full path in a write-ahead-log transaction, name the project after the file name without extension, and use the file's directory as the base directory.
  - Tests: `ProjectServiceTests.CreateNewProject_RaisesProjectOpened`
- **LIB-PROJECT-SERVICE-060** — If the project file already exists, an open tree has that disk location (full path, ignoring case), its directory does not exist, or the directory or any subdirectory holds a `*.xml` file, then creating an empty project shall fail without writing.
  - Tests: `ProjectServiceTests.CreateNewProject_ExistingFile_FailsAndKeepsFile`, `ProjectServiceTests.CreateNewProject_PathOfOpenProject_Fails`, `ProjectServiceTests.CreateNewProject_MissingDirectory_Fails`, `ProjectServiceTests.CreateNewProject_DirectoryHoldsXml_Fails`
- **LIB-PROJECT-SERVICE-008** — When a project is created from an existing data file, the service shall add a data file node named after the data file without extension at the root, save the project, and raise `ProjectOpened` only if the save succeeds.
  - Tests: `ProjectServiceTests.CreateNewProjectWithExistingFile_Succeeds_RaisesProjectOpenedAndLoads`
- **LIB-PROJECT-SERVICE-009** — If the project file already exists, an open tree has that disk location (full path, ignoring case), the data file does not exist, an open project already holds a data file at the same full path (ignoring case), or the project or data file name derived from the file names breaks the name rule (LIB-PROJECT-TREE-030) or matches the other (LIB-PROJECT-TREE-031), then creating a project from an existing data file shall fail without writing.
  - Tests: `ProjectServiceTests.CreateNewProjectWithExistingFile_FileInOpenProject_Fails`, `ProjectServiceTests.CreateNewProjectWithExistingFile_ProjectFileExists_Fails`, `ProjectServiceTests.CreateNewProjectWithExistingFile_DataFileMissing_Fails`
- **LIB-PROJECT-SERVICE-010** — If saving a project created from an existing data file fails, then the service shall untrack it, dispose the data source and return the save's reason.
  - Tests: untested
- **LIB-PROJECT-SERVICE-011** — When a project is opened, the service shall first run write-ahead-log recovery in the project file's directory and fail the open if recovery fails (LIB-PROJECT-FORMAT).
  - Tests: untested
- **LIB-PROJECT-SERVICE-012** — If the project file does not exist, or a tree with the same disk location string is already open, then opening shall fail.
  - Tests: untested
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
- **LIB-PROJECT-SERVICE-019** — While a tree is standalone, rename, deletion preview and save-as shall fail with "'<name>' is a standalone file, not a project", and add, create folder and move shall fail on their own checks, all without touching disk.
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
- **LIB-PROJECT-SERVICE-061** — If the save-as target file exists, or its directory or any subdirectory holds a `*.xml` file, then save-as shall fail without writing.
  - Tests: `ProjectServiceTests.SaveProjectAs_TargetDirectoryHoldsXml_FailsWithoutWriting`
- **LIB-PROJECT-SERVICE-062** — If the save-as write fails, then the service shall restore the base directory, name and every node's disk location, remove the directories it created when empty, and return the failure; the old project's files shall be unchanged.
  - Tests: `ProjectServiceTests.SaveProjectAs_WriteFails_RestoresLocationsAndName`, `ProjectServiceTests.SaveAndSaveAs_WriterThrows_FailWithoutThrowing`
- **LIB-PROJECT-SERVICE-024** — When a single resource is saved, the service shall write only that resource's file, when its model changed or overwrite is forced, and shall not rewrite resources that reference it.
  - Tests: `ProjectServiceTests.SaveResource_WritesOnlyThatResource`
- **LIB-PROJECT-SERVICE-025** — If a single resource has no disk location, then saving it shall fail.
  - Tests: `ProjectServiceTests.SaveResource_NoDiskLocation_Fails`
- **LIB-PROJECT-SERVICE-026** — While a palette node has a committed model, project and resource saves shall write the committed state, not the live palette's pending edits.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`

### Add

- **LIB-PROJECT-SERVICE-027** — When a data file, palette or scattered arranger is added under a node of an open project, the service shall write its resource file under the parent's directory in a write-ahead-log transaction, then attach the node and set its disk location and persisted model.
  - Tests: `ProjectServiceTests.AddResource_WritesFileWithoutLeftovers`
- **LIB-PROJECT-SERVICE-028** — If the parent is not in an open tree or cannot contain children, or the resource's name breaks the name rule (LIB-PROJECT-TREE-030), matches a sibling's name (LIB-PROJECT-TREE-031), or (directly under the project root) matches the project's name, then adding shall fail with the rule's message without writing.
  - Tests: `ProjectServiceTests.AddResource_InvalidName_FailsWithoutWriting`, `ProjectServiceTests.AddResource_CaseVariantOfSibling_FailsAndKeepsSiblingFile`, `ProjectServiceTests.AddResource_RootResourceNamedLikeProject_FailsAndKeepsProjectFile`, `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`
- **LIB-PROJECT-SERVICE-029** — If the resource is a folder, an in-memory data source, a sequential arranger or any other type the format cannot write, then adding shall fail.
  - Tests: untested
- **LIB-PROJECT-SERVICE-030** — When a folder is created, the service shall create its directory under the parent's directory, attach the node and set its disk location.
  - Tests: `ProjectServiceTests.RenameFolder_UpdatesNestedFolderLocations`
- **LIB-PROJECT-SERVICE-031** — If the parent cannot contain children or the name breaks the name rule or matches a sibling's name, then creating a folder shall fail without writing; the service shall not choose another name.
  - Tests: `ProjectServiceTests.CreateNewFolder_ExistingName_Fails`, `ProjectServiceTests.CreateNewFolder_InvalidName_CreatesNoDirectory`

### Rename

- **LIB-PROJECT-SERVICE-032** — If the node is not in an open tree, or the new name breaks the name rule, matches a sibling's name other than the node's own, or (for a non-folder directly under the root) matches the project's name, or (for the root) matches a root-level non-folder resource's name, then rename shall fail without changes.
  - Tests: `ProjectServiceTests.RenameResource_CaseVariantOfSibling_Fails`, `ProjectServiceTests.RenameProject_ToRootResourceName_Fails`
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

- **LIB-PROJECT-SERVICE-045** — When a deletion is previewed, the plan shall mark as removed the node, all its descendants, and every palette elsewhere whose data source is among them.
  - Tests: `ProjectServiceTests.DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce`
- **LIB-PROJECT-SERVICE-046** — When a surviving element of a surviving arranger uses a removed palette, the plan shall mark the arranger as losing its palette.
  - Tests: `ProjectServiceTests.DeletePalette_OnlyElementsOnItGetFallback`
- **LIB-PROJECT-SERVICE-047** — When every non-empty element of an arranger reads a removed data source, the plan shall mark it removed; when only some do, the plan shall mark it as losing elements.
  - Tests: `ProjectServiceTests.DeleteDataFile_ArrangerOnlyOnIt_IsRemoved`, `ProjectServiceTests.DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements`
- **LIB-PROJECT-SERVICE-048** — When a plan is applied, the service shall write the changed resources in one write-ahead-log transaction, dropping elements that read a removed data source and giving the fallback palette only to indexed elements whose palette was removed. Only after the commit shall it apply the same changes to the live resources and update their persisted models.
  - Tests: `ProjectServiceTests.DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements`, `ProjectServiceTests.DeletePalette_OnlyElementsOnItGetFallback`, `ProjectServiceTests.DeletePalette_ArrangerFileRewrittenWithFallbackKey`
- **LIB-PROJECT-SERVICE-064** — If the transaction for a plan's changed resources fails, then applying the plan shall fail with the tree, every resource and the disk unchanged.
  - Tests: `ProjectServiceTests.ApplyDeletion_TransactionFails_LeavesTreeResourcesAndDiskUnchanged`
- **LIB-PROJECT-SERVICE-049** — After the changed resources are committed, every removed non-folder node shall be removed from the tree and its resource file deleted, and the data source of every removed data file node shall be disposed. Then removed folders shall be removed deepest first and their directories deleted when empty.
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

### Serialization

- **LIB-PROJECT-SERVICE-063** — The service shall run create, save, save-as, save-resource, add, create-folder, rename, move and delete-apply one at a time; each shall wait for the previous one to finish, including its rollback.
  - Tests: `ProjectServiceTests.ConcurrentSaveAndSaveResource_BothSucceed_NoLeftoverFiles`

### Lookups

- **LIB-PROJECT-SERVICE-055** — The service shall find the open tree containing a node or resource; the `Get` lookups shall throw an argument error when none does, and `FindContainingProject` shall return null.
  - Tests: `ProjectServiceTests.OpenDataFile_RaisesProjectOpened_AndTreeContainsSource`
- **LIB-PROJECT-SERVICE-056** — Two resources shall count as in the same project when the same open tree contains both, or when no open tree contains either.
  - Tests: untested

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
- **Tree operations write immediately.** Add writes the resource file, create folder makes the directory, rename/move/delete save as they go. Reason: a tree-level "modified" flag only produced pointless save prompts.
- **Pending palette edits stay out of saves.** The palette editor sets `PaletteNode.CommittedModel` while it has pending edits and finds the node with the non-throwing `FindContainingProject`.
- **Folders holding other files are kept.** A folder being deleted may hold files the user placed there, such as ROMs, so a non-empty directory stays on disk and is reported.
- **Move check comes first.** `MoveNodeAsync` returns the `CanMoveNode` failure before resolving locations, so a node without a disk location fails instead of throwing.
- **Missing data files load.** A missing file no longer fails the load; the source reports `IsMissing` so the project can be repaired.
- **Relink copies the file in.** The chosen file is copied to the expected location and name, so no project reference changes; the source is reopened because its lazy stream caches the failed open. `IsMissing` and the relink key on `FileDataSource`, so standalone trees can reuse them.
- **Standalone trees refuse project-only work.** Rename, delete preview and save-as fail with a fixed reason; add, folder and move already fail because a data source has no parent and cannot contain children. These guards keep the project-only file locator and writer out of reach. Saving the project is a successful no-op, so editor-save, close and exit paths need no checks.
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
- **Refuse a target directory that holds `*.xml`.** The reader loads every `*.xml` under the base directory (LIB-PROJECT-FORMAT-005), so writing into such a directory merges the copy with whatever is there or makes it fail to load. The same guard applies to a new empty project. Rejected: requiring an empty directory, because users keep ROMs beside projects. Not applied to a project from an existing file: the UI derives that path beside the ROM and offers no other location, and the guard would block the flow; the existing-file check stays.
- **Deletion edits models, then commits, then mutates.** Changed arrangers' new contents come from their `ScatteredArrangerModel`: element models on removed data file keys are dropped and removed palette keys are replaced by the fallback palette's key. Those models are written in one transaction, and only after the commit are the live arrangers unlinked and re-paletted, so a failed commit leaves nothing half-applied. Rejected: mutate first and restore on failure, because restoring a codec's palette in place is not reversible while codecs may be shared (LIB-ARRANGERS). Rejected: mutating a cloned arranger, which depends on the same codec-sharing fix.
- **Resource file deletes happen after the commit, outside the WAL.** The WAL only writes files. A crash between the commit and the deletes leaves orphan resource files that nothing references; they reappear on reopen as harmless leftovers. Rejected: a delete operation in the journal, which is more format surface for a case that does no damage.
- **Single-file writes go through the WAL too.** Add and create each write one file through the writer's one-resource transaction, which gives the atomic replace and recovery already in place for one staging file and one journal write. Rejected: a hand-written temp-then-rename, a second mechanism for the same guarantee.
- **One service-wide write lock.** Serializing per writer does nothing because writers are per call, and serializing per tree misses two projects in one directory, which share `_transaction.json`. Writes are short and user-driven, so a single lock costs nothing noticeable. Every locked operation is async and waits with `WaitAsync`, because a sync wait on the UI thread would deadlock against a holder whose continuation needs that thread. Public operations take the lock and private cores do not, so creating a project from an existing file calls the save core without deadlocking. Rejected: documenting "call one at a time", which the UI cannot guarantee across awaits.
- **Dispose a removed data source after the tree raises `Removed`.** Editors on the data file close on `Removed` (UI-PROJECT-TREE decision "Removal prompts belong to the editors"), and the graphics editors' shared clipboard drops element copies on it (UI-ARRANGING-045), so nothing reads the source after it is disposed, and the file is released at once.

## Non-goals

- Persisting per-file view state (codec, offset, palette, element layout) for standalone files; state worth keeping belongs in a project.
- Recent-list entries for standalone files (the UI skips them).
- Adding a standalone file to an existing project; converting it to a new project covers the common case.
- Checking full path length or data file locations on disk; an over-long path fails with the OS's message.

## Open items

- Saving a single resource on a standalone root would write XML over the data file; no guard exists (unreachable from today's UI).
- LIB-PROJECT-SERVICE-056: two resources outside every open tree count as the same project.
- Save-as into a subdirectory of the old project's base directory leaves files the old project will load on reopen; foreign XML under a project directory is LIB-PROJECT-FORMAT's.
- `FindStaleKeyResources` is private and never called.
- Manual: rename, move and delete in the project tree with editors open; relink after renaming a ROM and reopening; Save Project As... to a new folder, then close and reopen from there.
