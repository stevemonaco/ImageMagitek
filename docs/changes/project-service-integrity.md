# Project operations never lose or corrupt data

## Why

ARCHITECTURE §1 says a 1.0 user never loses work and never corrupts a ROM. The project operations in `ProjectService` and the write-ahead log break that in several places. All claims below were confirmed in the code; none was already fixed.

- **1.0** [LIB-PROJECT-SERVICE] Save As is broken. `ResourceFileLocator` resolves every path from `ProjectNode.BaseDirectory` and the root name, both fixed at creation, while the writer's `_baseDirectory` (data file relative paths, journal directory) follows the new path. Saving to a new location writes nothing there, because every resource's location still matches its old disk location. Any resource that did change is written into the old directory with data file paths made relative to the new one, which breaks the old project. `DiskLocation` is then repointed at a project file that does not exist.
- **1.0** [LIB-PROJECT-SERVICE] In `PreviewResourceDeletion`, the arranger check `All(x => removedDict.ContainsKey(linkedResource) || x.Source is null)` tests the loop variable, which is always in the dictionary. An arranger with one element on a removed data file is therefore deleted whole.
- **1.0** [LIB-PROJECT-SERVICE] `ApplyResourceDeletion` with `LostPalette` gives the default palette to every indexed element, including elements whose palettes survive.
- **1.0** [LIB-PROJECT-FORMAT] WAL recovery breaks after a crash between `File.Move` and the journal rewrite. The operation is still pending but has no staging file. Roll-forward stops, rollback touches only completed operations, so the target keeps its new content and its `.bak` is orphaned. A failed commit (for example a read-only target) also leaves the pending operation's `.bak`. `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove` already has to clean up after this.
- **1.0** [LIB-PALETTES] `Palette.SavePalette` writes file colors through `ColorSourceSerializer.StoreColors` and never flushes. The bytes sit in the `FileStream` buffer until something else flushes or the source is disposed, so a crash after "Save" loses the palette. LIB-PALETTES-025 currently specifies "shall not flush", and the `DataSource.NotifyDataWritten` doc comment says the opposite.
- (promoted) [LIB-PROJECT-SERVICE] `CreateNewProject` checks for duplicates by comparing tree names to file paths, so the check never matches; `CreateNewProjectWithExistingFileAsync` has the same check. It overwrites an existing project file, which loses work, and throws `DirectoryNotFoundException` instead of returning a result.
- (promoted, undecided) [LIB-PROJECT-SERVICE] Add, delete-apply rewrites and create-project use plain `File.WriteAllText`. A crash mid-write leaves a truncated XML file, and the reader stops at the first file that fails validation (LIB-PROJECT-FORMAT-026), so the project will not open.
- (promoted) [LIB-PROJECT-SERVICE] A deleted data file node's `FileDataSource` is never disposed. The ROM stays locked (`FileShare.Read`) until the project closes, so the user cannot move, replace or patch it.
- (promoted) [UI-PROJECT-TREE] `ResourceRemovalChangesViewModel` adds changed-but-kept resources to `RemovedResources`, so the "Changed Items" section never shows. The delete confirmation lists arrangers that survive as if they will be destroyed, so the user cannot judge what a delete costs.
- (promoted, undecided) [LIB-PROJECT-SERVICE] Concurrent saves are not serialized. Each call makes a new `XmlProjectWriter` with its own lock, and every transaction in a directory uses the same `_transaction.json`, `<target>.tmp` and `<target>.bak` paths. Two overlapping writes, such as an editor save while a rename's project write is in progress, clobber each other's journal and staging files.
- Untested [LIB-PROJECT-SERVICE]: Save As; the delete cascades (lost-palette fallback, an arranger on a removed data file, the data file itself kept); rename rollback when the project write fails for a non-folder resource; `CreateNewProject` failures.

## What

**Save As** makes a full copy of the project's XML at the new location and switches the open project to it. Every resource file is written under the new project file's directory. Folders become directories there. Data file `location`s are rewritten relative to the new directory, and the data files themselves stay where they are. The project is renamed after the new file name and its `root` is cleared. Everything is written in one transaction whose journal is in the new directory. The old project's files are never touched. Save As refuses a target whose project file exists or whose directory tree already holds any `*.xml`. If the write fails, the root's base directory, name and every disk location are restored, and directories it created are removed when empty.

**Deletion** removes an arranger only when every element reads a removed data file. Otherwise the arranger loses only those elements. Only elements whose palette was removed get the fallback palette. The rewrites of changed resources are computed on models and committed in one transaction before anything changes in memory. If the commit fails, the tree, the resources and the disk are unchanged. After the commit, nodes are removed and their files deleted. A file that cannot be deleted is reported the way kept folders are. A removed data file's source is disposed after its node is removed, so the file is released at once.

**Create, add and create-folder** go through the same transaction. Creating an empty project fails with a result when the file exists, a tree with that path is open, the directory is missing, or the directory already holds `*.xml` files. Creating from an existing file uses the same open-tree check.

**Serialization.** Every operation that writes project XML or directories holds one service-wide async lock for its whole run, including rollback. Operations that take the lock become async: `CreateNewProjectAsync`, `AddResourceAsync`, `CreateNewFolderAsync`, `ApplyResourceDeletionAsync`. `IProjectService` and the seven `ProjectTreeViewModel` call sites change with them.

**WAL.** Recovery counts a pending operation with no staging file but an existing target as completed. Rollback restores every replaced target from its backup and deletes the other backups. A failed commit leaves no `.bak` files.

**Palettes.** `SavePalette` flushes the data source when it wrote any file color. It still does not raise `DataWritten`.

**Delete dialog.** Removed resources are listed under "Removed Items". Kept resources that lose elements or a palette are listed under "Changed Items", each with what it loses.

Unchanged: binary pixel writes stay outside the WAL (backlog LIB-IMAGES). The rename and move flows and the read path are unchanged. `ScatteredArranger.CloneArranger` codec sharing is a separate 1.0 item (LIB-ARRANGERS). So is foreign XML under a project directory (LIB-PROJECT-FORMAT). Save As does not touch the recent list.

## Decisions

- **Save As copies the project's XML and switches to the copy.** A project is a directory of files, so "save as" can only mean writing all of them somewhere else. Setting `ProjectNode.BaseDirectory` (it gains a setter) and the root name before the write makes every node's located path differ from its disk location, so the existing writer writes every resource without a new code path. Rejected: removing Save As for 1.0, because copying a project folder by hand loses the relative data file paths when the folder moves. Rejected: keeping the old project open and opening the copy beside it, because open editors would stay bound to the old project's resources.
- **Data files are referenced, not copied.** ROMs and disc images can be hundreds of MB, and a project is metadata over the ROM being hacked. Graphics edits after Save As keep going to the same file. See Open questions.
- **Refuse a target directory that holds `*.xml`.** The reader loads every `*.xml` under the base directory (LIB-PROJECT-FORMAT-005), so writing into such a directory merges the copy with whatever is there or makes it fail to load. The same guard applies to New Empty Project. Rejected: requiring an empty directory, because users keep ROMs beside projects. Not applied to New Project from Existing File: the UI derives that path beside the ROM and offers no other location, and the guard would block the flow. The existing-file check stays.
- **Deletion edits models, then commits, then mutates.** Changed arrangers' new contents come from their `ScatteredArrangerModel`: element models on removed data file keys are dropped, and removed palette keys are replaced by the fallback palette's name. That model is written in one transaction, and only after the commit are the live arrangers unlinked and re-paletted. A failed commit then leaves nothing half-applied. Rejected: mutate first and restore on failure, because restoring a codec's palette in place is not reversible while codecs may be shared (LIB-ARRANGERS). Rejected: mutating a cloned arranger, which depends on the same codec-sharing fix.
- **Resource file deletes happen after the commit, outside the WAL.** The WAL only writes files. A crash between the commit and the deletes leaves orphan resource files that nothing references; they reappear on reopen as harmless leftovers the user can delete again. Rejected: a delete operation in the journal, which is more format surface for a case that does no damage.
- **Single-file writes go through the WAL too.** Add and create each write one file. A transaction costs one staging file and one journal write, and it gives the atomic replace and recovery already in place. Rejected: a hand-written temp-then-rename for these paths, a second mechanism for the same guarantee.
- **One service-wide write lock.** Serializing per writer does nothing because writers are per call. Serializing per tree misses two projects in one directory, which share `_transaction.json`. Writes are short and user-driven, so a single lock costs nothing noticeable. Every locked operation is async and waits with `WaitAsync`, because a sync `Wait` on the UI thread would deadlock against a holder whose continuation needs that thread. Public operations take the lock and private cores do not, so `CreateNewProjectWithExistingFileAsync` calling the save core does not deadlock. Rejected: documenting "call one at a time", which the UI cannot guarantee across awaits.
- **Recovery treats a moved-but-unmarked operation as completed.** Every staging file exists before the journal is written, so a pending operation whose staging file is gone and whose target exists was moved. Rolling forward keeps the user's save, which matches recovery's existing roll-forward-first rule. Rollback restores only replaced targets, plus any target that is missing while its backup exists. It never restores an operation whose staging file is still present, because that backup may be a partial copy from a crash during `File.Copy`.
- **A palette save flushes and stays silent.** The flush makes Save mean "on disk". No `DataWritten`, because that event reloads graphics editors and clears their history (LIB-DATASOURCE decision "`DataWritten` is not raised from `Flush`"). Rejected: `Flush(true)` to the physical disk, which image saves do not do either.
- **Dispose a removed data source after the tree raises `Removed`.** Editors on the data file close on `Removed` (UI-PROJECT-TREE decision "Removal prompts belong to the editors"), so nothing reads the source after it is disposed.

## Spec changes

**LIB-PROJECT-SERVICE**

- Changed **-007**: When a new empty project is created, the service shall write the project file at the given path's full path in a write-ahead-log transaction, name the project after the file name without extension, and use the file's directory as the base directory.
- Added: If the project file already exists, an open tree has that disk location (full path, ignoring case), its directory does not exist, or the directory or any subdirectory holds a `*.xml` file, then creating an empty project shall fail without writing.
- Changed **-009**: If the project file already exists, an open tree has that disk location (full path, ignoring case), or the data file does not exist, then creating a project from an existing data file shall fail without writing.
- Changed **-023**: When save-as succeeds, every resource shall have been written under the new project file's directory with folders as directories, data file locations shall be relative to that directory, and the project shall be named after the new file name with no `root`. The root's disk location and base directory shall be the new ones. The old project's files shall be unchanged.
- Added: If the save-as target file exists, or its directory or any subdirectory holds a `*.xml` file, then save-as shall fail without writing.
- Added: If the save-as write fails, then the service shall restore the base directory, name and every node's disk location, remove the directories it created when empty, and return the failure; the old project's files shall be unchanged.
- Changed **-027**: When a data file, palette or scattered arranger is added under a node of an open project, the service shall write its resource file under the parent's directory in a write-ahead-log transaction, then attach the node and set its disk location and persisted model.
- Added: The service shall run create, save, save-as, save-resource, add, create-folder, rename, move and delete-apply one at a time; each shall wait for the previous one to finish, including its rollback.
- Changed **-046**: When a surviving element of a surviving arranger uses a removed palette, the plan shall mark the arranger as losing its palette.
- Changed **-047**: When every non-empty element of an arranger reads a removed data source, the plan shall mark it removed; when only some do, the plan shall mark it as losing elements.
- Changed **-048**: When a plan is applied, the service shall write the changed resources in one write-ahead-log transaction, dropping elements that read a removed data source and giving the fallback palette only to indexed elements whose palette was removed. Only after the commit shall it apply the same changes to the live resources and update their persisted models.
- Added: If the transaction for a plan's changed resources fails, then applying the plan shall fail with the tree, every resource and the disk unchanged.
- Changed **-049**: After the changed resources are committed, every removed non-folder node shall be removed from the tree and its resource file deleted, and the data source of every removed data file node shall be disposed. Then removed folders shall be removed deepest first and their directories deleted when empty.
- Added: If a removed resource's file cannot be deleted, then its node shall still be removed and the result shall fail listing the file.
- **-022**, **-024**, **-025**, **-037**, **-051**: wording unchanged; `Tests:` lines filled.
- Edge cases: strike "Creating a project over an existing project file overwrites it…", "Creating a project in a directory that does not exist throws…" and "A removed data file's source is not disposed by delete…". Replace "An arranger that ends up with no readable element after a delete is removed…" with "An arranger whose elements all read removed data files is removed rather than left empty."
- Threading and lifetime: replace "A new writer is created per call, so its write lock does not serialize separate calls" with "Disk-writing operations are serialized by one service-wide async lock; the set of open trees is still unsynchronized."
- Open items: remove the save-as item, the -047 and -048 items, and "Add, delete-apply rewrites and create-project write with plain file writes, outside the write-ahead log."
- Decisions: add the Decisions above that govern the service (Save As, data files referenced, the `*.xml` guard, model-first deletion, post-commit deletes, single-file WAL, the write lock, disposal after `Removed`).

**LIB-PROJECT-FORMAT**

- Changed **-041**: If replacing a target fails, then the transaction shall restore replaced targets in reverse order from their backups, delete targets that did not exist before, delete the backups of operations whose target was not replaced, delete the journal and fail listing any rollback errors.
- Added: When recovering a pending operation whose staging file is missing and whose target exists, recovery shall count it as completed.
- Changed **-045**: If a pending operation's staging file and target are both missing, then recovery shall restore from its backup every operation counted as completed and every operation whose target is missing, delete targets that did not exist before, delete the remaining backups, the staging files and the journal, and succeed unless rollback reported errors.
- **-047**: `Tests:` filled by the Save As test (journal in the new directory).
- Edge cases: strike the two WAL bullets ("A crash after replacing a target…" and "A failed replace after its backup was made…").
- Threading and lifetime: replace the per-writer lock bullet with "Writes are serialized by the caller (LIB-PROJECT-SERVICE); the writer's own lock covers only a single writer."
- Decisions: add "Recovery treats a moved-but-unmarked operation as completed."

**LIB-PALETTES**

- Changed **-025**: When a palette save writes any file color, the palette shall flush its data source before returning; saving shall not raise `DataWritten`, and project sources reach disk only when the caller saves the project.
- Open items: remove "`SavePalette` never flushes…". Decisions: add "A palette save flushes and stays silent."

**LIB-DATASOURCE**

- No requirement changes. Open items: remove the stale `NotifyDataWritten` doc comment item (the comment becomes true).

**UI-PROJECT-TREE**

- Changed **-120**: Remove shall preview the deletion and show a "Resource Removal Changes" dialog stating "'<name>' will be permanently removed", listing removed resources under "Removed Items" and kept resources that lose elements or a palette under "Changed Items" with what each loses, all by path with type icons, with a Remove button.
- Changed **-057**: Save Project As... shall ask for a new XML file name, save a copy of the project there and switch the open project to it, renaming its root; on failure the tree shall alert "Project Save Error".
- Open items: remove the `ResourceRemovalChangesViewModel` item.

## Tasks

1. **WAL fixes.** Recovery counts a moved-but-unmarked operation as completed, rollback restores only replaced or missing targets, and a failed commit deletes untouched backups. Tests in `WriteAheadLogTransactionTests`:
   - `RecoverAsync_PendingMovedButUnmarked_CountsAsCompleted`: journal pending, staging gone, target holds new content, `.bak` present. Pins: new content kept, no `.bak`, no journal.
   - `RecoverAsync_PendingStagingPresentAndMovedOp_RollsForwardBoth`.
   - `ExecuteAsync_ReplaceFails_LeavesNoBackupFiles`: a read-only target. Pins: original content, no `.bak`/`.tmp`/journal.
   - `RecoverAsync_PendingOperationWithoutStagingFile_RollsBack` stays as is (target missing, so still a rollback) and also asserts no `.bak` remains.
   - Remove the `.bak` cleanup workaround from `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove` and assert no `.bak` remains.
2. **Palette flush.** `SavePalette` flushes when it wrote a file color; fix the `NotifyDataWritten` doc comment. Test: `PaletteTests.SavePalette_FileColor_IsOnDiskWhenSaveReturns`. Read the file with `FileShare.ReadWrite`, because the source holds a write handle. Also `PaletteTests.SavePalette_DoesNotRaiseDataWritten`.
3. **Write lock and async signatures.** A service-wide `SemaphoreSlim` around every disk-writing operation, with private unlocked cores. Rename `CreateNewProject`, `AddResource`, `CreateNewFolder` and `ApplyResourceDeletion` to `…Async` in `IProjectService`, `ProjectService`, `ProjectTreeViewModel` and the test helpers. Test: `ProjectServiceTests.ConcurrentSaveAndSaveResource_BothSucceed_NoLeftoverFiles`. Start both without awaiting, then assert both succeed, both files hold the new content, and no journal, `.tmp` or `.bak` remains.
4. **Create and add through the WAL.** Create via a transaction, the shared full-path open-tree check, the `*.xml` and missing-directory guards. Add sets the unattached node's disk location, writes through `WriteResourceAsync(node, true)`, then attaches. Tests in `ProjectServiceTests`:
   - `CreateNewProject_ExistingFile_FailsAndKeepsFile`
   - `CreateNewProject_MissingDirectory_Fails`
   - `CreateNewProject_DirectoryHoldsXml_Fails`
   - `CreateNewProject_PathOfOpenProject_Fails`
   - `CreateNewProjectWithExistingFile_ProjectFileExists_Fails`
   - `CreateNewProjectWithExistingFile_DataFileMissing_Fails`
   - `CreateNewProjectWithExistingFile_Succeeds_RaisesProjectOpenedAndLoads`
   - `AddResource_WritesFileWithoutLeftovers`
5. **Deletion cascade.** Fix the preview checks (-046, -047). Apply builds the edited models, commits them in one transaction, then unlinks and re-palettes the live arrangers, removes nodes, deletes files (collecting failures) and disposes removed data sources. Tests in `ProjectServiceTests`:
   - `DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements`
   - `DeleteDataFile_ArrangerOnlyOnIt_IsRemoved`
   - `DeletePalette_OnlyElementsOnItGetFallback`, with an arranger on two palettes
   - `DeletePalette_ArrangerFileRewrittenWithFallbackKey`, which reopens the project and checks the keys
   - `DeleteDataFile_KeepsDataFileOnDisk`
   - `DeleteDataFile_ReleasesFileHandle`, which deletes the ROM right after
   - `ApplyDeletion_TransactionFails_LeavesTreeResourcesAndDiskUnchanged`, using a read-only referencing file
   - `ApplyDeletion_ResourceFileLocked_RemovesNodeAndReportsFile`
6. **Save As.** Guards; set the base directory and root name; create folder directories; write through the normal project write; clear `root`; roll back on failure. Tests in `ProjectServiceTests`:
   - `SaveProjectAs_NewDirectory_ProducesProjectThatLoads`. Reopen it with a fresh service and compare the tree, the palette colors, the arranger elements and the resolved data file path. Assert the old directory's files are unchanged and the journal was written in the new directory (LIB-PROJECT-FORMAT-047).
   - `SaveProjectAs_ThenSave_WritesToNewLocation`
   - `SaveProjectAs_TargetDirectoryHoldsXml_FailsWithoutWriting`
   - `SaveProjectAs_WriteFails_RestoresLocationsAndName`
7. **Rename rollback test.** `ProjectServiceTests.RenameFile_ProjectWriteFails_RestoresNameAndKeepsOldFile` (-037): a read-only referencing palette makes the write fail.
8. **Delete dialog.** `ResourceRemovalChangesViewModel` sends kept resources to `ChangedResources` with a "loses elements" or "uses default palette" note. Update the header text and the Changed Items template. Remove the single-argument constructor and the commented-out members (backlog UI-PROJECT-TREE dead code). Test: `ResourceRemovalChangesViewModelTests.KeptResources_ListedOnlyUnderChanged`. Manual: delete a data file used by a palette and a two-source arranger in the running app; the dialog shows the palette under Removed and the arranger under Changed. Save Project As... on the FF2 project to a new folder, then close and reopen it from there.
9. **Close.** Update LIB-PROJECT-SERVICE, LIB-PROJECT-FORMAT, LIB-PALETTES, LIB-DATASOURCE and UI-PROJECT-TREE as above, with `Tests:` lines and Decisions. Delete from `docs/BACKLOG.md` the lines resolved here:
   - the three **1.0** LIB-PROJECT-SERVICE likely bugs, the **1.0** WAL item and the **1.0** `SavePalette` item
   - the `FileDataSource` disposal and `CreateNewProject` lines
   - the `ResourceRemovalChangesViewModel` line
   - the undecided plain-`File.WriteAllText` and concurrent-saves lines
   - the LIB-DATASOURCE stale-comment line
   - from the LIB-PROJECT-SERVICE untested line: Save As, the delete cascades and the rename rollback; leave its other entries
   - from the UI-PROJECT-TREE dead-code line: the `ResourceRemovalChangesViewModel` parts

   Delete this proposal.

## Open questions

- Save As keeps referencing the original data files, with paths rewritten relative to the new location, instead of copying them into the new folder. Recommendation: reference them. Copying duplicates large ROMs and silently splits future graphics edits from the ROM the user is working on. If a ROM lives inside the old project folder, the copy points back into it, and that is visible in the data file's location.
