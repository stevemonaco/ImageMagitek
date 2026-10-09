---
id: LIB-PROJECT-TREE
title: Project tree and resource nodes
project: ImageMagitek
sources:
  - ImageMagitek/Project/ProjectTree.cs
  - ImageMagitek/Project/ProjectTreeChange.cs
  - ImageMagitek/Project/Resource Tree
  - ImageMagitek/Project/IProjectResource.cs
  - ImageMagitek/Project/ImageProject.cs
  - ImageMagitek/Project/ResourceFolder.cs
  - ImageMagitek/Project/ProjectResourceBaseComparer.cs
types:
  - ProjectTree
  - ProjectTreeChange
  - ProjectTreeChangeKind
  - ResourceNode
  - ResourceNode<TModel>
  - ProjectNode
  - DataFileNode
  - PaletteNode
  - ArrangerNode
  - ResourceFolderNode
  - IProjectResource
  - ImageProject
  - ResourceFolder
  - ProjectResourceBaseComparer
tests:
  - ProjectTreeEventTests
  - ProjectServiceTests
depends:
  - LIB-DATASOURCE
  - LIB-PALETTES
  - LIB-ARRANGERS
---

# Project tree and resource nodes

## Purpose

The in-memory tree of an open project, or of a single data file opened without a project. It holds resource nodes (project, folders, data files, palettes, scattered arrangers), raises an event for every structural change and for content changes of the resources it holds, and answers resource → node lookups. LIB-PROJECT-SERVICE drives it and persists it through LIB-PROJECT-FORMAT; the UI tree is a projection of its events.

## Requirements

### Roots

- **LIB-PROJECT-TREE-001** — The tree shall accept as its root either a `ProjectNode` holding an `ImageProject` or a `DataFileNode`; if given any other root, construction shall fail with an argument error.
  - Tests: `ProjectTreeEventTests.ProjectTree_FolderRoot_Throws`
- **LIB-PROJECT-TREE-002** — While the root is a `DataFileNode`, the tree shall report `IsStandaloneFile` as true and `Project` as null.
  - Tests: `ProjectTreeEventTests.DataFileRoot_IsStandaloneAndIndexesSource`
- **LIB-PROJECT-TREE-003** — The tree's `Name` shall be the root resource's name.
  - Tests: untested
- **LIB-PROJECT-TREE-004** — Path keys shall exclude the root's name, start with `/` and join child names with `/` (a direct child `data` of the root has key `/data`).
  - Tests: untested

### Structural changes

- **LIB-PROJECT-TREE-005** — When a node is attached under a node in the tree, the tree shall raise one `Added` change naming the node and its new parent.
  - Tests: `ProjectTreeEventTests.AttachChildNode_RaisesSingleAdded`
- **LIB-PROJECT-TREE-006** — When a node is attached under a parent that is not in the tree, the tree shall raise nothing.
  - Tests: `ProjectTreeEventTests.AttachChildNode_UnattachedParent_RaisesNothing`
- **LIB-PROJECT-TREE-007** — When a child is removed, the tree shall raise one `Removed` change naming the node and its former parent, and the removed node's `Parent` shall be null.
  - Tests: `ProjectTreeEventTests.RemoveChildNode_RaisesRemovedAndClearsParent`
- **LIB-PROJECT-TREE-008** — When a child is detached, the tree shall raise one `Removed` change and return the detached node with no parent.
  - Tests: `ProjectTreeEventTests.DetachChildNode_RaisesRemoved`
- **LIB-PROJECT-TREE-009** — When a node is renamed, the tree shall raise exactly one `Renamed` change carrying the old name and the current parent, and no `Removed` or `Added`.
  - Tests: `ProjectTreeEventTests.Rename_RaisesSingleRenamedWithOldName`
- **LIB-PROJECT-TREE-010** — When a node is renamed, its resource's `Name` shall change to the same value.
  - Tests: `ProjectTreeEventTests.Rename_RaisesSingleRenamedWithOldName`
- **LIB-PROJECT-TREE-011** — When a node is moved to another parent, the tree shall raise exactly one `Moved` change carrying the new and the old parent, and no `Removed` or `Added`.
  - Tests: `ProjectTreeEventTests.MoveTo_RaisesSingleMovedWithOldParent`
- **LIB-PROJECT-TREE-012** — Every change shall be raised after the in-memory change is applied, so a caller that reverts a change produces the reverse change (a second `Renamed` or `Moved`).
  - Tests: `ProjectServiceTests.RenameFolder_DirectoryMoveFails_RaisesReverseRename`, `ProjectServiceTests.MoveNode_ProjectWriteFails_RaisesReverseMove`
- **LIB-PROJECT-TREE-013** — The root node shall be renameable; renaming it changes the tree's `Name`.
  - Tests: untested
- **LIB-PROJECT-TREE-014** (inherited) — If a child with the same name already exists under the parent, then attaching shall fail with an argument error.
  - Tests: untested
- **LIB-PROJECT-TREE-015** (inherited) — Child-name lookups shall be case-sensitive (`data` and `DATA` are different children).
  - Tests: untested
- **LIB-PROJECT-TREE-016** (inherited) — If the child to remove does not exist, then removal shall fail with a key-not-found error.
  - Tests: untested

### Containment and index

- **LIB-PROJECT-TREE-017** — A node shall count as contained in the tree only while its topmost ancestor is the tree's root.
  - Tests: `ProjectTreeEventTests.RemoveChildNode_RaisesRemovedAndClearsParent`
- **LIB-PROJECT-TREE-018** — The tree shall find the node holding a resource by reference identity in constant time, including the root's own resource.
  - Tests: `ProjectTreeEventTests.Index_FindsResourcesAfterAddRenameAndMove`, `ProjectTreeEventTests.DataFileRoot_IsStandaloneAndIndexesSource`
- **LIB-PROJECT-TREE-019** — When a subtree is attached, every resource in it shall become findable.
  - Tests: `ProjectTreeEventTests.Index_AttachedSubtree_IndexesDescendants`
- **LIB-PROJECT-TREE-020** — When a subtree is removed, none of its resources shall remain findable.
  - Tests: `ProjectTreeEventTests.Index_RemovedFolder_DropsDescendants`
- **LIB-PROJECT-TREE-021** — After a rename or move, the resource shall still resolve to the same node.
  - Tests: `ProjectTreeEventTests.Index_FindsResourcesAfterAddRenameAndMove`
- **LIB-PROJECT-TREE-022** — If a resource is not in the tree, then the throwing lookup shall fail with a key-not-found error naming the resource and tree; the typed lookup shall fail the same way when the node is of another type.
  - Tests: untested

### Content changes

- **LIB-PROJECT-TREE-023** — When a palette in the tree raises `Changed`, or a data source in the tree raises `DataWritten`, the tree shall raise `ResourceChanged` with that resource.
  - Tests: `ProjectTreeEventTests.DataWritten_AttachedSource_RaisesResourceChanged`, `ProjectTreeEventTests.DataFileRoot_DataWritten_RaisesResourceChanged`, `ProjectServiceTests.PaletteChange_RaisesServiceResourceChangedFromTree`
- **LIB-PROJECT-TREE-024** — When a resource has been removed from the tree, its content changes shall raise nothing.
  - Tests: `ProjectTreeEventTests.DataWritten_RemovedSource_RaisesNothing`
- **LIB-PROJECT-TREE-025** — Arranger and folder content changes shall not raise `ResourceChanged`.
  - Tests: untested

### Node state

- **LIB-PROJECT-TREE-026** — Each node shall carry an optional disk location and an optional persisted model; both are null until LIB-PROJECT-FORMAT or LIB-PROJECT-SERVICE sets them, and stay null for standalone trees.
  - Tests: untested
- **LIB-PROJECT-TREE-027** — A `PaletteNode` shall carry an optional committed-model function that writers use instead of the live palette while it is set (see LIB-PROJECT-FORMAT).
  - Tests: `ProjectServiceTests.SaveProject_PaletteWithCommittedModel_WritesCommittedState`
- **LIB-PROJECT-TREE-028** — A `ProjectNode` shall carry the base directory that resource files are located under.
  - Tests: untested
- **LIB-PROJECT-TREE-029** — Projects and folders shall report that they can contain child resources; their linked-resource list shall be empty and unlinking shall do nothing.
  - Tests: untested

## Invariants

- Every node reachable from the root is in the index, and nothing else is.
- Each indexed palette and data source has exactly one subscription from the tree.
- A node has at most one parent; a removed or detached node has none.
- `Item.Name` equals the node's `Name` after any rename through the node.

## Edge cases

- Children attached to a detached node raise their events to that detached node's own root, which nobody observes.
- Nothing stops a caller from attaching children under a `DataFileNode` root; LIB-PROJECT-SERVICE refuses to, because a data source cannot contain children.
- A child name containing `/` is accepted but its path key cannot be resolved back to the node.
- Changes are reported in the order they occur; a subscriber that mutates the tree inside a handler sees nested events.

## Threading and lifetime

- No locking. Changes and events happen synchronously on the caller's thread.
- The tree subscribes to its root once and never unsubscribes; the tree lives as long as its root.
- Content subscriptions are added on index and removed on unindex, so removed resources do not keep the tree alive through it.
- Subscribers: LIB-PROJECT-SERVICE forwards `Changed` and `ResourceChanged` for open trees; UI projections subscribe to `Changed`.

## Decisions

- **The domain raises tree change events; views are projections.** Every mutator raises a `ProjectTreeChange` after the change, bubbled to the root and raised by the tree as `Changed`. Reason: callers no longer have to know what changed and patch each ViewModel by hand. Rejected: per-command VM patching in five places, and a reference-scan `SynchronizeTree` diff that was O(n²) per level.
- **Rename and move raise one event each.** The base library renames and moves through detach/attach on the parent; those events are suppressed and replaced by one `Renamed` or `Moved`. Reason: a projection must not see the node disappear and reappear.
- **Removal clears `Parent`.** The base library left it set, so `ContainsNode` reported removed nodes as attached.
- **No `INotifyPropertyChanged` on nodes.** `Renamed` already covers the tree view and editor titles.
- **Reference-keyed resource index.** Maintained from the tree's own `Added`/`Removed` changes. Reason: editors look up nodes on every save. Rejected: depth-first scans of every open project.
- **Content changes come from the resources.** The tree forwards `Palette.Changed` and `DataSource.DataWritten` for what it indexes, so no caller has to announce them. `DataWritten` is not raised by `Flush` (see LIB-DATASOURCE).
- **A tree may be rooted at a data file.** A standalone file is the same kind of tree with a `DataFileNode` root. Reason: lifetime events, the index, content events and containment lookups then cover standalone files with no second collection or event pair.
- **Events bubble to whatever the root is.** The internal tree-change event lives on `ResourceNode`, not `ProjectNode`, so the tree subscribes to any root.

## Non-goals

- Ordering children. The tree keeps insertion order; the UI projection sorts (folders first, then by name).
- Persistence and disk locations (LIB-PROJECT-FORMAT, LIB-PROJECT-SERVICE).
- Validating resource names.

## Open items

- `ProjectResourceBaseComparer` is internal and unused; the UI has its own comparer.
- `IProjectResource.ShouldBeSerialized` is set by resources but read nowhere.
- Case-sensitive child names (LIB-PROJECT-TREE-015) differ from case-insensitive file systems; see LIB-PROJECT-SERVICE open items.
