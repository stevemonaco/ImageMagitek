---
id: UI-SHELL
title: Application shell
project: TileShop.UI
sources:
  - TileShop.UI/App.axaml
  - TileShop.UI/App.axaml.cs
  - TileShop.UI/Program.cs
  - TileShop.UI/Bootstrapper.cs
  - TileShop.UI/ViewLocator.cs
  - TileShop.UI/Features/Shell/ShellViewModel.cs
  - TileShop.UI/Features/Shell/ShellView.axaml
  - TileShop.UI/Features/Shell/ShellView.axaml.cs
  - TileShop.UI/Features/Shell/MenuViewModel.cs
  - TileShop.UI/Features/Shell/MenuView.axaml
  - TileShop.UI/Features/Shell/MenuView.axaml.cs
  - TileShop.UI/Features/Shell/StatusViewModel.cs
  - TileShop.UI/Features/Shell/StatusView.axaml
  - TileShop.UI/Features/Shell/AppIconView.axaml
  - TileShop.UI/Features/Shell/DockableToolViewModel.cs
  - TileShop.UI/Features/Shell/DockableToolView.axaml
  - TileShop.UI/Features/Dialogs/PreferencesViewModel.cs
  - TileShop.UI/Features/Dialogs/PreferencesView.axaml
  - TileShop.UI/ViewExtenders/Docking/DockFactory.cs
  - TileShop.UI/Services
  - TileShop.UI/Models/Hotkey.cs
  - TileShop.UI/Models/EditCommands.cs
  - TileShop.UI/Styles
  - TileShop.UI.Controls/Dialogs
  - TileShop.Shared/Interactions
  - TileShop.Shared/Services
  - TileShop.Shared/Models/UserPreferences.cs
  - TileShop.Shared/Messages/Messages.cs
types:
  - ShellViewModel
  - MenuViewModel
  - StatusViewModel
  - PreferencesViewModel
  - DockFactory
  - DockableToolViewModel
  - HotkeyService
  - Hotkey
  - EditCommands
  - ThemeService
  - InteractionService
  - ExploreService
  - AsyncFileRequestService
  - ClipboardService
  - ViewLocator
  - TileShopBootstrapper
  - UserPreferences
  - UserPreferencesStore
  - DialogHost
  - OverlayDialog
  - AlertViewModel
  - PromptViewModel
  - RequestViewModel
  - NotifyStatusMessage
tests: []
depends:
  - LIB-PROJECT-SERVICE
  - LIB-PALETTES
  - LIB-CODECS
  - UI-EDITORS
  - UI-PROJECT-TREE
---

# Application shell

## Purpose

The main window of TileShop.UI: docking layout, the File/Edit/View/Help menus, the status bar, modal dialogs, window-wide hotkeys, theme, user preferences and app startup/exit. Editor tabs are UI-EDITORS; the tree pane's contents are UI-PROJECT-TREE. Menu items delegate to those two and to the active editor (UI-GRAPHICS-EDITOR, UI-PALETTE-EDITOR).

## Requirements

### Window and docking

- **UI-SHELL-001** — The main window shall draw into its title bar, showing the app icon and the menu bar there, and hide the window title text.
  - Tests: manual — launch TileShop.UI and look at the title bar.
- **UI-SHELL-002** — When the main window opens, the shell shall build a dock layout with the project tree pane on the left and the document area on the right.
  - Tests: manual — launch and inspect the layout through DevTools `tree`.
- **UI-SHELL-003** (inherited) — The tree pane shall start at 0.3 and the document area at 0.7 of the window width.
  - Tests: untested
- **UI-SHELL-004** — The project tree pane shall be pinnable and shall not be closable or floatable.
  - Tests: manual — check the pane header buttons.
- **UI-SHELL-005** — Each open editor shall appear as a closable, floatable document tab whose header shows the resource type icon, the editor's display name and a `*` while the editor is modified.
  - Tests: manual — open an arranger, draw a pixel, check the tab header.
- **UI-SHELL-006** — When a document tab gains focus, the shell shall make its editor the active editor and move keyboard focus to the first focusable control bound to that editor.
  - Tests: manual — switch tabs, then press an editor hotkey without clicking the canvas.

### File menu

- **UI-SHELL-010** — The File menu shall list, in order: New Empty Project..., New Project from Existing File..., Open File..., Open Project..., Open Recent Project, Close <active editor>, Close All Projects, Save <active editor>, Exit.
  - Tests: manual — open the File menu.
- **UI-SHELL-011** — New Empty Project..., New Project from Existing File..., Open File... and Open Project... shall run the project tree's commands of the same purpose (UI-PROJECT-TREE).
  - Tests: manual — run each from the File menu.
- **UI-SHELL-012** — While no editor is active, the Close <name> and Save <name> items and the separator above Save shall be hidden.
  - Tests: manual — start the app with no editors and open the File menu.
- **UI-SHELL-013** — Save <name> shall be enabled only while the active editor is modified, and shall save that editor.
  - Tests: manual — modify an arranger, check Save is enabled, choose it, check `*` clears.
- **UI-SHELL-014** — Close <name> shall close the active editor with the save prompt of UI-EDITORS.
  - Tests: manual — modify an editor, choose Close <name>, check the prompt.
- **UI-SHELL-015** — Close All Projects shall be enabled while any project or standalone file is open, and shall close each open root in turn, stopping at the first one the user cancels.
  - Tests: manual — open a project and a standalone file, choose Close All Projects.
- **UI-SHELL-016** — Exit shall run the exit sequence (UI-SHELL-060) and shut the app down only when it completes.
  - Tests: manual — modify an editor, choose Exit, choose Cancel, check the app stays open.

### Recent projects

- **UI-SHELL-020** — Open Recent Project shall be visible only while the recent list is not empty, and choosing an entry shall open that project.
  - Tests: manual — open a project, then check File → Open Recent Project.
- **UI-SHELL-021** — When a project is opened or created, the shell shall move its project file path to the top of the recent list and save the preferences.
  - Tests: untested
- **UI-SHELL-022** — When a standalone data file is opened, the recent list shall not change.
  - Tests: manual — open a ROM with Open File... and check the recent list.
- **UI-SHELL-023** (inherited) — The recent list shall keep at most 8 entries, dropping the oldest.
  - Tests: untested
- **UI-SHELL-024** — At startup, the shell shall drop recent entries whose file no longer exists from the displayed list.
  - Tests: untested

### Edit menu

- **UI-SHELL-030** — The Edit menu's Undo, Redo, Cut, Copy, Paste, Delete and Select All items shall run the active editor's `EditCommands`, and shall be disabled while no editor is active.
  - Tests: manual — open an arranger, make a selection, check the Edit menu enablement follows the editor.
- **UI-SHELL-031** — Each Edit menu item shall be enabled from its command's own can-execute state, the same state that gates the editor's hotkey for it.
  - Tests: manual — with an empty undo history check Undo is disabled; draw, check it enables.
- **UI-SHELL-032** — The Edit menu shall display the gestures Ctrl+Z, Ctrl+Y, Ctrl+C, Ctrl+V, Delete and Ctrl+A for Undo, Redo, Copy, Paste, Delete and Select All, and none for Cut.
  - Tests: manual — open the Edit menu.
- **UI-SHELL-033** — Preferences..., at the bottom of the Edit menu, shall open the Preferences dialog.
  - Tests: manual — choose Edit → Preferences....

### View and Help menus

- **UI-SHELL-040** — The View menu's Show Gridlines toggle and Grid Settings... item shall act on the active graphics editor, and shall be disabled while the active editor is not a graphics editor.
  - Tests: manual — switch between a palette tab and an arranger tab and check the View menu.
- **UI-SHELL-041** — View → Set Theme shall offer Light Theme and Dark Theme; choosing one shall apply it immediately and save it to the preferences.
  - Tests: manual — switch theme and restart the app.
- **UI-SHELL-042** — Help → Wiki shall open `https://github.com/stevemonaco/ImageMagitek/wiki` in the system browser on Windows, Linux and macOS.
  - Tests: manual — choose Help → Wiki.
- **UI-SHELL-043** — Help → About TileShop shall show an alert headed "TileShop" with the product version and either "Plugin codecs:" followed by each loaded plugin codec's name, or "No plugin codecs loaded".
  - Tests: manual — choose Help → About TileShop with and without a plugin in `_plugins`.
- **UI-SHELL-044** — The menu bar shall have no Plugins menu.
  - Tests: manual — inspect the menu bar.

### Preferences dialog

- **UI-SHELL-050** — The Preferences dialog shall show the current theme, Jump to Offset base, symmetry tools toggle, gridline and checkerboard primary/secondary colors, and NES master palette.
  - Tests: manual — open Preferences and compare with `preferences.json`.
- **UI-SHELL-051** — The NES master palette list shall contain every `*.json` palette in the `_palettes` folder, sorted case-insensitively, with the palette currently in effect selected.
  - Tests: manual — open Preferences and check the Master Palette list.
- **UI-SHELL-052** — The Reset button shall restore the three grid colors in the dialog to their defaults without closing it.
  - Tests: manual — change a grid color, press Reset.
- **UI-SHELL-053** — When the dialog is accepted, the shell shall apply the theme immediately and save every setting to the preferences; when it is cancelled, nothing shall change.
  - Tests: manual — change settings, Cancel, reopen; then accept and reopen.
- **UI-SHELL-054** — When the chosen NES palette equals the appsettings.json palette, the preferences shall store no NES override.
  - Tests: untested
- **UI-SHELL-055** — The NES master palette shall take effect only after restart, and grid colors and symmetry tools only in editors opened afterwards; the dialog shall say so.
  - Tests: manual — change the NES palette and check an open NES arranger is unchanged until restart.
- **UI-SHELL-056** — At startup, if the NES override names a palette file that does not exist, or one with fewer than 64 entries, then the app shall use the appsettings.json NES palette.
  - Tests: untested

### Exit

- **UI-SHELL-060** — When the app exits, the shell shall prompt to save each modified editor (UI-EDITORS), and when the user does not cancel, close all projects and save the preferences.
  - Tests: manual — modify two editors and choose File → Exit.
- **UI-SHELL-061** — If the user cancels any save prompt during exit, then the app shall stay open with every project still open.
  - Tests: manual — modify an editor, File → Exit, Cancel.
- **UI-SHELL-062** — When the platform requests shutdown, the shell shall run the same exit sequence and cancel the shutdown if the user cancels.
  - Tests: manual — modify an editor and close the window with the title-bar close button; see Open items.

### Status bar

- **UI-SHELL-070** — The status bar shall show, left to right: the active editor's activity color swatch (only when it has one), its activity message, its pending-operation message, and the latest status message.
  - Tests: manual — hover a pixel in Draw mode and check the swatch and message.
- **UI-SHELL-071** — When a Short status message arrives, the status bar shall show it and clear it 2 s later unless a newer message replaces it.
  - Tests: untested
- **UI-SHELL-072** (inherited) — The Short message duration shall be 2 s.
  - Tests: untested
- **UI-SHELL-073** — When an Indefinite status message arrives, the status bar shall show it until another message arrives.
  - Tests: untested
- **UI-SHELL-074** — When a Reset status message arrives, the status bar shall clear the status message.
  - Tests: untested

### Hotkeys

- **UI-SHELL-080** — While an editor is active, a key gesture matching one of that editor's hotkeys shall run the hotkey's command from anywhere in the main window, even when the editor does not have keyboard focus.
  - Tests: manual — focus the project tree, press Ctrl+Z with an arranger tab active.
- **UI-SHELL-081** — While no editor is active, no editor hotkey shall run.
  - Tests: untested
- **UI-SHELL-082** — While a modal dialog is open, no editor hotkey shall run.
  - Tests: manual — open Preferences and press an editor hotkey.
- **UI-SHELL-083** — While a text box has focus, a hotkey without Ctrl, Alt or Meta shall be left to the text box; a chord with one of those modifiers that the text box did not handle shall still run.
  - Tests: manual — type `G` in a toolbar text box and check gridlines do not toggle.
- **UI-SHELL-084** — When the first hotkey matching a gesture cannot execute, the gesture shall do nothing and no later hotkey shall be tried.
  - Tests: untested
- **UI-SHELL-085** — A key already handled by the focused control shall not run a hotkey.
  - Tests: untested

### Modal dialogs

- **UI-SHELL-090** — Alerts, prompts and request dialogs shall open as overlays inside the main window, and a dialog opened from a dialog shall stack above it.
  - Tests: manual — open Preferences; open a nested dialog from any dialog that has one.
- **UI-SHELL-091** — A dialog shall open only after any open context menu has closed, so focus is not stolen.
  - Tests: manual — run a context-menu command that alerts and check focus lands in the dialog.
- **UI-SHELL-092** — A prompt shall show its Cancel, reject (danger style) and accept (default) buttons in that order, each only when the prompt offers it.
  - Tests: manual — trigger the save prompt (Yes/No/Cancel) by closing a modified editor.
- **UI-SHELL-093** — When Enter is pressed in a dialog, the dialog shall run its default option if that option can execute.
  - Tests: manual — open Rename..., type a name, press Enter.
- **UI-SHELL-094** — When Escape is pressed in a dialog that has a cancel option, the dialog shall run that option.
  - Tests: manual — open Preferences, press Escape.
- **UI-SHELL-095** — When a dialog's close button is pressed, the dialog shall cancel its request.
  - Tests: manual — press the X on a prompt and check the action was cancelled.
- **UI-SHELL-096** — When a dialog closes, focus shall return to the element that had it before the dialog opened.
  - Tests: manual — open and close Preferences from the menu, then press an arrow key in the tree.
- **UI-SHELL-097** — A Default-size dialog shall be draggable by its title bar; Large and Full dialogs shall not.
  - Tests: manual — drag the Preferences dialog title bar.

### Startup and build variants

- **UI-SHELL-100** — At startup the app shall load preferences from `%LOCALAPPDATA%/TileShop/preferences.json`, falling back to defaults when the file is missing or unreadable, and apply the saved theme.
  - Tests: untested
- **UI-SHELL-101** — Saving preferences shall write a sibling temporary file first and then replace the real file, and a failed save shall be logged, not shown.
  - Tests: untested
- **UI-SHELL-102** (inherited) — When no preferences exist, the theme shall be Dark and the Jump to Offset base Hexadecimal.
  - Tests: untested
- **UI-SHELL-103** — In Debug builds only, the title bar shall have a "Load FF2" button (`debugLoadButton`) that opens a fixed test project path, and the Avalonia DevTools shall be attachable.
  - Tests: manual — run a Debug build and a Release build and compare title bars.
- **UI-SHELL-104** (inherited) — The app shall log errors only, to a monthly rolling log file.
  - Tests: untested
- **UI-SHELL-105** — Each view model shall be shown with the view registered for its exact type, and an unregistered view model shall render as the text "<type> not registered".
  - Tests: untested

### OS integration

- **UI-SHELL-110** — "Open in Folder" shall reveal the location in Explorer on Windows and do nothing on other platforms.
  - Tests: manual — right-click a node → Open in Folder on Windows.
- **UI-SHELL-111** — The app shall ignore command-line arguments.
  - Tests: untested

## Invariants

- `EditorsViewModel.ActiveEditCommands` is `EditCommands.None` (every command disabled) whenever there is no active editor, so Edit menu items never act on a stale editor through that path.
- The hotkey scope is always the active editor's `Hotkeys`, or empty.
- Every preference change made through the menus or Preferences dialog is written to disk before the command returns.

## Edge cases

- Opening a recent entry whose project is already open shows "Project Open Error" with "File '…' is already open" (LIB-PROJECT-SERVICE); the entry stays.
- Opening a recent entry whose file was deleted after startup shows the open error; the entry is not removed.
- An NES override that is ignored at startup (missing file or fewer than 64 colors) is not reported to the user, and the Preferences dialog still shows the ignored override as selected.
- File → Exit calls `Shutdown`, which may raise the shutdown request again; the second exit sequence finds no modified editors and repeats the project close and preference save harmlessly.
- `ExploreWebLocation` throws for a non-HTTP(S) URI or an unsupported OS; only the fixed wiki URL reaches it.

## Threading and lifetime

- All shell view models are DI singletons living for the app; `ShellViewModel` wires itself into `EditorsViewModel.Shell` and `MenuViewModel.Shell`.
- `HotkeyService` attaches a bubbling KeyDown handler to the main window on `Opened`.
- `StatusViewModel` receives `NotifyStatusMessage` through the default Messenger; its `DispatcherTimer` runs on the UI thread.
- `InteractionService` finds the `RootDialogHost` in any open window; dialogs are awaited on the UI thread.
- `MenuViewModel` subscribes to `ProjectService.ProjectOpened` for the recent list and never unsubscribes (both live for the app).

## Decisions

- **Edit menu commands come from the editor.** Each editor exposes an `EditCommands` record holding the same command instances its `Hotkeys` dispatch, so menu enablement and hotkeys share one can-execute state. Reason: every implemented feature must be reachable without knowing a hotkey, and two copies of the enablement rules would drift. Cut is Copy plus element delete, so it is a menu item only, with no Ctrl+X hotkey or displayed gesture.
- **Menu gestures are display only.** `InputGesture` on a MenuItem only labels it; the key is dispatched by `HotkeyService` from the active editor's hotkeys. Reason: hotkeys must be scoped to the active editor, which a window-level MenuItem gesture cannot express.
- **Hotkeys dispatch window-wide but defer to text boxes.** Plain-key hotkeys are skipped while a TextBox has focus; chords still run. Reason: tool hotkeys must work without clicking the canvas, but typing a number into a toolbar box must not trigger tools.
- **No Plugins menu for 1.0.** Loaded plugin codecs are listed in About instead, so users can confirm a plugin loaded. Rejected: a plugin manager UI (non-goal for 1.0).
- **Preferences dialog covers only persisted settings.** Theme, grid colors, numeric base, symmetry tools and NES master palette. Reason: settings reachable only through files users have never seen are not really user-facing. The NES palette is a nullable `UserPreferences.NesPalette` override of appsettings.json applied at bootstrap, so it needs a restart; the list shows every JSON palette in `_palettes`, not only 64-entry ones.
- **Status bar uses one reusable 2 s timer.** Short restarts it, Indefinite stops it, Reset clears the message. Reason: the earlier `async void` handler dropped Indefinite and Reset messages.
- **Debug load button is Debug-only.** The button, `DebugLoad` and the hardcoded `D:\` path are compiled only in Debug builds; the button is created in code and keeps the `debugLoadButton` name so automation recipes still find it. Rejected: shipping a hidden button with a hardcoded path.
- **Standalone files are not remembered.** The recent list follows `ProjectOpened` and skips standalone trees; standalone files are not restored on next launch.
- **Dialogs are in-window overlays.** All dialogs go through `IInteractionService` and the `RootDialogHost`, so view models never touch windows and hotkeys can be suspended while one is open.

## Non-goals

- Customizable keybindings, saving and restoring the dock layout, localization (backlog).
- A plugin manager.
- Opening files from the command line or by dropping them on the window (backlog).

## Open items

- `App.Desktop_ShutdownRequested` is `async void` and sets `e.Cancel` after awaiting the exit sequence, so the cancel cannot take effect once a prompt yields; the window has no `Closing` handler. Closing the window with the title-bar button may skip or ignore the save prompt (UI-SHELL-062). Needs a manual check.
- Escape in a dialog with no cancel option (every alert, e.g. About) animates the dialog out without closing its request, so the overlay may stay in `RootDialogHost` (keeping hotkeys suspended per UI-SHELL-082) and the awaiting command never finishes. Needs a manual check.
- No code sends `Indefinite` or `Reset` status messages; UI-SHELL-073/074 are reachable only in principle.
- The window title is "TileShop.Avalonia" (hidden in the title bar but visible in the taskbar).
- Command-line and OS integration is open (backlog): `Program.Main` passes `args` to Avalonia and nothing reads them; there is no window drop handler.
- The File menu has no Save All and no Save Project As; Save Project As... is only on the project node context menu (UI-PROJECT-TREE).
- Dead code: `ShellView.LoadLayout`, `SyncDialogExtensions` (unused), `MenuViewModel.ExportArrangerToImage`/`ImportArrangerFromImage` (bound only from a commented-out Arranger menu), the commented-out Arranger and Plugins menus, the commented-out `OnUnhandledException` in `Bootstrapper`, `DialogHost.ShowDialogAsync` (no caller found), `IStateViewDriver` (no implementer found), and the `gridLineBrush`/`separatorBrush` resources (unreferenced).
- Dead styling: `Styles/Arranger.axaml` styles `Rectangle.selection`, `Rectangle.paste`, `Rectangle.animatedBorder` and `.arrangerDrag` have no users (only `.arrangerDrop` is used), so `editSelectionFillBrush` and `pasteSelectionFillBrush` are dead too.
- Unhandled exceptions are not caught or shown; the app logs them as fatal at `Program.Main` and terminates.
