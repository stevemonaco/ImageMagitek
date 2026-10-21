# ImageMagitek library specs

One spec per feature; format and usage in [../README.md](../README.md). Find a type's spec by its `types` list, or search this folder for the type name.

## Data and codecs

| Spec | Id | Owns |
|---|---|---|
| [Data sources](datasource.md) | LIB-DATASOURCE | `DataSource`, `FileDataSource`, `MemoryDataSource`, `BitAddress` |
| [Graphics codecs](codecs.md) | LIB-CODECS | `IGraphicsCodec`, XML flow and pattern codecs, specialized direct codecs, `CodecFactory`, `XmlCodecService` |
| [Plugin contract and loading](plugins.md) | LIB-PLUGINS | `ImageMagitek.Plugins.Contracts` types, plugin codec adapters, `PluginService` |
| [Read-only compressed data sources](compression.md) (draft) | LIB-COMPRESSION | `ICompressionPlugin`, `CompressorFactory`, built-in GBA LZ77 and RLE, `CompressedDataSource` |

## Color

| Spec | Id | Owns |
|---|---|---|
| [Color models and color matching](colors.md) | LIB-COLORS | `ColorModel`, color types and converters, `ColorFactory`, `ColorParser`, `PaletteColorMatcher` |
| [Palettes](palettes.md) | LIB-PALETTES | `Palette`, color sources, `PaletteFileSerializer`, `PaletteJsonSerializer`, `PaletteService`, `PaletteStore` |

## Arrangers and images

| Spec | Id | Owns |
|---|---|---|
| [Arrangers and elements](arrangers.md) | LIB-ARRANGERS | `Arranger`, `SequentialArranger`, `ScatteredArranger`, `ArrangerElement`, `TileLayout`, `ElementCopier`, save-conflict analysis, `ElementStore` |
| [Tilemaps](tilemaps.md) (draft) | LIB-TILEMAPS | `MapFormat`, `MapFormatFactory`, `XmlMapFormatReader`, `MapScreenLayout`, built-in map formats, `TilemapArranger`, `TileSet`, `SharedTileConflict`, `TileBrush`, `TilePalette`, `TilemapPlotter` |
| [Metatile maps](metatile-maps.md) (draft) | LIB-METATILE-MAPS | `MetatileSet`, `MetatileOrder`, `MetatileMap`, `MetatileLayerArranger`, `BlockGrid`, `BlockBrush`, `BlockPalette`, `MetatileMapPlotter`, the FRLG Block map format |
| [Layer compositing and scenes](scenes.md) (draft) | LIB-SCENES | `LayerCompositor`, `CompositorLayer`, `LayerImage` |
| [Indexed and direct images](images.md) | LIB-IMAGES | `IndexedImage`, `DirectImage`, `ImageCopier`, `PixelRemapOperation` |
| [Image export and import](image-io.md) | LIB-IMAGE-IO | `ImageSharpFileAdapter`, `IndexedPngFile`, `CombinedPalette`, `ImageImporter`, `ImportReport` |

## Projects

| Spec | Id | Owns |
|---|---|---|
| [Project tree and resource nodes](project-tree.md) | LIB-PROJECT-TREE | `ProjectTree`, `ProjectTreeChange`, `ResourceNode` and node types, `ImageProject`, `ResourceFolder` |
| [Project service](project-service.md) | LIB-PROJECT-SERVICE | `ProjectService`, `ResourceChange`, `ResourceDeletionPlan` |
| [XML project format and transactional writes](project-format.md) | LIB-PROJECT-FORMAT | `XmlProjectReader`, `XmlProjectWriter`, `ProjectTreeBuilder`, serialization models, `WriteAheadLogTransaction` |
| [Bootstrapping and application settings](services.md) | LIB-SERVICES | `BootstrapService`, `SettingsService`, `AppSettings` |

## Tables

| Spec | Id | Owns |
|---|---|---|
| [Table-driven resources](tables.md) (draft) | LIB-TABLES | `TableDefinition`, `XmlTableDefinitionReader`, `Charmap`, address mappers, `TableExpression`, `TableEvaluator`, `RecordInstance`, `TableGenerator`, `GenerationPlan`, `TableManifest` and its reader and writer |
