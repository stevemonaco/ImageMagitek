# Resource kind descriptors (optional)

## Why

Adding a resource kind means editing the same type switch in many places. Adding the four 1.1 kinds (compressed data, tilemap, metatile set, metatile map) touched every site below. The line numbers are at commit c7d5bfc; roadmap step 2 merges the writer's first two switches into `MapCurrentModel`.

**Core (ImageMagitek)**

- `XmlProjectWriter`: `SerializeResource` (`:74-122`), `WriteResourceAsync` (`:131-197`), `MapCurrentModel` (`:284-299`), the persisted-model setter chain in `RunTransactionsAsync` (`:332-367`), `ToElement` (`:409-422`), plus one `Serialize(…Model)` per kind.
- `XmlProjectReader`: `IsResourceRoot` (`:250`), the `TryDeserializeResource` switch (`:320-353`), and the per-kind load loops (`:109-161`, LIB-PROJECT-FORMAT-021).
- `ProjectFormat.Required` (`ProjectFormat.cs:25-26`), which lists the kinds that need 1.1.
- `XmlProjectSerializerFactory.BuildResources` (`:58-66`), and one `ProjectTreeBuilder.Add…` per kind.
- `RegenerationPlan`: `Offsets` (`:295`), `PaletteKeys` (`:307`) and `WithKeys` (`:318-398`). `TableGenerator.Summarize` (`TableGenerator.cs:677`).
- One `ResourceNode<TModel>` subclass per kind (`Project/Resource Tree/`), and the XSD.

**Services**

- `ProjectService`: the node factory (`:483-493`), the dispose list on deletion (`:1055`), the cascade kinds (`:1176`), `GeneratedTypeName` (`:1267-1276`) and `RebuildOrder` (`:1420-1428`).

**UI and CLI**

- `ResourceNodeViewModel.Create` (`ResourceNodeViewModel.cs:32-41`) and `TreeViewItemNodeClassBehavior` (`:24-29`).
- `EditorsViewModel`: the `OpenEditor` switch (`:248-303`), `AlertIfUnavailableAsync` (`:391-408`) and `FindMissingDataSource` (`:410-419`).
- CLI: `ExportAllHandler` (`:27`), `ImportAllHandler` (`:26`), `Exporter` (`:21-62`) and `Importer` (`:22-98`).

That is about 25 sites in 15 files, plus a node class, a view model, XAML and the XSD. Missing one shows up only when that path runs. For example, `ProjectFormat.Required` must list a new kind, or the project file keeps version 1.0 and an older reader loads it without the new resources. `ResourceFileLocator` already shows the alternative: it is type-agnostic, because it handles only the two special cases (project, folder) and treats every other kind alike.

## What

This proposal is optional. It pays off when the next kind arrives (sequential arrangers as resources, a range source from [container-scanners](container-scanners.md), tables). It depends on roadmap step 3 (record models, so remapping keys is generic `with` code) and on [resource-graph](resource-graph.md), since dependencies are one of the facets a descriptor provides.

**Core: `ResourceKind` descriptors in a `ResourceKindRegistry`.** Each kind registers one descriptor:

- Identity: `ElementName` (`"tilemap"`), `ResourceType`, `ModelType`, `NodeType`, and `GeneratedTypeName`, which today matches `ElementName` for every generated kind.
- Format: `Read(XElement, string name) → ResourceModel`, `Write(ResourceModel) → XElement`, and `MinimumVersion`, which `ProjectFormat.Required` takes the maximum of over the tree.
- Mapping: `MapToModel(resource, resourceMap) → ResourceModel` and `Build(ProjectTreeBuilder, model, parentPath, location) → MagitekResults`.
- Graph: `Dependencies(model) → IEnumerable<(string Key, ResourceEdgeKind)>`, the model-level twin of `IProjectResource.Dependencies`. It replaces `ReferencedKeys`, and with records `WithKeys` becomes one generic remap over the keys it names. `BuildRank` gives the reader's load order (LIB-PROJECT-FORMAT-021).
- Behavior flags: `DisposeOnRemoval`, `RebuiltOnReplacement` (immutable kinds) and `Exportable` (the CLI).
- Regeneration: `Summarize`, `Offsets` and `PaletteKeys`.

The reader's switch, `IsResourceRoot` and load loops; the writer's five switches; `BuildResources`; the node factory and `GeneratedTypeName`; `ProjectFormat.Required`; and `RegenerationPlan`'s helpers all become registry lookups. The `ResourceNode<TModel>` subclasses stay for now, because the UI pattern-matches on them; with a generic node they could collapse later.

**UI: `IEditorFactory` and `IUnavailableResource`.**

- Editor factories (`IEditorFactory`, one per kind, registered in DI) replace the `OpenEditor` switch. Each says whether it opens the resource and how.
- `IUnavailableResource` in the core (`string? UnavailableReason`, `FileDataSource? MissingDataSource`) replaces `AlertIfUnavailableAsync`'s type switch and `FindMissingDataSource`. `TilemapArranger`, `MetatileMap`, `CompressedDataSource` and the arrangers implement it.
- The node view-model factory and the tree item CSS class come from a UI-side registry keyed by node type.

**CLI.** The exporter and importer ask `Exportable` and `IUnavailableResource` instead of listing kinds.

**What a new kind still needs after this:** the resource class and its model; its descriptor (read, write, map, build, dependencies, version); the XSD element; a `ResourceNode<TModel>` subclass while those stay; in the UI, a node view model, its XAML (icon, context menu) and, if it opens, an `IEditorFactory` and its editor; and its spec. That is down from about 25 scattered sites to about 6 places, each the obvious home of the new code.

What does not change: the format, load order, error messages, and every behavior.

Cost: about 4-5 days, mostly mechanical once records and the graph exist. The XSD stays hand-written.

## Decisions

- **Descriptors, not virtual methods on resources.** Reading XML and building from models are serialization concerns, and the resource classes stay free of `XElement`. Rejected: `IProjectResource.Serialize()` (couples domain types to the XML format, which LIB-PROJECT-FORMAT keeps in `Serialization/`).
- **One registry, built at startup.** Built-in kinds register in `XmlProjectSerializerFactory`. Rejected: assembly scanning (slower startup and implicit), and plugin-provided kinds (no use case; the plugin contract stays codecs, LIB-PLUGINS).
- **Load order stays a rank.** Models resolve by key before resource objects exist, so a rank per kind (with compressed data's passes) is enough. Rejected: a topological sort over model keys at load, which changes error order for broken projects and gains nothing for current kinds.
- **UI facets are interfaces in DI.** Rejected: putting UI factories on the core descriptor, which would make the core reference the UI.
- **Optional.** The duplication costs time only when a kind is added. If no new kind is planned for 1.x, this can wait.

## Spec changes

- **LIB-PROJECT-FORMAT:** add (draft wording): "Each resource kind shall be described once by a descriptor naming its root element, model, reader, writer, builder, model dependencies, build rank and minimum format version; the reader, writer and builder shall dispatch through it." Reword -006 so the required version is "the highest minimum version of any resource kind in the tree", with the same result for current kinds. -021 is unchanged, and its order now comes from the build ranks. Add the decisions above.
- **LIB-PROJECT-SERVICE:** no requirement changes. Its node factory and type lists dispatch through the registry.
- **LIB-TABLES-164, -165:** unchanged wording. `Summarize`, `Offsets` and `PaletteKeys` dispatch through the registry.
- **UI-EDITORS-001 to -010, -062, -063, -065:** no behavior change. Add `IEditorFactory` and `IUnavailableResource` to `types`.
- **CLI-COMMANDS:** no change.

## Tasks

1. **Registry and descriptors.** Add `ResourceKind` and `ResourceKindRegistry` with the seven built-in kinds, and move each `Serialize`/`Deserialize` pair into its descriptor. Tests (`ResourceKindRegistryTests`): every kind round-trips a model through `Write`/`Read`; element names are unique; `BuildRank` reproduces the LIB-PROJECT-FORMAT-021 order.
2. **Reader, writer, builder and format version** dispatch through the registry. Tests: `XmlProjectReaderTests`, `XmlProjectWriterTests`, `XmlProjectRoundTripTests` and `SerializationModelTests` pass unchanged; a test with a fake eighth kind proves that no switch remains (read, write, build, version).
3. **Services and regeneration.** Move the node factory, dispose list, `GeneratedTypeName`, `Summarize`, `Offsets`, `PaletteKeys` and the model-level dependencies to the registry. Tests: `TableGenerationProjectTests` and `ProjectServiceTests` pass.
4. **UI and CLI.** Add `IEditorFactory` per kind, `IUnavailableResource`, and the node view-model registry; switch the CLI porters to `Exportable`. Tests: `EditorsViewModelTests` (open each kind; missing data file alert; map error alert); the CLI export and import tests pass.
5. **Close.** Update the specs above, `docs/ARCHITECTURE.md` §4 (one line: "Resource kinds are described once"), and the backlog.

## Open questions

- Should `ResourceNode<TModel>` subclasses collapse into one generic node now? It removes one more per-kind file, but every UI `is TilemapNode` match would change. Proposed: later, with [tilemap-grid-editing](tilemap-grid-editing.md) if that lands.
- Is a sequential-arranger resource (backlog, LIB-PROJECT-FORMAT) the first kind to add through the registry? If so, sequence this proposal just before it.
