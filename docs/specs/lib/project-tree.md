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
  - ImageMagitek/Project/ResourceName.cs
  - ImageMagitek/Project/ResourceGraph.cs
  - ImageMagitek/Project/IUnavailableResource.cs
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
  - CompressedDataNode
  - TilemapNode
  - MetatileSetNode
  - MetatileMapNode
  - ResourceFolderNode
  - IProjectResource
  - ImageProject
  - ResourceFolder
  - ProjectResourceBaseComparer
  - ResourceName
  - ResourceGraph
  - ResourceEdge
  - ResourceEdgeKind
  - ResourceRemovalClosure
  - IUnavailableResource
tests:
  - ProjectTreeEventTests
  - ProjectServiceTests
  - ResourceNameTests
  - ResourceGraphTests
  - CliApplicationTests
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

The change kinds are `Added`, `Removed`, `Moved`, `Renamed` and `Replaced`. A detach and attach inside `ReplaceNodes` raise one `Replaced` in place of the `Removed` (LIB-PROJECT-TREE-007, -008) and `Added` they would raise alone (LIB-PROJECT-TREE-035).

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
- **LIB-PROJECT-TREE-015** — Child-name lookups shall be case-sensitive (`data` and `DATA` are different children).
  - Tests: untested
- **LIB-PROJECT-TREE-016** (inherited) — If the child to remove does not exist, then removal shall fail with a key-not-found error.
  - Tests: untested
- **LIB-PROJECT-TREE-035** — While `ReplaceNodes` runs, the tree shall hold its changes. When it ends, each listed node that was detached and whose new key holds a node attached during the run shall raise one `Replaced` change carrying the new node, its parent, the old node and the old node's former parent, and no `Removed` or `Added`. Changes that cancel out within the run (a node added then removed, or removed then put back under the same parent) shall raise nothing, and every other change shall be raised in order. The index shall follow each attach and detach as it happens. A nested `ReplaceNodes` shall fail with an invalid-operation error.
  - Tests: `ProjectTreeEventTests.ReplaceNodes_DetachAndAttachAtKey_RaisesOneReplaced`, `ProjectTreeEventTests.ReplaceNodes_OldReattached_RaisesNothing`, `ProjectTreeEventTests.ReplaceNodes_OtherChanges_PassThroughInOrder`, `ProjectTreeEventTests.ReplaceNodes_Nested_Throws`, `TableGenerationProjectTests.Regenerate_ChangedResource_RaisesReplacedOnly`

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
- **LIB-PROJECT-TREE-029** — Projects and folders shall report that they can contain child resources; their dependencies shall be empty.
  - Tests: untested
- **LIB-PROJECT-TREE-036** — A resource that reads data files shall report the first missing file it reads (directly, through compressed data's parent, or through its elements' and palettes' sources) and the reason its contents cannot be read (a tilemap's or metatile map's `MapError`, compressed data's `DecompressionError`), or null.
  - Tests: `ArrangerExtensionsTests.FindMissingDataSource_ElementOrPaletteSource`, `TilemapArrangerTests.FindMissingDataSource_MissingMapFile_ReturnsIt`, `CliApplicationTests.Export_MissingDataFile_NamesFileExits7`, `CliApplicationTests.Import_MissingDataFile_NamesFileExits6`, `CliApplicationTests.Export_MetatileMapWithMapError_Exits7`, `CliApplicationTests.Import_TilemapWithMapError_PrintsErrorExits6`

### Resource names

- **LIB-PROJECT-TREE-030** — The name rule shall reject an empty name; a name with leading or trailing whitespace, a leading `.` or a trailing `.`; a name containing `< > : " / \ | ? *` or a control character (U+0000–U+001F); a name whose part before the first `.` is a Windows reserved device name (`CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, `LPT0`–`LPT9`, `COM¹²³`, `LPT¹²³`), ignoring case; a name over 100 characters (counted as Unicode scalar values) or 240 UTF-8 bytes; and, for a folder, a name ending in `.xml` ignoring case. Each rejection shall carry a message naming the problem, and the rules shall be checked in that order with the first failure reported. A leading `.` is rejected because the reader skips such names (LIB-PROJECT-FORMAT-004, -005).
  - Tests: `ResourceNameTests.Validate_Rejects`, `ResourceNameTests.Validate_Accepts`
- **LIB-PROJECT-TREE-031** — Two names shall count as the same sibling name when their NFC-normalized forms are equal ignoring case (ordinal); a name that cannot be normalized shall be compared as is.
  - Tests: `ResourceNameTests.AreSame_CaseVariants_AreSame`, `ResourceNameTests.AreSame_ComposedAndDecomposed_AreSame`, `ResourceNameTests.AreSame_DifferentNames_AreNotSame`, `ResourceNameTests.AreSame_LoneSurrogate_DoesNotThrow`

### Resource graph

Deletion planning over the removal closure is `ResourceDeletionPlanner`, specified in LIB-PROJECT-SERVICE-045 to -047, -066 and -080.

- **LIB-PROJECT-TREE-032** — The resource graph of a tree shall hold one edge for each dependency each resource in the tree reports, with its kind: `Cascade` (the dependent is removed with the target, or rebuilt when it is replaced), `DropElements` (the dependent's elements on the target are dropped), `FallbackPalette` (the dependent falls back to a default palette or slot) or `ClearTilePalette` (the dependent tilemap's tile palette is cleared). A resource outside the tree, such as a global palette or a replaced node already detached, shall be an edge target only, never a dependent.
  - Tests: `ResourceGraphTests.CompressedDataAndPalette_CascadeToTheirSources`, `ResourceGraphTests.ScatteredArranger_DropsElementsOnSourcesAndFallsBackPalettes`, `ResourceGraphTests.RemovingTilePalette_ClearsTilemapTilePalette`, `ResourceGraphTests.RemovingSlotPalette_FallsBackInsteadOfRemoving`, `ResourceGraphTests.GlobalPalette_IsATargetButNeverADependent`
- **LIB-PROJECT-TREE-033** — The removal closure of a set of resources shall be those resources plus every resource reached from them over reverse `Cascade` edges and every resource whose `DropElements` targets are all removed, repeated until no resource is added. Each surviving resource with an edge to a removed one shall be listed as affected, with the kinds of all such edges.
  - Tests: `ResourceGraphTests.RemovalClosure_NestedCompressedData_RemovesEverythingOverIt`, `ResourceGraphTests.RemovalClosure_ArrangerKeepingASource_IsAffectedNotRemoved`, `ResourceGraphTests.RemovingSlotPalette_FallsBackInsteadOfRemoving`
- **LIB-PROJECT-TREE-034** — The rebuild order of a set of replaced resources shall list every resource other than a scattered or sequential arranger that reaches one of them over `Cascade` or `FallbackPalette` edges, each after every listed resource it depends on, ties in depth-first tree order.
  - Tests: `ResourceGraphTests.RebuildOrder_CompressedChainInReverseTreeOrder_ListsEachAfterItsParent`, `ResourceGraphTests.RebuildOrder_ReplacedPalette_RebuildsSetButNotScatteredArranger`

## Invariants

- Every node reachable from the root is in the index, and nothing else is.
- Each indexed palette and data source has exactly one subscription from the tree.
- A node has at most one parent; a removed or detached node has none.
- `Item.Name` equals the node's `Name` after any rename through the node.

## Edge cases

- Children attached to a detached node raise their events to that detached node's own root, which nobody observes.
- Nothing stops a caller from attaching children under a `DataFileNode` root; LIB-PROJECT-SERVICE refuses to, because a data source cannot contain children.
- The tree accepts any child name, including `/`; LIB-PROJECT-SERVICE refuses new names that break the rule, but a hand-made project may still hold them.
- Changes are reported in the order they occur; a subscriber that mutates the tree inside a handler sees nested events.
- `ReplaceNodes` changes are raised only when the swap ends, so a subscriber never sees the intermediate states, while lookups inside the swap already see them. A swap that throws still raises the changes it made.

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
- **The name rule lives here; the tree does not enforce it.** `ResourceName` defines what a new name may be, and LIB-PROJECT-SERVICE is the only enforcer (see its "One rule for names, enforced by the service"). Reason: the reader must keep loading names already on disk, and throwing from attach would turn user errors into exceptions. Rejected: validating in `ResourceNode`/`ProjectTree` attach.
- **Tree lookups stay case-sensitive.** LIB-PROJECT-TREE-015 is kept: path keys in project files resolve exactly, and only the service's uniqueness check ignores case, so new names cannot collide anyway. Rejected: a case-insensitive child comparer, which would change how references in existing projects resolve for no gain.

- **Edges carry their removal semantics.** The kind says whether a dependent cascades, drops elements, falls back or clears its tile palette. Reason: that knowledge lived in the deletion preview's phase order and in hand-written exceptions such as the tile palette. Rejected: a plain adjacency list with the semantics kept in the preview, spread across four places.
- **Resources report their edges.** `IProjectResource.Dependencies` replaces `LinkedResources`, so a tilemap's tile palette is an edge (`ClearTilePalette`) and no caller adds it back by hand. Rejected: a graph builder that switches on type (one more per-type switch); keeping `LinkedResources` beside `Dependencies` (two answers to one question).
- **Topological order, not rank.** Rebuilds follow the edges (Kahn's algorithm, ties in tree order), so a chain of any depth among resources of one kind, such as compressed data over compressed data, is ordered correctly. Rejected: adding ranks or a fixed-point loop to the retarget pass, which fixes the chain but keeps a second ordering beside the deletion phases.
- **The graph is built per operation, not kept live.** A preview, plan or retarget builds it from the tree in O(N+E). Reason: no invalidation on tree changes, arranger edits or palette assignments. Rejected: a cached graph kept current by tree events, since arranger element edits raise no event (LIB-PROJECT-TREE-025).
- **The core library owns the graph.** It needs only the tree and its resources, so deletion, retargeting and the regeneration plan's affected set (LIB-PROJECT-SERVICE-081) query the same code.
- **`UnlinkResource` is removed.** Its only caller was the deletion apply for scattered arrangers, and the base implementation reset elements, which throws on a tilemap. Dropping elements is now a model edit plus `ScatteredArranger.ResetElementsReading` (LIB-ARRANGERS-007). Rejected: keeping it on `IProjectResource`, where every resource implemented it as a no-op.
- **`Replaced` on the tree, not a service event.** Regeneration and rebuilds swap nodes with one `Replaced` change each. Reason: every consumer already follows `Changed`, rollbacks already emit reverse changes (ARCHITECTURE §4), and a swap lets the tree view and the editors keep their state. Rejected: a separate `IProjectService.ResourcesReplaced` event, which consumers would have to merge with `Changed` and which would race the `Removed` it follows.
- **One node class per kind, for now.** `ResourceNode<TModel>` keeps a subclass per resource kind (`TilemapNode`, `PaletteNode`, ...), each named by its kind descriptor (LIB-PROJECT-FORMAT-075). Reason: the UI matches on the node types in many places, so a generic node keyed by kind would churn every match for one fewer file per kind. Rejected for now: one generic node (backlog).
- **Unavailability is one interface.** Data sources, palettes, arrangers, tilemaps, metatile sets and metatile maps implement `IUnavailableResource` (LIB-PROJECT-TREE-036), so the editors and the CLI ask it instead of switching on the resource type; arrangers and tilemaps implement it explicitly over the `IArrangerView` extension, which non-resource views (`TilemapView`, `MetatileLayerArranger`) keep using. Rejected: a missing-file member on `IProjectResource` (projects and folders read no data).
- **Pairing in a tree scope, not `ResourceNode.ReplaceChildNode`.** `ReplaceNodes` holds the changes while a caller detaches and rebuilds, then pairs each listed node with the node at its new key. Reason: the project builder attaches the nodes it builds itself, and resources rebuilt together resolve their references to each other through the tree, so the new nodes must be attached while they are built. Rejected: building detached nodes and swapping them in with one call, which needs a second, tree-free way to resolve references while building.

## Non-goals

- Ordering children. The tree keeps insertion order; the UI projection sorts (folders first, then by name).
- Persistence and disk locations (LIB-PROJECT-FORMAT, LIB-PROJECT-SERVICE).

## Open items

- `ProjectResourceBaseComparer` is internal and unused; the UI has its own comparer.
- `IProjectResource.ShouldBeSerialized` is set by resources but read nowhere.
