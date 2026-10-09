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

- **LIB-PROJECT-SERVICE-007** — When a new project is created, the service shall write the project file at the given path, name the project after the file name without extension, and use the file's directory as the base directory.
  - Tests: `ProjectServiceTests.CreateNewProject_RaisesProjectOpened`
- **LIB-PROJECT-SERVICE-008** — When a project is created from an existing data file, the service shall add a data file node named after the data file without extension at the root, save the project, and raise `ProjectOpened` only if the save succeeds.
  - Tests: untested
- **LIB-PROJECT-SERVICE-009** — If the project file already exists or the data file does not, then creating a project from an existing data file shall fail without writing.
  - Tests: untested
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
  - Tests: untested
- **LIB-PROJECT-SERVICE-023** — When save-as succeeds, the root's disk location shall become the full new path; resource files shall be written at the locations under the project's existing base directory.
  - Tests: untested
- **LIB-PROJECT-SERVICE-024** — When a single resource is saved, the service shall write only that resource's file, when its model changed or overwrite is forced, and shall not rewrite resources that reference it.
  - Tests: untested
- **LIB-PROJECT-SERVICE-025** — If a single resource has no disk location, then saving it shall fail.
  - Tests: untested
- **LIB-PROJECT-SERVICE-026** — While a palette node has a committed model, project and resource saves shall write the committed state, not the live palette's pending edits.
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`

### Add

- **LIB-PROJECT-SERVICE-027** — When a data file, palette or scattered arranger is added under a node of an open project, the service shall write its resource file under the parent's directory, attach the node, and set its disk location and persisted model.
  - Tests: untested
- **LIB-PROJECT-SERVICE-028** — If the parent is not in an open tree, already has a child of that name (case-sensitive), or cannot contain children, then adding shall fail without writing.
  - Tests: `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`
- **LIB-PROJECT-SERVICE-029** — If the resource is a folder, an in-memory data source, a sequential arranger or any other type the format cannot write, then adding shall fail.
  - Tests: untested
- **LIB-PROJECT-SERVICE-030** — When a folder is created, the service shall create its directory under the parent's directory, attach the node and set its disk location.
  - Tests: `ProjectServiceTests.RenameFolder_UpdatesNestedFolderLocations`
- **LIB-PROJECT-SERVICE-031** — If the parent already has a child of that name or cannot contain children, then creating a folder shall fail; no numbered alternative name is chosen.
  - Tests: untested

### Rename

- **LIB-PROJECT-SERVICE-032** — If the node is not in an open tree, or its parent already has a child with the new name (case-sensitive, including the node's own current name), then rename shall fail without changes.
  - Tests: untested
- **LIB-PROJECT-SERVICE-033** — When a folder is renamed, the service shall move its directory, update the disk locations of it and every nested folder, and save the project so every resource under it and every reference to them is rewritten.
  - Tests: `ProjectServiceTests.RenameFolder_MovesDirectoryAndChildResource`, `ProjectServiceTests.RenameFolder_UpdatesNestedFolderLocations`
- **LIB-PROJECT-SERVICE-034** — If moving the folder's directory fails, or the project save fails, then the service shall restore the name and directory and return a failure; the tree raises the reverse `Renamed`.
  - Tests: `ProjectServiceTests.RenameFolder_DirectoryMoveFails_RaisesReverseRename`
- **LIB-PROJECT-SERVICE-035** — When a resource with a file is renamed, the service shall save the project, which writes the resource under its new file name and rewrites resources that reference it, then delete the old file.
  - Tests: `ProjectServiceTests.RenameFile_LeavesOnlyNewResourceFile`
- **LIB-PROJECT-SERVICE-036** — When a rename changes only the letter case, the resource file shall end up with the new casing and no file with the old casing shall remain.
  - Tests: `ProjectServiceTests.RenameFile_CaseOnly_KeepsResourceFile`
- **LIB-PROJECT-SERVICE-037** — If the project save fails during a resource rename, then the service shall restore the name and keep the old file.
  - Tests: untested
- **LIB-PROJECT-SERVICE-038** — When a node without a disk location is renamed, the service shall rename it in memory only.
  - Tests: untested
- **LIB-PROJECT-SERVICE-039** — When the project root is renamed, the project file shall be written under the new name and the old project file deleted.
  - Tests: untested

### Move

- **LIB-PROJECT-SERVICE-040** — The move check shall fail when the nodes are in different trees, the node has no parent, the target is the node or its current parent, the target has a child of the same name, the target cannot contain children, the target folder is inside the moved folder, or the node has no disk location.
  - Tests: `ProjectServiceTests.MoveNode_NodeWithoutDiskLocation_FailsWithoutThrowing`, `ProjectServiceTests.ProjectOnlyOperations_StandaloneFile_FailWithoutWriting`
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
- **LIB-PROJECT-SERVICE-046** — When a surviving arranger uses a removed palette, the plan shall mark it as losing its palette.
  - Tests: untested
- **LIB-PROJECT-SERVICE-047** — When a surviving arranger has any element reading a removed data source, the plan shall mark it removed, whatever its other elements read.
  - Tests: untested
- **LIB-PROJECT-SERVICE-048** — When a plan is applied, every changed resource shall be unlinked from every removed resource and, if it lost a palette, every indexed element shall be given the fallback palette; then its file shall be rewritten and its persisted model updated.
  - Tests: untested
- **LIB-PROJECT-SERVICE-049** — When a plan is applied, every removed non-folder node shall be removed from the tree and its resource file deleted, then removed folders shall be removed deepest first and their directories deleted when empty.
  - Tests: `ProjectServiceTests.DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce`, `ProjectServiceTests.DeleteFolder_WithNestedFolder_RemovesBothDirectories`
- **LIB-PROJECT-SERVICE-050** — If a removed folder's directory still holds other files, then the directory shall be left on disk, the node still removed, and the result shall fail listing the kept directories.
  - Tests: `ProjectServiceTests.DeleteFolder_WithUnmanagedFile_KeepsDirectoryAndReportsIt`
- **LIB-PROJECT-SERVICE-051** — Deleting a data file node shall delete its resource file, never the data file itself.
  - Tests: untested

### Relink

- **LIB-PROJECT-SERVICE-052** — If the data source is not missing or the replacement file does not exist, then relink shall fail without copying.
  - Tests: `ProjectServiceTests.Relink_SourceNotMissing_Fails`
- **LIB-PROJECT-SERVICE-053** — When a missing data source is relinked, the service shall copy the chosen file to the source's expected location (creating its directory), leaving the chosen file in place and every project reference unchanged.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`
- **LIB-PROJECT-SERVICE-054** — After the copy, the service shall reopen the source, reload every palette in its project that reads from it, and raise `DataWritten` on it so the tree raises `ResourceChanged`.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`

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

- Creating a project over an existing project file overwrites it; the already-open check compares tree names to file paths and never matches.
- Creating a project in a directory that does not exist throws instead of failing.
- Opening a project by a different path string (relative, other casing) opens a second tree on the same files.
- An arranger that ends up with no readable element after a delete is removed, so "loses elements but survives" does not occur.
- Applying a stale plan after the tree changed is not detected.
- A removed data file's source is not disposed by delete; its file stays open until the project closes.
- If moving a file back during move rollback throws, the exception escapes.
- If deleting the old file after a successful rename throws, the exception escapes.
- Relink copies on the calling thread before its task completes.

## Threading and lifetime

- Not thread-safe: the set of open trees is unsynchronized; call from one thread.
- Events are raised synchronously on the thread that made the change; after an awaited write, rollback changes are raised on the awaiting context.
- The service holds open trees until closed; closing disposes their data sources and releases file handles.
- A new writer is created per call, so its write lock does not serialize separate calls.

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

## Non-goals

- Persisting per-file view state (codec, offset, palette, element layout) for standalone files; state worth keeping belongs in a project.
- Recent-list entries for standalone files (the UI skips them).
- Adding a standalone file to an existing project; converting it to a new project covers the common case.
- Validating resource names against file-system rules.

## Open items

- Save-as writes resources under the original base directory and the project file under its original name, then points the root at the new path, so saving to a new location writes no new project file (LIB-PROJECT-SERVICE-023). Untested; likely a bug.
- Saving a single resource on a standalone root would write XML over the data file; no guard exists (unreachable from today's UI).
- LIB-PROJECT-SERVICE-047: the "all elements removed" test compares the removed source with itself, so any reference removes the whole arranger.
- LIB-PROJECT-SERVICE-048: losing one palette resets every indexed element to the fallback palette, including elements on surviving palettes.
- LIB-PROJECT-SERVICE-031: the folder-name helper that picks "name (n)" is unreachable because the name check fails first; the doc comment says names are augmented.
- LIB-PROJECT-SERVICE-028/032: name checks are case-sensitive while Windows paths are not; adding `Data` beside `data` overwrites `data.xml`. No check rejects empty names, `/`, or invalid file-name characters.
- LIB-PROJECT-SERVICE-056: two resources outside every open tree count as the same project.
- Add, delete-apply rewrites and create-project write with plain file writes, outside the write-ahead log.
- `FindStaleKeyResources` is private and never called.
- Manual: rename, move and delete in the project tree with editors open; relink after renaming a ROM and reopening.
