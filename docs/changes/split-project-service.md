# Split ProjectService

## Why

`ProjectService` (`ImageMagitek.Services/ProjectService.cs`, 1926 lines) holds seven concerns. The ranges below are at commit c7d5bfc, before roadmap steps 1-3:

| Lines | Concern |
|---|---|
| 1-97, 1914-1926 | Infrastructure: open-tree registry, event forwarding, the service-wide write lock (`WithWriteLockAsync` :64), XML conflict scan, `CreateResourceMap` |
| 98-469 | Lifecycle: create, open, open standalone, save, save-as, close |
| 470-656, 685-932 | Tree operations: add (with a per-type node factory at :483-493), create folder, name checks, save resource, rename, move |
| 657-684 | Lookups |
| 933-976 | Relink |
| 977-1211 | Deletion: apply (:977-1129, `WithoutRemovedKeys` :1090) and preview (:1131-1211) |
| 1213-1455 | Generation planning: `PlanGeneration`, manifest read, `GeneratedTypeName`, `FindGeneratedResources`, `KeepReferencedRemovals`, the public statics `References` (:1413) and `RebuildOrder` (:1420), target folder resolution |
| 1456-1913 | Generation and regeneration execution: build, write, rollback, `RegenerateCoreAsync`, `FinishRegenerationAsync`, `RetargetReferences`, `RetargetElements`, conflicts, `EnsureFolder`, `RollBackGeneration` |

Generation is about 700 lines (1213-1913), more than a third of the file. Its tests (`TableGenerationProjectTests`) have to construct the whole service. Deletion planning is pure tree logic with no I/O, but it lives in the Services assembly, so the core cannot use it.

`RebuildOrder` is public and static only so that `GenerateResourcesViewModel.PredictReplaced` (`GenerateResourcesViewModel.cs:334`) can reach it. `References` is public static too, but only `ProjectService` calls it.

Regeneration also writes the rebuilt and retargeted dependents in a second transaction after the main commit (`FinishRegenerationAsync`, `:1692-1697`), as LIB-PROJECT-SERVICE-079 specifies. If that write fails, the generated resources are committed, and the files at their old locations have already been deleted (`RemoveRenamedFile` at `:1675`). Dependents still name the old keys, so when a generated resource was renamed, the project can fail to load (a missing key) until the user saves it again.

## What

Three moves. This proposal depends on [resource-graph](resource-graph.md) and assumes roadmap steps 1-3 have landed.

1. **`ResourceGenerationService`** (`ImageMagitek.Services`, behind `IResourceGenerationService`) takes `PlanGeneration`, `GenerateResourcesAsync` and `FindGenerationConflicts`, with everything in lines 1213-1913 that only they use. It shares the open trees, the write lock and the serializer factory with `ProjectService` through a small internal `ProjectWorkspace` (registry plus lock plus factory), so LIB-PROJECT-SERVICE-063's one-at-a-time rule still covers both. `IProjectService` loses the three generation members. The UI's `GenerateResourcesViewModel` and the CLI's `GenerateHandler` take the new interface. `CliBootstrap.CreateProjectService` returns both services, since the CLI's bootstrap now returns only `IProjectService` (`CliApplication.cs:25`).
2. **`ResourceDeletionPlanner`** moves to the core (`ImageMagitek/Project/`, beside `ResourceChange` and `ResourceDeletionPlan`, which already live there). It turns a node into a `ResourceDeletionPlan` using the graph's removal closure, and turns a plan into the edited models (`WithoutRemovedKeys`, the tile palette clear). `ProjectService` keeps `PreviewResourceDeletion` as a one-line call and keeps applying the plan, because applying needs the writer, the lock and file deletes. The generation service uses the planner directly for its removal checks.
3. **No public statics.** `References` and `RebuildOrder` are removed. Their callers use `ResourceGraph` until [resources-replaced-event](resources-replaced-event.md) removes the UI's need to predict.

The generation service also **folds the retarget writes into the main transaction**. Before the commit, it maps every dependent the graph's rebuild order names to its model with a resource map in which each replaced resource maps to its new key. With record models, this is `MapCurrentModel` plus `with`. It then writes those models in the same write-ahead-log transaction as the generated resources and the manifest. After the commit only in-memory work remains: rebuild, retarget in place, dispose, and delete old files. A failed transaction leaves everything as it was (LIB-PROJECT-SERVICE-070).

What does not change: every public behavior apart from the transaction boundary, the event sequence, the result messages, and the deletion apply path.

Cost: about 3-4 days. The moves are mechanical. The transaction fold and its rollback tests are the real work.

## Decisions

- **Generation becomes its own service; lifecycle and tree operations stay.** Generation is the largest concern and has its own consumers (the generate dialog, the CLI verb). The rest is tied together by the lock and the events. Rejected: one service per concern (lifecycle, tree, relink), which leaves several tiny classes over one lock. Rejected: partial classes, which shrink the files but not the coupling or the test setup.
- **A shared workspace object, not an inheritance chain.** Both services take `ProjectWorkspace` from DI. Reason: one lock for all trees is a decision of LIB-PROJECT-SERVICE ("One service-wide write lock"), and it must stay one object. Rejected: the generation service calling `ProjectService` for the lock (a circular dependency through public API).
- **Deletion planning is core; applying stays in Services.** The planner is pure and uses only core types. Applying writes files. Rejected: moving apply too, which would pull the writer and the lock into the core.
- **One transaction for regeneration.** Spec check: the Decisions entry "Regeneration replaces changed nodes and retargets their users" says dependents are "written only when their saved form changed", and "Resource file deletes happen after the commit" is about deletes, not writes. Neither requires a second transaction; only the wording of LIB-PROJECT-SERVICE-079 does ("After the commit … write those whose saved form changed"). Reason to change: the failure described in Why. Rejected: keeping the second write and retrying it on the next save (the project stays unloadable until then).

## Spec changes

- **LIB-PROJECT-SERVICE:** move -067 to -079 under a new heading "Generation service", keeping their ids (ids are never renumbered). Change the `sources`/`types` front matter to add `ResourceGenerationService`, `IResourceGenerationService` and `ProjectWorkspace`. Change -079 to: "…write the added and changed resources, every other resource whose saved form changes because it uses a replaced resource, the project file when its version changes and the manifest (LIB-TABLES-166) in one write-ahead-log transaction … After the commit it shall rebuild and retarget those resources in memory, dispose every replaced data source, tilemap and metatile map once all their users point elsewhere, delete the files left at the changed resources' old locations, and delete the removed resources as LIB-PROJECT-SERVICE-048 applies a deletion." Reword the Decisions entry "Regeneration replaces changed nodes and retargets their users" to match, and add the decisions above. Extend -063 to name the generation service.
- **LIB-PROJECT-SERVICE-045 to -047, -066:** the requirements stay, and their governing type becomes `ResourceDeletionPlanner`. Add it to `types`; the spec may stay the owner, or the requirements move to LIB-PROJECT-TREE with the planner (see Open questions).
- **CLI-COMMANDS:** no requirement changes; `GenerateHandler` takes the new interface and the bootstrap is internal.

## Tasks

1. **Workspace.** Extract `ProjectWorkspace` (open trees, lock, serializer factory, `CreateResourceMap` after step 2's dedup) and have `ProjectService` use it. Tests: existing `ProjectServiceTests` pass, including `ConcurrentSaveAndSaveResource_BothSucceed_NoLeftoverFiles`.
2. **Deletion planner.** Move the preview and the model edits to `ResourceDeletionPlanner` in the core, over [resource-graph](resource-graph.md). Tests: the deletion tests run against the planner directly where they need no I/O; the existing `ProjectServiceTests.Delete*` pass.
3. **Generation service.** Move lines 1213-1913 into `ResourceGenerationService`, register it in DI (`Bootstrapper.cs`) and in `CliBootstrap`, and update `GenerateResourcesViewModel` and `GenerateHandler`. Remove `References` and `RebuildOrder`. Tests: `TableGenerationProjectTests` construct the generation service; a test that generation and a save in flight run one at a time (-063); the CLI generate tests in `Cli/` pass.
4. **One transaction.** Map retargeted dependents before the commit and write them with the generated resources. Tests: new `Regenerate_RenamedResource_DependentsWrittenInSameTransaction` (one journal, every dependent's file names the new key); new `Regenerate_TransactionFails_DependentsUnchangedOnDisk`; `Regenerate_DependentReferencesResourceNowAtOldKey` and `Regenerate_WrittenMapReferencesRebuiltSet` pass; reloading the project after regeneration succeeds.
5. **Close.** Update LIB-PROJECT-SERVICE and its Decisions, `docs/ARCHITECTURE.md`'s project table entry for `ImageMagitek.Services`, and the backlog.

## Open questions

- Should the deletion requirements move with the planner into LIB-PROJECT-TREE, or stay in LIB-PROJECT-SERVICE with a cross-reference? Staying is less churn, and the requirement ids do not change either way.
- `ResourceGenerationService` or `TableGenerationService`? Generation today comes only from table definitions (LIB-TABLES). The broader name leaves room for scanners ([container-scanners](container-scanners.md)), which also add resources in bulk.
