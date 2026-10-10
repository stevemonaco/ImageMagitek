# TileShop.UI specs

One spec per feature; format and usage in [../README.md](../README.md). Find a type's spec by its `types` list, or search this folder for the type name.

## Application

| Spec | Id | Owns |
|---|---|---|
| [Application shell](shell.md) | UI-SHELL | `ShellViewModel`, `MenuViewModel`, `StatusViewModel`, `PreferencesViewModel`, `HotkeyService`, `EditCommands`, `InteractionService`, `ViewLocator`, `TileShopBootstrapper`, `UserPreferences`, dialogs (`AlertViewModel`, `PromptViewModel`, `RequestViewModel`) |
| [Editor hosting](editors.md) | UI-EDITORS | `EditorsViewModel`, `DockableEditorViewModel`, `ResourceEditorBaseViewModel`, `PaletteColorAssignedMessage` |
| [Welcome screen](welcome.md) (draft) | UI-WELCOME | `WelcomeViewModel`, `DemoArt` (the demo file generator) |
| [Project tree](project-tree.md) | UI-PROJECT-TREE | `ProjectTreeViewModel`, `ResourceNodeViewModel` and subclasses, `ResourceNodeComparer`, `PayloadDragBehavior`, `PayloadDropBehavior`, move/rename/add/remove dialogs |

## Graphics editor

| Spec | Id | Owns |
|---|---|---|
| [Graphics editor](graphics-editor.md) | UI-GRAPHICS-EDITOR | `GraphicsEditorViewModel`, `GraphicsEditMode`, `ArrangerRenderer`, grid settings, Jump to Offset, custom element layout, `IToolHandler` |
| [Arranging and selection](arranging.md) | UI-ARRANGING | `ArrangeTool`, element/pixel select, apply/pick palette, mirror/rotate tools, `ArrangerSelection`, `ArrangerPaste`, drag and drop handlers, resize and associate-palette dialogs |
| [Drawing](drawing.md) | UI-DRAWING | `DrawTool`, pencil, flood fill, color picker, `DrawClipEffect`, `ColorEditorFlyoutViewModel`, `ColorRemapViewModel` |
| [Graphics edit history](edit-history.md) | UI-EDIT-HISTORY | `GraphicsEditHistory`, `HistoryAction` and subclasses, `ArrangerSnapshot` |
| [Image import and export](image-import-export.md) | UI-IMAGE-IO | `ImportImageViewModel`, `ImportPreviewRenderer`, `ImportImageIntoArrangerMessage` |

## Palette editor

| Spec | Id | Owns |
|---|---|---|
| [Palette editor](palette-editor.md) | UI-PALETTE-EDITOR | `PaletteEditorViewModel`, `PaletteEditSession`, `PaletteSelection`, `PaletteSnapshot`, color editors, `ChangeColorModelViewModel`, color source models |
