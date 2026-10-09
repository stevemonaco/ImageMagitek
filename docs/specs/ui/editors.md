---
id: UI-EDITORS
title: Editor hosting
project: TileShop.UI
sources:
  - TileShop.UI/Features/Shell/EditorsViewModel.cs
  - TileShop.UI/Features/Shell/DockableEditorViewModel.cs
  - TileShop.UI/Features/Shell/DockableEditorView.axaml
  - TileShop.UI/Features/Shell/ToolViewModel.cs
  - TileShop.UI/Features/ResourceEditorBaseViewModel.cs
  - TileShop.UI/ViewExtenders/Docking/DockFactory.cs
types:
  - EditorsViewModel
  - DockableEditorViewModel
  - ResourceEditorBaseViewModel
  - ToolViewModel
  - UserSaveAction
  - PaletteColorAssignedMessage
tests:
  - EditorsViewModelTests
depends:
  - LIB-PROJECT-SERVICE
  - LIB-PROJECT-TREE
  - LIB-DATASOURCE
  - LIB-ARRANGERS
  - UI-SHELL
  - UI-GRAPHICS-EDITOR
  - UI-PALETTE-EDITOR
---

# Editor hosting

## Purpose

`EditorsViewModel` owns the set of open editors: which editor a resource opens in, the active editor, save and discard prompts on close, removal and exit, and keeping open editors in step with the project tree and with content changes raised by the domain. The editors themselves are UI-GRAPHICS-EDITOR and UI-PALETTE-EDITOR; the tabs and menus that show them are UI-SHELL; the tree commands that call in are UI-PROJECT-TREE.

## Requirements

### Opening editors

- **UI-EDITORS-001** — When a palette is activated, the editors shall open it in a palette editor.
  - Tests: manual — double-click a palette node.
- **UI-EDITORS-002** — When a scattered or sequential arranger is activated, the editors shall open it in a graphics editor.
  - Tests: manual — double-click an arranger node.
- **UI-EDITORS-003** — When a resource that already has an open editor is activated, the editors shall make that editor active instead of opening another.
  - Tests: manual — double-click the same arranger node twice and count tabs.
- **UI-EDITORS-004** — When a data file is activated, the editors shall open a new graphics editor on a new sequential arranger over that file every time, so several sections of one file can be open at once.
  - Tests: manual — double-click a data file node twice and check two tabs open.
- **UI-EDITORS-005** — The sequential arranger for a data file shall use the codec associated with the file's lower-cased extension in `ExtensionCodecAssociations`, else the `default` association, else "NES 1bpp".
  - Tests: untested
- **UI-EDITORS-006** (inherited) — A sequential arranger for a tiled codec shall start 8 elements wide and 16 high; for a single-layout codec, 1 by 1.
  - Tests: untested
- **UI-EDITORS-007** — If the chosen codec cannot be created, then the editors shall alert "Codec Error" naming the codec and open nothing.
  - Tests: untested
- **UI-EDITORS-008** — The sequential editor opened from a data file shall record that data file as its originating project resource.
  - Tests: untested
- **UI-EDITORS-009** — When a folder or project root is activated, the editors shall open nothing.
  - Tests: untested
- **UI-EDITORS-010** — If a resource reads from a missing data file, directly, through an element's source, or through a referenced palette's source, then the editors shall refuse to open it and alert "Missing Data File" naming the file and telling the user to use Relink... in the project tree.
  - Tests: manual — rename a project's ROM, reopen the project, double-click an arranger on it.
- **UI-EDITORS-011** — The missing-file alert shall name the file's location when the activated resource is the data file itself, and name the resource and the file otherwise.
  - Tests: untested
- **UI-EDITORS-012** — When an editor is opened, its tab shall become the active, focused document.
  - Tests: manual — open an arranger and check its tab is selected and focused.

### Active editor

- **UI-EDITORS-020** — When the active editor changes, the editors shall scope window hotkeys to its hotkeys, or clear them when there is none (UI-SHELL).
  - Tests: untested
- **UI-EDITORS-021** — When the active editor is changed in code, the matching document tab shall become active and focused.
  - Tests: manual — close one of several tabs from File → Close <name> and check which tab is shown.

### Closing and saving

- **UI-EDITORS-030** — When a modified editor is closed, the editors shall prompt "Save changes" with Yes, No and Cancel, naming the editor.
  - Tests: manual — modify an arranger and close its tab.
- **UI-EDITORS-031** — When the user chooses Yes, the editors shall save the editor, then save its containing project; if the editor is still modified after saving, then the close shall be cancelled. The containing project shall be saved once per close.
  - Tests: `EditorsViewModelTests.CloseEditor_Yes_SavesProjectOnce`
- **UI-EDITORS-032** — When the user chooses No, the editors shall discard the editor's changes and close it.
  - Tests: manual — modify, close, choose No, reopen and check the change is gone.
- **UI-EDITORS-033** — When the user chooses Cancel, the editor shall stay open and unchanged.
  - Tests: manual — modify, close, choose Cancel.
- **UI-EDITORS-034** — When an unmodified editor is closed, it shall close without a prompt.
  - Tests: manual — open and close an arranger.
- **UI-EDITORS-035** — If saving the containing project fails, then the editors shall alert with the project location and the reason and still close the editor.
  - Tests: untested
- **UI-EDITORS-036** — When an editor reads from a sequential arranger or standalone file with no containing project, saving it shall skip the project save without failing.
  - Tests: manual — save a sequential editor in Draw and in Arrange mode, for a standalone file and a project DataFile node.
- **UI-EDITORS-037** — When the active editor is closed by any path (tab close button, File → Close <name>, a removal or a project close), the first remaining editor shall become active, or none when no editor remains; closing an inactive editor shall not change the active editor.
  - Tests: `EditorsViewModelTests.CloseEditor_Active_ActivatesFirstRemaining`, `EditorsViewModelTests.CloseEditor_Last_LeavesNoActiveEditor`, `EditorsViewModelTests.CloseEditor_Inactive_KeepsActiveEditor`; DevTools — open two arrangers, close each with its tab X, check File → Close <name> names the remaining editor and is hidden after the last close.
- **UI-EDITORS-038** — When the app asks to save all, the editors shall prompt for each modified editor in tab order, stop at the first Cancel, and save each containing project of a saved editor once, after the prompts.
  - Tests: `EditorsViewModelTests.RequestSaveAllUserChanges_TwoEditorsInOneProject_SavesProjectOnce`, `EditorsViewModelTests.RequestSaveAllUserChanges_Cancel_SavesNothing`
- **UI-EDITORS-039** — If an unexpected error occurs while saving all, then the editors shall alert "Error" with the message, log it, and report the exit as cancelled.
  - Tests: untested

### Following the project tree

- **UI-EDITORS-040** — When a resource is renamed, every editor whose resource or originating resource is that resource shall show the new name in its tab.
  - Tests: `EditorsViewModelTests.Rename_DataFile_RetitlesEditorOpenedFromIt`; manual — rename an arranger with its tab open.
- **UI-EDITORS-041** — When a palette is renamed, every graphics editor listing that palette shall show the new name in its palette list.
  - Tests: manual — rename a palette while an arranger using it is open.
- **UI-EDITORS-042** — When a node is removed from a tree, the editors shall close, without prompting, every editor whose resource or originating resource is that node's resource or a descendant's, and make the first remaining editor active if the active one closed.
  - Tests: manual — delete a folder with editors of its arrangers open.
- **UI-EDITORS-043** — Before a deletion is applied, the editors shall prompt Yes/No/Cancel for each modified editor whose resource or originating resource the deletion removes or changes, without saving the project, and abort the deletion on Cancel.
  - Tests: manual — modify an arranger, delete the data file it uses.
- **UI-EDITORS-044** — Before a deletion is applied, for each other modified scattered-arranger graphics editor whose working arranger uses a removed resource as an element source or palette, the editors shall prompt "Discard Changes" with Ok and Cancel, and abort the deletion on Cancel.
  - Tests: manual — modify an arranger without saving, delete a palette it uses.
- **UI-EDITORS-045** — When the user accepts every deletion prompt, the editors shall discard the changes of those dependent editors and close the editors of resources the deletion changes but keeps.
  - Tests: manual — delete a palette used by an open, unmodified arranger, and check its tab closes.

### Following content changes

- **UI-EDITORS-050** — When a palette in an open tree changes, every graphics editor listing it shall refresh its palette swatches, and every graphics editor whose arranger uses it shall repaint, without re-decoding pixels, so pending pixel edits are kept.
  - Tests: manual — edit a color in the palette editor and watch an open arranger.
- **UI-EDITORS-051** — When data is written to a data source in an open tree, every unmodified graphics editor reading from it shall reload from the source; modified editors shall be left alone.
  - Tests: manual — open two editors on one file, save a pixel in one, check the other updates.
- **UI-EDITORS-052** — Content changes raised in a burst shall be applied once per resource, after the burst, on the UI thread.
  - Tests: untested
- **UI-EDITORS-053** — When a color is assigned to a palette from outside the palette editor, the editors shall open that palette's editor if needed and apply the color through it, so the palette has one modified state.
  - Tests: manual — edit a color through the graphics editor's color flyout and check the palette tab shows `*`.
- **UI-EDITORS-054** — If routing an assigned color fails, then the editors shall log it and alert "Palette Error" with the message.
  - Tests: untested

### Projects closing

- **UI-EDITORS-060** — When a project or standalone file is closed, every editor whose resource or originating resource is in that tree shall be prompted for and closed (UI-PROJECT-TREE), including sequential editors opened from its data files.
  - Tests: manual — open a sequential editor on a project data file, Close All Projects, check the tab closes.
- **UI-EDITORS-061** — When an editor is removed from `Editors`, its document tab shall be removed wherever it is docked, including a floating window.
  - Tests: manual — float a tab, File → Close <name>; float a tab, delete its resource in the tree.

## Invariants

- At most one editor per resource exists in `Editors`, except data files, which may have any number of sequential editors (each with its own arranger as `Resource`).
- `ActiveEditor` is either null or an editor previously added to `Editors`, and is null whenever `Editors` is empty.
- Every editor in `Editors` has a document tab, and removing it from `Editors` removes the tab.

## Edge cases

- A resource of an unsupported type throws `NotSupportedException` from `OpenEditor`; only palettes, arrangers, file data sources, folders and projects reach it from the tree.
- A non-file data source (memory) is not handled by the missing-file gate and has no open case; it throws as unsupported.
- A deletion prompt's "Yes" saves the editor even though the resource is about to be removed.
- The save prompt always says the editor "will be closed", including during removal and exit.

## Threading and lifetime

- `EditorsViewModel` is a DI singleton for the app's lifetime. It subscribes once to `ProjectService.TreeChanged` and `ResourceChanged` and never unsubscribes, so closed editors are not kept alive by those subscriptions.
- `ResourceChanged` may arrive off the UI thread; it is re-posted to the UI dispatcher, then coalesced in a reference-keyed set flushed by one posted callback.
- `PaletteColorAssignedMessage` is received through the default Messenger; the handler is `async void` and catches its own exceptions.
- Closing a tab from its X runs `CloseEditor` synchronously inside a nested dispatcher loop (`Dispatcher.UIThread.MainLoop`) because Dock's `OnClose` is synchronous. While it runs the dockable is marked as closing, so the dock factory leaves its removal to Dock.

## Decisions

- **Editors follow the tree.** `EditorsViewModel` subscribes to `ProjectService.TreeChanged`: Renamed updates tab titles and palette names, Removed closes matching editors. The save and discard prompts run before a removal is applied, in `ConfirmRemovalAsync(plan)`, so the tree VM no longer manages editor tabs. `ResourceRenamedMessage` is gone. Reason: the domain raises events and view models are projections (ARCHITECTURE §4).
- **Content changes come from the domain, not from the editor that made them.** Palettes raise `Changed`, image saves call `DataSource.NotifyDataWritten`, `ProjectTree` forwards both as `ResourceChanged`, and `ProjectService` forwards from every open tree. `EditorsViewModel` is the only consumer and subscribes directly. Reason: any new write path would otherwise have to remember to announce itself. Rejected: a UI-side `DomainEventBridge` republishing domain events as Messenger messages, built and removed because it had one consumer; `PaletteChangedMessage` and `ArrangerChangedMessage` are gone with nothing replacing them.
- **Refreshes never wipe unsaved work.** Data-source reloads skip modified graphics editors, and palette changes re-map colors without re-decoding pixels.
- **Each data-file open is a new editor.** A sequential editor's resource is its own new arranger, so users can view several sections of one file at once. The editor keeps the data file as `OriginatingProjectResource`, which is how close, removal and missing-file checks find it.
- **Missing sources are refused, not opened broken.** The gate keys on `FileDataSource.IsMissing` and covers the file itself, element sources and referenced palettes' sources, so standalone files reuse it.
- **Project saves after editor saves are skipped when there is no project.** Call sites use the non-throwing `FindContainingProject`, because a sequential arranger is never in a tree.
- **One modified state per palette.** A color edited elsewhere is routed into the palette's editor, opening it if needed.
- **One removal path for editors.** `DockableEditorViewModel.OnClose` calls `EditorsViewModel.CloseEditor`, which removes through the same `RemoveEditors` rule as removals: only removing the active editor changes `ActiveEditor`, to the first remaining editor or null. Reason: one place owns `ActiveEditor`; the tab X used to remove the editor directly and leave it stale. Rejected: having `ShellView` set `ActiveEditor` from Dock's focus events after a close (Dock may raise none when the last tab closes); activating the neighbouring tab (needs Dock's tab order, which drag reordering changes).
- **Project saves once.** The prompt path keeps its project save (UI-EDITORS-031); `CloseEditor` no longer saves a second time; save-all prompts without the per-editor project save and saves each collected project once afterwards, as Save All does. Reason: identical behavior on every close path and one write-ahead log transaction per project. Rejected: dropping the project save after an editor save entirely (other pending tree state may rely on it; not verified).
- **Dockables are removed through the factory.** When an editor leaves `Editors`, the dock factory finds its dockable in the main layout or any floating window and removes it with `RemoveDockable`. Reason: a floated tab lives in another dock and used to stay open bound to a closed editor. Rejected: blocking floating (UI-SHELL-005 allows it).
- **Retitle sequential editors on data file rename.** The editor's display name follows the data file's new name; the sequential arranger's own name is not persisted, so it is left alone.

## Non-goals

- Persisting which editors were open across sessions.
- Persisting a sequential editor's codec, offset or layout (backlog: sequential arrangers as project resources).

## Open items

- `Receive(PaletteColorAssignedMessage)` is documented as opening the palette editor "in the background", but adding an editor makes its tab active and focused (UI-EDITORS-012), so focus likely jumps to the palette tab. Needs a manual check.
- `EditorsViewModelTests` covers closing, saving and renaming; opening editors and content refreshes are untested. Still to verify by hand: rename, move and delete with editors open; palette edits live in graphics editors; saves and imports refreshing other unmodified editors.
