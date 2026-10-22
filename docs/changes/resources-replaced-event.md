# Resources replaced event and one editor contract

## Why

Line numbers are at commit c7d5bfc, before roadmap steps 1-3.

The UI decides which editors an operation affects in five places, with five different predicates:

- `EditorsViewModel.OnTreeChanged`, Renamed (`EditorsViewModel.cs:121`): `Resource` or `OriginatingProjectResource` is the node's item.
- `EditorsViewModel.OnTreeChanged`, Removed (`:134-140`): the same check over the removed subtree.
- `EditorsViewModel.ConfirmRemovalAsync` (`:172-205`): the same check over the plan's changes (`:175-177`), plus "dangling" modified scattered editors whose working arranger references a removed resource (`:185-189`, `ReferencesAny` at `:210`).
- `GenerateResourcesViewModel.EditorsUsing` (`GenerateResourcesViewModel.cs:315-321`): resource, origin, working arranger elements, or the editor's palette list.
- `ProjectTreeViewModel`: `ResolveUnsavedChangesAsync` matches `Resource` only (`ProjectTreeViewModel.cs:505`), and `CloseProject` matches by tree containment (`:893-895`).

There are also three different unsaved-changes prompts. `EditorsViewModel.RequestSaveUserChanges` (`EditorsViewModel.cs:502`) asks Yes/No/Cancel with a "closing" phrase. `ProjectTreeViewModel.ResolveUnsavedChangesAsync` (`:503-527`) asks Yes/No/Cancel with "Save them before {verb}?" and an optional discard. `ConfirmRemovalAsync`'s "Discard Changes" prompt (`EditorsViewModel.cs:191-197`) asks Ok/Cancel.

Regeneration has the worst of it. The editor lifecycle lives in a dialog view model: `GenerateResourcesViewModel.OnAccepted` and its helpers (`:225-374`) prompt, record which editors are open by future key, close editors whose items vanished, and reopen them. Retargeting raises no event, so the dialog must predict what the service will replace. `PredictReplaced` (`:324-348`) re-implements `ProjectService.RetargetReferences` through the public static `ProjectService.RebuildOrder`. The prediction and the service already disagree in one case (see [resource-graph](resource-graph.md)). Detaching a replaced node raises `Removed` (LIB-PROJECT-TREE-008), so `EditorsViewModel` closes its editors mid-operation (UI-EDITORS-042) and the dialog reopens them afterwards.

Dialog view models also depend on the concrete `EditorsViewModel` (`GenerateResourcesViewModel.cs:37`, `ProjectTreeViewModel.cs:38`), which makes their tests build the whole editors shell.

## What

This proposal depends on [resource-graph](resource-graph.md). It works with or without [split-project-service](split-project-service.md).

**Plans name what they affect.** `ResourceDeletionPlan` already lists its changes. `GenerationPlan` gains `Affected`: the replaced, removed and rebuilt resources and the arrangers retargeted in place. These come from the same `ResourceGraph.RebuildOrder` and removal closure that execution uses, so a prediction cannot drift from what happens. `PredictReplaced` is deleted.

**Replacement is a tree change.** `ProjectTreeChangeKind` gains `Replaced`, which carries the old and new node. Regeneration and retargeting swap nodes with a new `ResourceNode.ReplaceChildNode` instead of detach plus attach, and a rollback raises the reverse `Replaced`. The tree view swaps the node view model in place, keeping expansion and selection. `EditorsViewModel` handles `Replaced`: an unmodified editor on the old resource reopens on the new one in the same tab position, active if it was active. That replaces `FindOpenEditors`, `CloseEditorsOfReplacedResources` and `ReopenEditorsAsync` (`GenerateResourcesViewModel.cs:286-372`). Arrangers retargeted in place are not swapped. Their editors are found from the plan's `Affected` set and resolved before the operation, as today.

**One editor contract.** `ResourceEditorBaseViewModel` gains:

- `bool DependsOn(IReadOnlySet<object> resources)`. The default checks `Resource` and `OriginatingProjectResource`. `GraphicsEditorViewModel` adds its working arranger's elements and its palette list. `MetatileMapEditorViewModel` adds its map's sets.
- `OnClosing()`, for per-editor cleanup that is special-cased today, such as `MetatileMapEditorViewModel.ShowAllLayers` in `RemoveEditors` (`EditorsViewModel.cs:159-160`).

`SaveChangesAsync` and `DiscardChanges` already exist on `ToolViewModel`.

**One finder and one prompt.** `EditorsViewModel` gets `FindAffected(IReadOnlySet<object>)`, built on `DependsOn`, and `ResolveUnsavedChangesAsync(IReadOnlyList<editor>, UnsavedChangesPrompt)`. `UnsavedChangesPrompt` holds the text variant ("will be closed", "will be closed by regeneration", "before exporting"), whether No discards, and whether the choice is Save/Discard/Cancel or Discard/Cancel. The existing prompts become calls to it, with their texts unchanged.

**`IEditorManager`.** An interface over `EditorsViewModel` with `Editors`, `ActiveEditor`, `ActivateEditor`, `FindAffected`, `ResolveUnsavedChangesAsync`, `RemoveEditors` and `ConfirmRemovalAsync`. `GenerateResourcesViewModel` and `ProjectTreeViewModel` take the interface, and tests use a fake.

User-visible behavior does not change, with one exception: during regeneration, an editor on a replaced resource now stays in its tab position instead of closing and reopening at the end of the tab list.

Cost: about 4-5 days. Two days for the contract, finder, prompt and interface; two for `Replaced` through the tree, the tree view and the editors; the rest for tests and specs.

## Decisions

- **Plans carry their effects.** The service computes the affected set when it plans, and the UI reads it. Reason: one computation, so prompts match what runs. Rejected: a `ResourcesReplaced` event raised after the commit alone. It cannot drive the save prompts, which must come before the operation (UI-PROJECT-TREE-151, UI-EDITORS-043).
- **`Replaced` on the tree, not a service event.** Every consumer already follows `TreeChanged`; rollbacks already emit reverse changes (ARCHITECTURE §4); and a swap keeps the tree view's state. Rejected: a separate `IProjectService.ResourcesReplaced` event, which consumers would have to merge with `TreeChanged` and which would race the `Removed` it follows.
- **Editors say what they depend on.** Each editor type knows what it reads, so the predicate is polymorphic, not a list of `is GraphicsEditorViewModel` checks. Rejected: a static helper per editor type (today's `ReferencesAny`), which callers must remember to combine.
- **Prompt text stays.** The unified prompt takes its wording from the caller, so UI-EDITORS-030, -044, UI-IMAGE-IO-007, -026 and UI-PROJECT-TREE-151 keep their exact texts.
- **Dialogs depend on an interface.** Rejected: passing callbacks (`Func<…>`) into each dialog, which hides the same coupling in more parameters.

## Spec changes

- **LIB-PROJECT-TREE:** add (draft wording): "When a child is replaced, the tree shall raise one `Replaced` change carrying the old and the new node, and no `Removed` or `Added`; the new node takes the old one's position among its siblings." Reword the event list in LIB-PROJECT-TREE-007/-008's section to include `Replaced`.
- **LIB-PROJECT-SERVICE-079:** "replace each changed resource's node in the tree" becomes "…with a `Replaced` change". Add (draft): "A regeneration plan shall list the resources the run would replace, remove, rebuild or retarget, as the resource graph orders them." The plan type is LIB-TABLES' `GenerationPlan`, so the requirement may belong there instead (see Open questions).
- **UI-EDITORS:** add (draft): "When a node is replaced, every unmodified editor on the old resource shall reopen on the new one in its tab position, staying active if it was active." Reword -042 to cover `Removed` only. Keep -043 to -045 and state that they use the editors' dependency check.
- **UI-PROJECT-TREE-150, -151:** reword "editors … that regeneration would close" as the plan's affected set. This spec is being edited concurrently; take its wording at implementation time.
- **UI-PROJECT-TREE:** add (draft): "When a node is replaced, the tree shall swap its view model in place, keeping its expansion and selection."
- **ARCHITECTURE §4:** add `Replaced` to the list of tree change kinds.

## Tasks

1. **Editor contract.** Add `DependsOn` and `OnClosing`, with overrides in the graphics, metatile map and palette editors. Tests (`EditorsViewModelTests`): a graphics editor depends on a palette only in its palette list; a metatile map editor depends on its sets.
2. **Finder and prompt.** Add `FindAffected` and `ResolveUnsavedChangesAsync`, and move `RequestSaveUserChanges`, `ProjectTreeViewModel.ResolveUnsavedChangesAsync`, the dangling-editor prompt and the close-project loop onto them. Tests: the existing prompt tests pass with unchanged texts; Cancel at the second of three editors stops and leaves all three open.
3. **`IEditorManager`.** Extract it, register it in DI, and switch `GenerateResourcesViewModel` and `ProjectTreeViewModel` to it. Tests: `GenerateResourcesViewModelTests` use a fake manager (coordinate with the in-flight edits to that file).
4. **Affected set on plans.** `PlanGeneration` fills `GenerationPlan.Affected` from the graph, and `PredictReplaced` is deleted. Tests: the affected set equals what `GenerateResourcesAsync` replaces and retargets, including the chained compressed case from [resource-graph](resource-graph.md).
5. **`Replaced`.** Add `ResourceNode.ReplaceChildNode` and `ProjectTreeChangeKind.Replaced`; the service forwards it; regeneration and retargeting use it, and rollback uses the reverse. Tests (`ProjectTreeEventTests`, `TableGenerationProjectTests`): one `Replaced` per swapped node, none of `Removed`/`Added`; a failed regeneration raises the reverse swaps.
6. **Editors and tree on `Replaced`.** Editors reopen in place, and the tree view swaps the node view model. Remove `FindOpenEditors`, `CloseEditorsOfReplacedResources` and `ReopenEditorsAsync`. Tests: a view-model test that an open editor on a regenerated palette stays at its tab index and active.
7. **Manual:** regenerate with editors open on a changed palette, a rebuilt tilemap and a retargeted arranger; check tab order, the active tab, tree expansion and selection.
8. **Close.** Update the specs above and the backlog.

## Open questions

- Does `Affected` belong on `GenerationPlan` (LIB-TABLES) or on a service-side wrapper (LIB-PROJECT-SERVICE)? The plan type is produced by `TableGenerator` and extended by the service with `Regeneration`. Proposed: on `RegenerationPlan`, which only the service fills.
- Should a modified editor on a replaced resource be kept open on the old object after the user chooses No ("keep editing")? Today No discards its changes and the editor is reopened on the new resource (UI-PROJECT-TREE-151). Proposed: no change.
