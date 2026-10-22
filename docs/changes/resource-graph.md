# Resource graph

## Why

Line numbers are at commit c7d5bfc, before roadmap steps 1-3.

Links between resources are expressed four ways, and each consumer re-derives the part it needs:

- `IProjectResource.LinkedResources`. `TilemapArranger.LinkedResources` (`TilemapArranger.cs:501`) leaves out its `TilePaletteArranger` on purpose (LIB-TILEMAPS-072), so `ProjectService` adds it back by hand three times: the deletion preview (`ProjectService.cs:1201-1207`), `References` (`:1413-1415`) and the second pass of `RetargetReferences` (`:1764-1768`).
- Element walks over each element's source and codec palette: the deletion preview (`:1186-1199`), `ApplyResourceDeletionCoreAsync` (`:1016-1030`), `RetargetElements` (`:1780-1806`), and in the UI `EditorsViewModel.ReferencesAny` (`EditorsViewModel.cs:210`) and `GenerateResourcesViewModel.EditorsUsing` (`GenerateResourcesViewModel.cs:315-321`).
- Model-level keys: `RegenerationPlan.WithKeys`/`ReferencedKeys` (`RegenerationPlan.cs:261-398`), used by `KeepReferencedRemovals` and the hash check in `FindGeneratedResources`.
- Three dependency orderings: the reader's per-type load loops (`XmlProjectReader.cs:109-161`, LIB-PROJECT-FORMAT-021), `ProjectService.RebuildOrder` (`:1420-1428`), and the deletion preview's phases (compressed passes, then palettes, then tilemaps/sets before maps, then arrangers, then tile palettes; `:1146-1207`).

This has costs:

- **A latent bug.** `RetargetReferences` makes one pass in `RebuildOrder` rank, with depth-first tree order inside a rank (`:1722-1774`). Every compressed source has rank 0. Take compressed C3 over compressed C2 over C1, where regeneration replaces C1. If C3 comes before C2 in tree order, C3 is checked while C2 is not yet in `replacements` and is skipped. C2 is then rebuilt, and the old C2 is disposed (`:1688-1692`), so C3 keeps reading a disposed source. One level (C2 over a replaced C1) works. The UI's `PredictReplaced` loops until nothing changes (`GenerateResourcesViewModel.cs:337-345`), so it predicts that C3's editor closes, but the service never rebuilds C3.
- **Quadratic cost.** `KeepReferencedRemovals` (`:1370-1411`) restarts after each kept removal and runs a full `PreviewResourceDeletion` for every removal on every pass. Each preview walks the tree several times. That is O(R²·N) for R removals over N resources, and `CollectRemovals` (`:1642-1653`) previews every removal again.
- **The UI copies the service.** `PredictReplaced` (`GenerateResourcesViewModel.cs:324-348`) re-implements `RetargetReferences` through the public static `ProjectService.RebuildOrder`, so the two can disagree, as the bug above shows.

## What

Add `ResourceGraph` to the core library (`ImageMagitek/Project/ResourceGraph.cs`). It is built from a `ProjectTree` plus the global resources. Each node is a resource, and each edge goes from a dependent to the resource it uses and carries what happens to the dependent when that resource goes away:

| Edge kind | Dependent → target | On removal of the target | On replacement of the target |
|---|---|---|---|
| `Cascade` | compressed data → parent; palette → data source; tilemap → map source, tile source, slot palette; metatile set → metatile/tile/attribute source, slot palette; metatile map → block source, metatile set | dependent removed | dependent rebuilt from its model |
| `DropElements` | scattered arranger → element source | elements dropped; arranger removed when none are left (LIB-PROJECT-SERVICE-047) | elements retargeted in place |
| `FallbackPalette` | scattered arranger → element codec palette | elements get the fallback palette (-046) | codec palettes retargeted in place |
| `ClearTilePalette` | tilemap → tile palette arranger | tile palette cleared (-066) | tile palette retargeted in place |

Each resource type reports its own edges through `IProjectResource.Dependencies` (`IEnumerable<ResourceEdge>`), which replaces `LinkedResources`. The graph adds the reverse index and two queries:

- `RemovalClosure(roots)`: walk the reverse `Cascade` edges to a fixed point, then classify every surviving dependent by the kinds of edge that reach it. `PreviewResourceDeletion` becomes this query plus `ResourceChange` records, with the same results as today.
- `RebuildOrder(replaced)`: the dependents to rebuild, in topological order over `Cascade` edges (Kahn's algorithm, ties broken by tree order so output is deterministic), then the in-place retargets. `RetargetReferences` and the UI's prediction both use it.

`KeepReferencedRemovals` builds one graph, computes each removal's closure once, and when a removal is kept it re-checks only the removals whose closure contains the kept resource: O(R·(N+E)) instead of O(R²·N). The written models' references (`ReferencedKeys`) stay a model-level check; with records (step 3) they read the same edge kinds from the model.

The reader's load order stays the per-kind order of LIB-PROJECT-FORMAT-021, because models are resolved by key before any resource object exists. The rank list moves next to the graph, so the reader and the graph share one source; [resource-kind-descriptors](resource-kind-descriptors.md) later takes it from each kind's descriptor.

Behavior does not change, except that the chained compressed case above is retargeted correctly. This proposal assumes roadmap steps 1-3 have landed: lock-safe `DataSource` stream replacement, one `MapCurrentModel` path and a shared `CreateResourceMap`, and record models.

Cost: about 2-3 days. The graph and its tests take one day, moving the service's three consumers and the UI's prediction onto it takes one, and spec updates take the rest.

## Decisions

- **Edges carry their removal semantics.** The kind says whether the dependent cascades, drops elements, falls back or clears. Reason: today that knowledge lives in the preview's phase order and in hand-written exceptions such as the tile palette. Rejected: a plain adjacency list with the semantics kept in the preview (the current state, spread across four places).
- **Resources report their edges.** `Dependencies` on `IProjectResource` replaces `LinkedResources`, so the tilemap's tile palette is an edge (`ClearTilePalette`) and is no longer missing. Rejected: a graph builder that switches on type (one more per-type switch for [resource-kind-descriptors](resource-kind-descriptors.md) to remove). Rejected: keeping `LinkedResources` beside `Dependencies` (two answers to one question).
- **Topological order, not rank.** Rebuilds follow the edges, so a chain of any depth among resources of one kind is ordered correctly. Rejected: adding ranks or a fixed-point loop to `RetargetReferences`, which fixes this bug but keeps a second ordering beside the deletion phases.
- **The graph is built per operation, not kept live.** A preview, plan or retarget builds it from the tree: O(N+E), which is milliseconds for real projects. Reason: no invalidation on tree changes, arranger edits or palette assignments. Rejected: a cached graph kept current by tree events (arranger element edits raise no event, LIB-PROJECT-TREE-025).
- **The core library owns it.** It needs only the tree and resources, so the UI can query it directly and deletion planning can move to the core ([split-project-service](split-project-service.md)).

## Spec changes

- **LIB-PROJECT-TREE:** add (draft wording, no ids yet): "The resource graph shall hold one edge per dependency each resource reports, with its kind (`Cascade`, `DropElements`, `FallbackPalette`, `ClearTilePalette`)"; "The removal closure of a set of resources shall be every resource reachable from it over reverse `Cascade` edges, repeated until no resource is added"; "The rebuild order of a set of replaced resources shall list every resource that reaches one over `Cascade` edges, each after every resource it depends on, ties in tree order". Add the decisions above to its Decisions section.
- **LIB-TILEMAPS-030:** reword as "A tilemap's dependencies shall be its map source, its tile source and each assigned slot palette (`Cascade`), and its tile palette arranger when it names one (`ClearTilePalette`)". **LIB-TILEMAPS-072:** reword "shall not be a linked resource" to "shall be a `ClearTilePalette` dependency".
- **LIB-METATILE-MAPS-019:** reword "linked resources" as `Cascade` dependencies.
- **LIB-PROJECT-SERVICE-045, -046, -047, -066:** keep the behavior and reword each as the removal closure and edge kinds. Strike the phase ordering from -045, because the closure makes it unnecessary.
- **LIB-PROJECT-SERVICE-079:** add "in the rebuild order of LIB-PROJECT-TREE (draft), so a dependent of a rebuilt resource is rebuilt in turn, however deep the chain".
- **LIB-PROJECT-SERVICE-077:** no change in wording. Its Tests line gains the new quadratic-case test below.
- **UI-PROJECT-TREE-150, -151:** "editors … on resources rebuilt because they use one" are found from the graph's rebuild order. The wording is unchanged unless that spec has changed by then; it is being edited concurrently.

## Tasks

1. **Graph.** Add `ResourceEdge`, `ResourceEdgeKind`, `ResourceGraph` and `Dependencies` on every resource type, and remove `LinkedResources`. Tests (`ResourceGraphTests`): one per edge kind; a tilemap's tile palette edge; removal closure over nested compressed data; a topological order with a three-deep compressed chain listed in reverse tree order; global resources are targets but never dependents.
2. **Deletion preview on the graph.** Rewrite `PreviewResourceDeletion` and the model edits in `ApplyResourceDeletionCoreAsync` to use the closure and edge kinds. Tests: every existing deletion test (`ProjectServiceTests.Delete*`, `CompressedDataProjectTests.DeleteParent_*`, `TilemapProjectTests.Deleting*`, `MetatileProjectTests.Deleting*`) passes unchanged.
3. **Retargeting on the graph.** `RetargetReferences` takes its order from `RebuildOrder`, and `References` and the static `RebuildOrder` are removed. Tests: regression `TableGenerationProjectTests.Regenerate_CompressedChainOverReplacedCompressedData_RebuildsEveryLink`, which fails today: C3 over C2 over a generated C1, with C3 before C2 in tree order. After regeneration, C3's parent is the rebuilt C2, C3 reads the new bytes, and no reachable source is disposed. Existing `Regenerate_*` tests pass.
4. **Removal checks.** `KeepReferencedRemovals` and `CollectRemovals` use one graph and per-removal closures. Tests: the existing `Regenerate_RemovedPaletteUsedByUserArranger_Kept` and `Regenerate_RemovedRecord_DeletesResource`; a new test where keeping one removal blocks a second (a palette used by a kept set) still reports both.
5. **UI prediction.** `GenerateResourcesViewModel.PredictReplaced` calls the graph's rebuild order instead of `ProjectService.RebuildOrder`. Until [resources-replaced-event](resources-replaced-event.md) removes the prediction, this keeps it and the service on one code path. Tests: `GenerateResourcesViewModelTests` predicts the chained compressed case (coordinate with the in-flight edits to that file).
6. **Close.** Update the specs above, add the decisions, and remove any backlog lines this resolves.

## Open questions

- **Should slot palettes fall back instead of cascading?** Deleting a palette that is a tilemap's or metatile set's slot palette today removes the tilemap or set, and through the set every metatile map that lists it, with their files (LIB-PROJECT-SERVICE-045, LIB-METATILE-MAPS-019). A scattered arranger that loses a palette keeps its elements on the fallback palette instead (LIB-PROJECT-SERVICE-046, -048). With edge kinds, a slot palette could be a `FallbackPalette` edge: the slot is cleared and its cells show slot 0 or the codec's default (LIB-TILEMAPS-023). This changes user-visible behavior and the removal dialog's lists, and needs the user's call. The graph supports either answer.
- Does `UnlinkResource` go too? Its only caller is the deletion apply path for scattered arrangers (`ProjectService.cs:1020`), and the base implementation calls `ResetElement`, which throws on a tilemap (`Arranger.cs:346`, `TilemapArranger.cs:290`). Proposed: remove it from `IProjectResource` and keep the element drop as a model edit plus an arranger method.
