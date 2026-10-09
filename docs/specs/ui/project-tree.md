---
id: UI-PROJECT-TREE
title: Project tree
project: TileShop.UI
sources:
  - TileShop.UI/Features/Project/ProjectTreeViewModel.cs
  - TileShop.UI/Features/Project/ProjectTreeView.axaml
  - TileShop.UI/Features/Project/ProjectTreeView.axaml.cs
  - TileShop.UI/Features/Project Nodes
  - TileShop.UI/Features/Dialogs/MoveNodeViewModel.cs
  - TileShop.UI/Features/Dialogs/MoveNodeView.axaml
  - TileShop.UI/Features/Dialogs/RenameNodeViewModel.cs
  - TileShop.UI/Features/Dialogs/RenameNodeView.axaml
  - TileShop.UI/Features/Dialogs/NameResourceViewModel.cs
  - TileShop.UI/Features/Dialogs/NameResourceView.axaml
  - TileShop.UI/Features/Dialogs/ResourceRemovalChangesViewModel.cs
  - TileShop.UI/Features/Dialogs/ResourceRemovalChangesView.axaml
  - TileShop.UI/Features/Dialogs/AddPaletteViewModel.cs
  - TileShop.UI/Features/Dialogs/AddPaletteView.axaml
  - TileShop.UI/Features/Dialogs/AddScatteredArrangerViewModel.cs
  - TileShop.UI/Features/Dialogs/AddScatteredArrangerView.axaml
  - TileShop.UI/ViewExtenders/Behaviors/TreeViewItemNodeClassBehavior.cs
  - TileShop.UI/ViewExtenders/DragDrop/PayloadDragBehavior.cs
  - TileShop.UI/ViewExtenders/DragDrop/PayloadDropBehavior.cs
  - TileShop.UI/ViewExtenders/DragDrop/DragPayloadStore.cs
  - TileShop.UI/ViewExtenders/DragDrop/TreeViewItemResourceNodeDropHandler.cs
  - TileShop.UI/ViewExtenders/Selectors/ResourceRemovalTemplateSelector.cs
  - TileShop.Shared/Models/ResourceChangeViewModel.cs
types:
  - ProjectTreeViewModel
  - ResourceNodeViewModel
  - ProjectNodeViewModel
  - FolderNodeViewModel
  - DataFileNodeViewModel
  - StandaloneFileNodeViewModel
  - ArrangerNodeViewModel
  - PaletteNodeViewModel
  - ResourceNodeComparer
  - ResourceNodeViewModelExtensions
  - TreeViewItemNodeClassBehavior
  - PayloadDragBehavior
  - PayloadDropBehavior
  - DragPayloadStore
  - TreeViewItemResourceNodeDropHandler
  - MoveNodeViewModel
  - MoveDestinationModel
  - RenameNodeViewModel
  - NameResourceViewModel
  - ResourceRemovalChangesViewModel
  - ResourceRemovalTemplateSelector
  - ResourceChangeViewModel
  - AddPaletteViewModel
  - AddScatteredArrangerViewModel
  - AddScatteredArrangerFromCopyMessage
tests:
  - ProjectServiceTests
depends:
  - LIB-PROJECT-TREE
  - LIB-PROJECT-SERVICE
  - LIB-DATASOURCE
  - LIB-PALETTES
  - LIB-ARRANGERS
  - UI-EDITORS
  - UI-SHELL
---

# Project tree

## Purpose

The tree pane shows every open project and standalone data file as a projection of the domain `ProjectTree` (LIB-PROJECT-TREE), and hosts the commands that change it: open, create, close, add, rename, move (menu and drag and drop), delete and relink, all through `ProjectService` (LIB-PROJECT-SERVICE). Activating a node opens its editor (UI-EDITORS). Export As.../Import From... on arranger nodes and "Import Image Into Selection" are UI-IMAGE-IO; "New Scattered Arranger from Selection" is started in UI-ARRANGING and received here.

## Requirements

### Projection

- **UI-PROJECT-TREE-001** — When a project is opened, the tree shall add a root showing the project's folders, data files, palettes and arrangers, with the root expanded.
  - Tests: `ProjectServiceTests.ProjectNodeViewModel_ExistingTree_MatchesSorted`
- **UI-PROJECT-TREE-002** — Within each parent, folders shall come first, then all other nodes, each group ordered by name with culture-sensitive string comparison.
  - Tests: `ProjectServiceTests.ProjectNodeViewModel_ExistingTree_MatchesSorted`
- **UI-PROJECT-TREE-003** — When a node is added, removed, moved or renamed in an open project by any code path, the tree shall show the change, keeping the sorted order.
  - Tests: `ProjectServiceTests.ServiceOperations_KeepViewModelTreeInSync`
- **UI-PROJECT-TREE-004** — When a node is moved, the tree shall keep that node's existing item, including its expansion state, under the new parent.
  - Tests: `ProjectServiceTests.ServiceOperations_KeepViewModelTreeInSync`
- **UI-PROJECT-TREE-005** — When a node is renamed, the tree shall move it to its new sorted position without losing its selection.
  - Tests: `ProjectServiceTests.ServiceOperations_KeepViewModelTreeInSync`
- **UI-PROJECT-TREE-006** — When a project or standalone file is closed, the tree shall remove its root.
  - Tests: untested
- **UI-PROJECT-TREE-007** — Roots shall stay in the order they were opened.
  - Tests: untested

### Roots and node display

- **UI-PROJECT-TREE-010** — When a standalone data file is opened, the tree shall add a root for it that has no children and no expander, with the data file icon.
  - Tests: `ProjectServiceTests.StandaloneFileNodeViewModel_FindsOnlyItsRoot`
- **UI-PROJECT-TREE-011** — Each node shall show its type's icon and its name; a folder shall show an open-folder icon while expanded.
  - Tests: manual — expand a folder in the FF2 project.
- **UI-PROJECT-TREE-012** — Each node item shall carry a style class for its node type (`project`, `folder`, `datafile`, `standalone`, `arranger`, `palette`), which selects its context menu.
  - Tests: manual — DevTools `search TreeViewItem` and check `Classes`.
- **UI-PROJECT-TREE-013** — While a data file is missing, its name shall show in the theme's danger color with the tooltip "File not found".
  - Tests: manual — rename a project's ROM, reopen the project, hover the data file.

### Selection and activation

- **UI-PROJECT-TREE-020** — When a project root or folder is double-clicked or Enter is pressed on it, the tree shall toggle its expansion.
  - Tests: manual — double-click and press Enter on a folder.
- **UI-PROJECT-TREE-021** — When any other node is double-clicked or Enter is pressed on it, the tree shall activate the selected node's resource in an editor (UI-EDITORS).
  - Tests: manual — select an arranger, press Enter (the CLAUDE.md recipe).
- **UI-PROJECT-TREE-022** — When a standalone root is double-clicked or Enter is pressed on it, the tree shall open a new sequential editor on the file each time.
  - Tests: manual — Open File... on a ROM, press Enter twice, count tabs.

### Open, create and close

- **UI-PROJECT-TREE-030** — Open Project... shall ask for an XML project file and open it.
  - Tests: manual — File → Open Project....
- **UI-PROJECT-TREE-031** — If a project fails to open, then the tree shall alert "Project Open Error" listing the number of errors and each reason.
  - Tests: manual — open a project that is already open.
- **UI-PROJECT-TREE-032** — When an opened project contains a data file that is open as a standalone file, the tree shall close that standalone file first (with its editors' save prompts); if the user cancels, then the project shall be closed again.
  - Tests: manual — Open File... a ROM, then open the project that contains it.
- **UI-PROJECT-TREE-033** — When an opened project references missing data files, the tree shall show one "Missing Data Files" alert listing each missing file's name and location and explaining Relink....
  - Tests: manual — rename a project's ROM and reopen the project.
- **UI-PROJECT-TREE-034** — Open File... shall ask for any file; if the file is already open as a standalone root or as a data file in an open project, then the tree shall expand that node's ancestors and select it instead of opening it again.
  - Tests: manual — Open File... a ROM twice; Open File... a ROM already in the FF2 project.
- **UI-PROJECT-TREE-035** — When Open File... opens a new standalone file, the tree shall select its root; if opening fails, then the tree shall alert "Open File Error" with the reason.
  - Tests: manual — Open File... a ROM.
- **UI-PROJECT-TREE-036** — New Empty Project... shall ask for a new XML file name and create an empty project there; on failure the tree shall alert "Project Error" (or "Failed" for an exception) with the reason.
  - Tests: manual — File → New Empty Project....
- **UI-PROJECT-TREE-037** — New Project from Existing File... shall create `<file name without extension>Project.xml` beside the chosen file, containing that file, and select the new project's root.
  - Tests: manual — File → New Project from Existing File... on a ROM.
- **UI-PROJECT-TREE-038** — When New Project from Existing File... picks a file open as a standalone root, the tree shall convert that standalone file as in UI-PROJECT-TREE-061.
  - Tests: manual — Open File... a ROM, then New Project from Existing File... on the same ROM.
- **UI-PROJECT-TREE-039** — When a root is closed, the tree shall save its project, prompt Yes/No/Cancel for each modified editor whose resource or originating resource is in the tree, close those editors, save the project again and close it; Cancel shall keep the root and every editor open.
  - Tests: manual — modify an arranger and a sequential editor in a project, Close Project, Cancel, then repeat with Yes.
- **UI-PROJECT-TREE-040** — If the save before closing fails, then the tree shall alert "Project Save Error" and keep the root open.
  - Tests: untested
- **UI-PROJECT-TREE-041** — When a root closes, the first remaining editor shall become active.
  - Tests: untested

### Context menus

- **UI-PROJECT-TREE-050** — A project root's context menu shall offer: Add New Folder..., Add Existing Data File..., Add New Scattered Arranger..., Add New Palette..., Save Project As..., Close Project, Rename Project..., Open in Folder.
  - Tests: manual — right-click the FF2 project root.
- **UI-PROJECT-TREE-051** — A folder's context menu shall offer: the four Add items, Remove, Rename..., Move to Folder..., Open in Folder.
  - Tests: manual — right-click a folder.
- **UI-PROJECT-TREE-052** — A data file's context menu shall offer: Relink... (only while the file is missing), Remove, Rename..., Move to Folder..., Open in Folder.
  - Tests: manual — right-click a present and a missing data file.
- **UI-PROJECT-TREE-053** — An arranger's context menu shall offer: Export As..., Import From... (UI-IMAGE-IO), Remove, Rename..., Move to Folder..., Open in Folder.
  - Tests: manual — right-click an arranger.
- **UI-PROJECT-TREE-054** — A palette's context menu shall offer: Remove, Rename..., Move to Folder..., Open in Folder.
  - Tests: manual — right-click a palette.
- **UI-PROJECT-TREE-055** — A standalone root's context menu shall offer: Create Project from File..., Close, Open in Folder.
  - Tests: manual — right-click a standalone root.
- **UI-PROJECT-TREE-056** — Open in Folder shall reveal the node's disk location (UI-SHELL-110), and do nothing for a node without one.
  - Tests: manual — Open in Folder on an arranger.
- **UI-PROJECT-TREE-057** — Save Project As... shall ask for a new XML file name and save the project there; on failure the tree shall alert "Project Save Error".
  - Tests: manual — Save Project As... on the FF2 project.

### Standalone conversion

- **UI-PROJECT-TREE-061** — Create Project from File... shall close the standalone root (with its editors' save prompts), create `<name>Project.xml` beside the file, and select the new project's data file node.
  - Tests: manual — Open File... a ROM, then Create Project from File....
- **UI-PROJECT-TREE-062** — If closing the standalone root is cancelled, then Create Project from File... shall do nothing.
  - Tests: manual — modify a standalone editor, Create Project from File..., Cancel.
- **UI-PROJECT-TREE-063** — If creating the project fails, then the tree shall alert the reason and reopen the file as a standalone root.
  - Tests: manual — leave a `<name>Project.xml` beside a ROM and convert it.

### Adding resources

- **UI-PROJECT-TREE-070** — Add New Folder... shall create a folder named "New Folder" under the node without asking for a name, and select it; on failure the tree shall alert "Folder Creation Error".
  - Tests: manual — Add New Folder... on the project root.
- **UI-PROJECT-TREE-071** — Add Existing Data File... shall ask for any file and add it under the node with the file's name, and select it.
  - Tests: manual — Add Existing Data File... on a folder.
- **UI-PROJECT-TREE-072** — If the node already has a child with the chosen file's name, then Add Existing Data File... shall alert "Error" and add nothing.
  - Tests: manual — add the same file twice to one folder.
- **UI-PROJECT-TREE-073** — When the chosen file is open as a standalone root, Add Existing Data File... shall close that root first (with its prompts), and add nothing if the user cancels.
  - Tests: manual — Open File... a ROM, then add it to a project.
- **UI-PROJECT-TREE-074** — If the project has no data files, then Add New Palette... shall alert "Project Error" and open no dialog.
  - Tests: manual — Add New Palette... in a new empty project.
- **UI-PROJECT-TREE-075** — The Add New Palette dialog shall ask for a name, a data source from the project's data files (first selected), a color model, a global palette to copy colors and color model from (or None), and Zero Index Transparent.
  - Tests: manual — open the dialog.
- **UI-PROJECT-TREE-076** — While a template palette is chosen, the color model box shall be disabled and show the template's model.
  - Tests: manual — choose a template in the dialog.
- **UI-PROJECT-TREE-077** — While the palette name is empty, whitespace or already used by a sibling, the Add New Palette dialog shall list the validation error.
  - Tests: untested
- **UI-PROJECT-TREE-078** — When Add New Palette is accepted, the tree shall add a project-XML palette on the chosen source, starting from the template's colors or empty, select it, remember the color model and transparency choice, and open its editor.
  - Tests: manual — add a palette from a template.
- **UI-PROJECT-TREE-079** — The Add New Scattered Arranger dialog shall ask for a name, Indexed or Direct color, Tiled or Single layout, and for Tiled the element pixel size and arranger size in elements (showing the total pixel size), for Single the pixel size.
  - Tests: manual — open the dialog and switch layouts.
- **UI-PROJECT-TREE-080** — When Add New Scattered Arranger is accepted, the tree shall add an arranger of that shape (1 by 1 element for Single), select it, remember every choice, and open its editor.
  - Tests: manual — add a tiled arranger, reopen the dialog, check the values are kept.
- **UI-PROJECT-TREE-081** (inherited) — The Add New Scattered Arranger dialog shall default to Indexed, Tiled, 8x8-pixel elements, 16 by 8 elements, and 256x256 for Single; Add New Palette to RGBA32 with Zero Index Transparent on.
  - Tests: untested
- **UI-PROJECT-TREE-082** — If adding a palette or arranger fails, then the tree shall alert "Resource Error" with the reason.
  - Tests: manual — add an arranger with a name a sibling already has.
- **UI-PROJECT-TREE-083** — When a graphics editor asks for a new scattered arranger from a copied selection, the tree shall ask for a name, create an arranger the copy's size, copy the elements in, add it at the project root, select it and open its editor; any failure shall alert "Error".
  - Tests: manual — Arrange mode, select elements, "Add as New Scattered Arranger...".

### Rename

- **UI-PROJECT-TREE-090** — Rename... (and Rename Project...) shall open a dialog titled "Rename <name>" with the current name filled in and ✓ and x buttons.
  - Tests: manual — Rename... on an arranger.
- **UI-PROJECT-TREE-091** — When the rename is accepted, the tree shall rename the resource through the service; on failure it shall alert "Rename failed" with the reason.
  - Tests: manual — rename an arranger to a sibling's name.

### Move

- **UI-PROJECT-TREE-100** — Move to Folder... shall list the project root (by name) and every folder (by path) to which the node can be moved, and none other.
  - Tests: manual — Move to Folder... on a nested arranger.
- **UI-PROJECT-TREE-101** — If no destination is valid, then Move to Folder... shall alert "There are no folders that '<name>' can be moved to".
  - Tests: manual — Move to Folder... on a folder in a project with no other folders.
- **UI-PROJECT-TREE-102** — The move dialog's Move button shall be enabled only while a destination is selected.
  - Tests: manual — open the dialog and check Move before and after selecting.
- **UI-PROJECT-TREE-103** — When a move succeeds, the tree shall expand the destination and select the moved node; on failure it shall alert "Move failed" with the reason.
  - Tests: manual — move an arranger into a collapsed folder.

### Drag and drop

- **UI-PROJECT-TREE-110** — Dragging a node's header with the left button past a small threshold shall start a move drag of that node.
  - Tests: manual — drag an arranger header.
- **UI-PROJECT-TREE-111** (inherited) — The drag threshold shall be 3 px in each direction.
  - Tests: untested
- **UI-PROJECT-TREE-112** — While a dragged node is over a node it can be moved into, the target header shall highlight and the drop effect shall be Move; over any other target there shall be no highlight and no drop.
  - Tests: manual — drag an arranger over a folder, a palette and its own parent.
- **UI-PROJECT-TREE-113** — When a node is dropped on a valid target, the tree shall move it exactly as Move to Folder... does, and clear the highlight.
  - Tests: manual — drop an arranger onto a folder.
- **UI-PROJECT-TREE-114** — The highlight shall clear when the drag leaves the target.
  - Tests: manual — drag over a folder and away.

### Delete

- **UI-PROJECT-TREE-120** — Remove shall preview the deletion and show a "Resource Removal Changes" dialog stating "'<name>' will be permanently removed and all references will be reset to default" and listing the affected resources by path with type icons, with a Remove button.
  - Tests: manual — Remove on a data file that palettes and arrangers use.
- **UI-PROJECT-TREE-121** — If the deletion cannot be previewed, then Remove shall alert "Delete" with the reason.
  - Tests: untested
- **UI-PROJECT-TREE-122** — When the removal dialog is accepted, the tree shall run the editors' removal prompts (UI-EDITORS-043, -044), then apply the deletion, resetting lost palettes to the default palette; Cancel at any step shall leave the project unchanged.
  - Tests: manual — modify an arranger that uses a palette, remove the palette, Cancel at the editor prompt.
- **UI-PROJECT-TREE-123** — If applying the deletion fails, then the tree shall alert "Delete" with the reason.
  - Tests: untested

### Relink

- **UI-PROJECT-TREE-130** — Relink... shall ask for a file and relink the missing data file to it (LIB-PROJECT-SERVICE); on success the node shall stop showing as missing.
  - Tests: manual — rename a ROM, reopen the project, Relink... to the renamed file.
- **UI-PROJECT-TREE-131** — If the relink fails, then the tree shall alert "Relink failed" with the reason and keep the node missing.
  - Tests: untested
- **UI-PROJECT-TREE-132** — If Relink... runs on a data file that is no longer missing, then the tree shall clear the missing display without asking for a file.
  - Tests: untested

## Invariants

- For every open project, the root's VM subtree matches its `ProjectTree` node for node, in sorted order, and `ProjectNodeViewModel.Find` maps each node to its VM.
- A standalone root's VM has no children and `Find` returns it only for its own node.
- Every context-menu command goes through `ProjectService`; the tree VM never edits `Children` directly.

## Edge cases

- Add New Folder... in a parent that already has "New Folder" fails with "Folder Creation Error"; no number is appended.
- Accepting Rename... without changing the name fails with "Rename failed" (the service finds a sibling with that name, the node itself); a case-only change succeeds.
- Add New Palette's and Add New Scattered Arranger's validation does not block the Add button: an empty or duplicate name is accepted and only the service's duplicate check (or none, for an empty name) applies. The arranger dialog never runs its validation at all.
- Add Existing Data File... checks names only in the target parent; the same file may be added twice under different folders.
- New Project from Existing File... on a file already in an open project is not detected; it creates a second project over the same file.
- Relink... and the missing state are not refreshed if a file goes missing or reappears during the session; the state is read when the project opens.
- Closing a root saves its project before asking about editors, so a Cancel still leaves a saved project.
- The Ctrl and Alt modifiers request Copy and Link drag effects, but the drop handler always moves.

## Threading and lifetime

- `ProjectTreeViewModel` is a DI singleton; it follows `ProjectService.ProjectOpened`/`ProjectClosed` and receives `AddScatteredArrangerFromCopyMessage` and `ImportImageIntoArrangerMessage` through the default Messenger.
- Each `ProjectNodeViewModel` subscribes to its tree's `Changed` and keeps a node → VM map; it does not unsubscribe when the root closes (the tree and VM become unreachable together).
- Drag payloads are kept in the static `DragPayloadStore` under a GUID key for the drag's duration, because Avalonia drag data carries only strings and bytes.
- All commands run on the UI thread; `MoveNodeToAsync` from a drop is fire-and-forget.

## Decisions

- **The tree view is a projection of the domain.** One `ResourceNodeViewModel.Create` factory builds node VMs; `ProjectNodeViewModel` follows `ProjectTree.Changed` and inserts in sorted position with `ResourceNodeComparer`; a move reuses the existing VM. Commands only call the service and select the result. Reason: the tree had been patched by hand in five places and re-derived after deletion by an O(n²) diff with an inverted early exit. Rejected: `INotifyPropertyChanged` on `ResourceNode`, because `Renamed` already covers the tree VM and the editors.
- **No tree-level modified state.** Tree operations write to disk as they happen, so the tree VM has no `IsModified` or save prompt of its own.
- **Removal prompts belong to the editors.** The tree previews an immutable `ResourceDeletionPlan`, shows it, hands it to `EditorsViewModel.ConfirmRemovalAsync`, then applies it; editors of removed resources close on the `Removed` event. Reason: removal used to clear every open editor without a prompt.
- **Move to Folder... is the automatable path; drag and drop shares it.** Both call `MoveNodeToAsync`, and both filter targets with `CanMoveNode`, so the drop highlight shows only on valid targets. Reason: reorganizing a project must not need XML editing, and a menu item stays drivable through DevTools. The picker's pre-filter also avoided a throw in `MoveNodeAsync` for nodes without a disk location, since fixed in the service.
- **Drag behaviors sit on the node header.** `PayloadDragBehavior`/`PayloadDropBehavior` attach to `ContentControl.nodeHeader` in the item template, not to `TreeViewItem`. Reason: headers are not nested and exclude the expander chevron, so ancestor items don't also handle the drag and the highlight covers only the header. Rejected: the library `ContextDragBehavior` on `TreeViewItem`.
- **Missing data files load and are relinked in place.** A project with a missing file opens with the node marked missing and one alert; Relink copies the chosen file to the expected location and name so references stay unchanged.
- **Standalone roots are a `DataFileNodeViewModel` subclass.** `StandaloneFileNodeViewModel` reuses the data-file template and icon, gets its own `standalone` class and context menu, and is activated like a data file, so no separate codec lookup was extracted. Roots stay in open order.
- **One `FileDataSource` per file.** Opening, adding or converting a file that is open standalone first closes or selects the existing tree, because its stream would block a second source on the same file.

## Non-goals

- Tree search or filter, duplicating resources, "New Sequential Arranger" (backlog).
- Adding a standalone file to an existing project ("Add to Project..."); conversion covers it.
- Copy or link drag and drop.

## Open items

- `ResourceRemovalChangesViewModel` adds changed-but-kept resources (lost element or lost palette) to `RemovedResources`, so the "Changed Items" section never shows and kept resources are listed under "Removed Items".
- Add dialogs compute `CanAdd` and validation errors but do not gate Accept on them; `AddScatteredArrangerViewModel.ValidateModel` is never invoked. No client-side or service-side check rejects empty names or names with invalid file-name characters for add or rename.
- Rename with an unchanged name reports a failure instead of doing nothing.
- "Add New Folder..." has an ellipsis but opens no dialog, and fails if "New Folder" exists (the service's doc comment says the name is augmented; it is not).
- `NameResourceViewModel` (new arranger from selection) has no validation, and the arranger is always added at the project root, not near the source.
- Dead code: `ProjectTreeView.ProjectNode_KeyDown` (not wired), `ResourceNodeViewModelExtensions.BottomUpTraversal` (unused), the single-argument `ResourceRemovalChangesViewModel` constructor and its commented-out members, `ResourceNodeViewModel.SortPriority` (no reader; ordering uses `ResourceNodeComparer`), and `IsSelected` on node VMs (not bound).
- `ProjectTreeView` registers `ProjectTree_KeyDown` both in XAML and as a tunnel handler; the tunnel handler marks Enter handled, so the XAML one never sees it.
- Only the VM projection is unit tested; Open File, recent-list, close and convert paths need the file picker or many services and are manual.
- Manual checks still open: dropping onto a folder and onto an invalid target; rename a ROM, reopen, relink.
- Manual checks still open: Open File on a ROM shows a root with no expander and a file icon, and double-click/Enter opens a new editor each time; reopening it, or opening a file already in an open project, selects the existing node; two editors on one standalone file refresh each other after a save; Close and Create Project from File... work, with the data file node selected; the recent list is unchanged after Open File; "Add as New Scattered Arranger..." is disabled with its hint in a standalone editor (UI-ARRANGING); "Open File..." sits above "Open Project..."; "Close All Projects" closes a project DataFile's sequential editor tab.
- `PayloadDragBehavior`, `PayloadDropBehavior` and `DragPayloadStore` are also used by the graphics editor's arranger drag and drop (UI-ARRANGING); this spec owns them.
