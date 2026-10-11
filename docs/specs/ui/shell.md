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
  - TileShop.UI/Features/Shell/ExitGate.cs
  - TileShop.UI/Features/Shell/StartupErrorWindow.axaml
  - TileShop.UI/Features/Shell/StartupErrorWindow.axaml.cs
  - TileShop.UI/Features/Shell/StartupIssueNotifier.cs
  - TileShop.UI/Features/Shell/StatusViewModel.cs
  - TileShop.UI/Features/Shell/StatusView.axaml
  - TileShop.UI/Features/Shell/NotificationsViewModel.cs
  - TileShop.UI/Assets/tsicon.ico
  - TileShop.UI/Assets/tslogo.png
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
  - TileShop.Shared/Models/Notification.cs
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
  - ExitGate
  - StartupErrorWindow
  - StartupIssueNotifier
  - DialogKeyAction
  - INotificationService
  - NotificationService
  - Notification
  - NotificationSeverity
  - NotificationsViewModel
tests:
  - ExitGateTests
  - StartupIssueNotifierTests
  - DialogKeyActionTests
  - NotificationServiceTests
  - NotificationsViewModelTests
  - PluginDecodeNotificationTests
depends:
  - LIB-PROJECT-SERVICE
  - LIB-PALETTES
  - LIB-CODECS
  - UI-EDITORS
  - UI-PROJECT-TREE
  - UI-WELCOME
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
- **UI-SHELL-115** — The main window's title, shown in the taskbar, shall be "TileShop".
  - Tests: DevTools — `props` `Title` on the `ShellView` window.
- **UI-SHELL-139** — While the document area holds no editor, it shall show the welcome screen (UI-WELCOME).
  - Tests: DevTools — launch and `search` `WelcomeView` with `searchTemplates`; it is visible.

### File menu

- **UI-SHELL-010** — The File menu shall list, in order: New Empty Project..., New Project from Existing File..., Open File..., Open Project..., Open Recent Project, Close <active editor>, Close All Projects, Save <active editor>, Save All, Exit.
  - Tests: manual — open the File menu.
- **UI-SHELL-116** — Save All shall be enabled while any open editor is modified, and shall save every modified editor without prompting, then save each containing project once.
  - Tests: `EditorsViewModelTests.SaveAllAsync_SavesWithoutPromptingOncePerProject`; DevTools — Save All's `IsEnabled` is False with no modified editor.
- **UI-SHELL-011** — New Empty Project..., New Project from Existing File..., Open File... and Open Project... shall run the project tree's commands of the same purpose (UI-PROJECT-TREE).
  - Tests: manual — run each from the File menu.
- **UI-SHELL-012** — While no editor is active, the Close <name> and Save <name> items shall be hidden.
  - Tests: manual — start the app with no editors and open the File menu.
- **UI-SHELL-013** — Save <name> shall be enabled only while the active editor is modified, and shall save that editor.
  - Tests: manual — modify an arranger, check Save is enabled, choose it, check `*` clears.
- **UI-SHELL-014** — Close <name> shall close the active editor with the save prompt of UI-EDITORS.
  - Tests: manual — modify an editor, choose Close <name>, check the prompt.
- **UI-SHELL-015** — Close All Projects shall be enabled while any project or standalone file is open, and shall close each open root in turn, stopping at the first one the user cancels.
  - Tests: manual — open a project and a standalone file, choose Close All Projects.
- **UI-SHELL-016** — Exit shall request the main window to close, running the exit sequence (UI-SHELL-060).
  - Tests: DevTools — with no modified editor, File → Exit ends the process; manual — modify an editor, choose Exit, choose Cancel, check the app stays open.

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
- ~~**UI-SHELL-041**~~ — Removed: the theme is chosen in the Preferences dialog (UI-SHELL-050), so View → Set Theme duplicated it.
- **UI-SHELL-042** — Help → Wiki shall open `https://github.com/stevemonaco/ImageMagitek/wiki` in the system browser on Windows, Linux and macOS.
  - Tests: manual — choose Help → Wiki.
- **UI-SHELL-043** — Help → About TileShop shall show an alert headed "TileShop" with the app's informational version, in Debug, Release and single-file published builds, and either "Plugin codecs:" followed by each loaded plugin codec's name, or "No plugin codecs loaded".
  - Tests: manual — choose Help → About TileShop with and without a plugin in `_plugins`.
- **UI-SHELL-044** — The menu bar shall have no Plugins menu.
  - Tests: manual — inspect the menu bar.

### Preferences dialog

- **UI-SHELL-050** — The Preferences dialog shall show the current theme, Jump to Offset base, Foreign Color Display (Hex or Channels, default Hex; applies at once), symmetry tools toggle, gridline and checkerboard primary/secondary colors, default palette and NES master palette.
  - Tests: manual — open Preferences and compare with `preferences.json`.
- **UI-SHELL-051** — The NES master palette list shall contain every `*.json` palette in the `_palettes` folder, sorted case-insensitively, with the palette currently in effect selected.
  - Tests: manual — open Preferences and check the Master Palette list.
- **UI-SHELL-120** — The default palette list shall contain the appsettings.json global palettes in settings order, with the default currently in effect selected.
  - Tests: manual — open Preferences and check the Default Palette list.
- **UI-SHELL-114** — The NES palette list in the Preferences dialog shall come from the application directory's `_palettes`.
  - Tests: manual — launch with another working directory and open Preferences.
- **UI-SHELL-052** — The Reset button shall restore the three grid colors in the dialog to their defaults without closing it.
  - Tests: manual — change a grid color, press Reset.
- **UI-SHELL-053** — When the dialog is accepted, the shell shall apply the theme immediately and save every setting to the preferences; when it is cancelled, nothing shall change.
  - Tests: manual — change settings, Cancel, reopen; then accept and reopen.
- **UI-SHELL-054** — When the chosen NES palette equals the appsettings.json palette, the preferences shall store no NES override; when the chosen default palette is the first appsettings.json global palette, the preferences shall store no default override.
  - Tests: untested
- **UI-SHELL-055** — The NES master palette and the default palette shall take effect only after restart, and grid colors and symmetry tools only in editors opened afterwards; the dialog shall say so.
  - Tests: manual — change the NES palette and check an open NES arranger is unchanged until restart.
- **UI-SHELL-056** — At startup, the app shall pass the NES override to the palette store, which falls back to the appsettings.json NES palette when the override is unusable (LIB-PALETTES), and the fallback shall appear in the startup notifications.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_UnusableOverride_FallsBackAndRecordsIssue`
- **UI-SHELL-121** — At startup, the app shall pass the default palette override to the palette store, which makes that global palette the default (LIB-PALETTES), and an override naming no loaded global palette shall appear in the startup notifications.
  - Tests: `BootstrapServiceTests.CreatePaletteStore_DefaultOverride_MovesPaletteFirst`, `BootstrapServiceTests.CreatePaletteStore_UnknownDefaultOverride_KeepsSettingsOrderAndRecordsIssue`

### Exit

- **UI-SHELL-060** — When the app exits, the shell shall prompt to save each modified editor (UI-EDITORS), and when the user does not cancel, close all projects and save the preferences.
  - Tests: manual — modify two editors and choose File → Exit.
- **UI-SHELL-061** — If the user cancels any save prompt during exit, then the app shall stay open with every project still open.
  - Tests: manual — modify an editor, File → Exit, Cancel.
- **UI-SHELL-062** — When the main window is asked to close (title-bar button, Alt+F4, File → Exit) or the platform requests shutdown, the shell shall cancel the request before prompting, run the exit sequence, and shut the app down only when the sequence completes.
  - Tests: `ExitGateTests.Request_First_StartsSequence`, `ExitGateTests.Request_AfterCancelledSequence_StartsAgain`, `ExitGateTests.Request_AfterConfirmation_Proceeds`; manual — modify an editor, click the title-bar X, Cancel, check the app and edit remain; repeat with Yes; repeat with Alt+F4.
- **UI-SHELL-117** — While the exit sequence is running, further close or shutdown requests shall be ignored.
  - Tests: `ExitGateTests.Request_WhileRunning_IsIgnored`
- **UI-SHELL-118** — When the exit sequence completes, the app shall shut down even while floating editor windows are open.
  - Tests: manual — float a tab, exit.

### Status bar

- **UI-SHELL-070** — The status bar shall show, left to right: the active editor's activity color swatch (only when it has one), its activity message, its pending-operation message, the latest status message, and at the right end the notification area.
  - Tests: manual — hover a pixel in Draw mode and check the swatch and message; DevTools — in an arranger, Edit → Select All, Copy, Paste, then the status bar shows "Press [Enter] to Apply Paste".
- **UI-SHELL-071** — When a Short status message arrives, the status bar shall show it and clear it 2 s later unless a newer message replaces it.
  - Tests: untested
- **UI-SHELL-072** (inherited) — The Short message duration shall be 2 s.
  - Tests: untested
- **UI-SHELL-073** — When an Indefinite status message arrives, the status bar shall show it until another message arrives.
  - Tests: untested
- **UI-SHELL-074** — When a Reset status message arrives, the status bar shall clear the status message.
  - Tests: untested

### Notifications

- **UI-SHELL-122** — The status bar's notification button shall show a neutral bell when nothing is unread, a bell with a dot when the most severe unread notification is Info, a warning triangle when it is Warning, and a crossed circle when it is Error, colored primary, warning and danger respectively.
  - Tests: `NotificationsViewModelTests.IconSeverityAndCount_FollowPosts`; manual — light and dark themes.
- **UI-SHELL-123** — The button shall show the number of unread notifications, hidden when it is zero.
  - Tests: `NotificationsViewModelTests.IconSeverityAndCount_FollowPosts`; DevTools — `props IsVisible` on `NotificationCount`.
- **UI-SHELL-138** — The notification button shall be hidden while there are no notifications, read or unread.
  - Tests: DevTools — `props IsVisible` on `NotificationsButton` before and after a startup warning.
- **UI-SHELL-124** — Clicking the button shall open a flyout listing the notifications newest first, each with a severity icon, title, source and the time of its latest post.
  - Tests: `NotificationServiceTests.Post_SameSourceAndTitle_IncrementsCountKeepsFirstDetail`; manual — open the flyout.
- **UI-SHELL-125** — Expanding an entry shall show its detail as selectable, wrapped text.
  - Tests: manual — expand an entry and select its text.
- **UI-SHELL-126** — An entry that collapsed repeats shall show ×N, N being its count; an entry posted once shall show no count.
  - Tests: manual — trigger a collapsed repeat and open the flyout.
- **UI-SHELL-127** — The flyout's Clear all button shall remove every notification; while the list is empty it is disabled and the flyout shows "No notifications".
  - Tests: `NotificationServiceTests.ClearAll_Empties`; manual — press Clear all.
- **UI-SHELL-128** — Each entry's dismiss button shall remove that entry only.
  - Tests: `NotificationServiceTests.Dismiss_RemovesOne`; manual — dismiss one entry.
- **UI-SHELL-129** — Opening the flyout shall mark every notification read, resetting the count and the icon, and keep the entries.
  - Tests: `NotificationServiceTests.MarkAllRead_ZeroesCountKeepsEntries`, `NotificationsViewModelTests.Open_ZeroesCountAndClearsFlash`; DevTools — `input Click` on `NotificationsButton`, then `NotificationCount` is hidden.
- **UI-SHELL-130** — A notification may be posted from any thread; the list, counts and icon shall change only on the UI thread.
  - Tests: `NotificationServiceTests.Post_FromBackgroundThread_AppliesOnlyThroughDispatch`
- **UI-SHELL-131** — The list shall keep at most the newest 200 entries, dropping the oldest.
  - Tests: `NotificationServiceTests.Post_OverCap_DropsOldest`
- **UI-SHELL-132** — A post with the same source and title as an existing entry shall increment its count, refresh its time, mark it unread and move it to the top, keeping the first detail.
  - Tests: `NotificationServiceTests.Post_SameSourceAndTitle_IncrementsCountKeepsFirstDetail`
- **UI-SHELL-133** — Every post shall be logged at its severity (Info as Information), including collapsed repeats.
  - Tests: `NotificationServiceTests.Post_LogsAtSeverity`
- **UI-SHELL-134** — When a new Error entry is added, the button shall play a one-time highlight of about 1 s; Info, Warning, collapsed repeats and startup issues shall not.
  - Tests: `NotificationsViewModelTests.Post_NewError_SetsIsFlashing`, `NotificationsViewModelTests.Post_WarningInfoOrCollapsedError_DoesNotFlash`, `NotificationsViewModelTests.StartupIssues_DoNotFlash`; manual — the highlight in light and dark themes.
- **UI-SHELL-135** — Only unread notifications shall count toward the number and the icon's severity.
  - Tests: `NotificationServiceTests.UnreadCountAndHighestSeverity_FollowUnreadOnly`

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
- **UI-SHELL-094** — When Escape is pressed in a dialog, the dialog shall run its cancel option if it has one that can execute; otherwise, if it has no cancel option, it shall be dismissed as by its close button (UI-SHELL-095).
  - Tests: `DialogKeyActionTests.Escape_ExecutableCancel_RunsIt`, `DialogKeyActionTests.Escape_NonExecutableCancel_DoesNothing`, `DialogKeyActionTests.Escape_NoCancel_Dismisses`, `DialogKeyActionTests.Enter_NonExecutableDefault_DoesNothing`; DevTools — Help → About, `input KeyDown Escape`, `search OverlayDialog` finds none and `G` toggles gridlines in an open arranger.
- **UI-SHELL-119** — A dialog shall leave the screen only when its request has closed; if the request refuses to close, then the dialog shall stay visible and interactive.
  - Tests: manual — make an import commit fail (read-only ROM), press Enter on Import, check the dialog stays visible after the error alert closes.
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
- **UI-SHELL-103** — In Debug builds only, the Avalonia DevTools shall be attachable.
  - Tests: manual — attach DevTools to a Debug build and a Release build; only the Debug build connects.
- **UI-SHELL-104** (inherited) — The app shall log Warning and higher messages to a monthly rolling log file in `%LOCALAPPDATA%/TileShop/`.
  - Tests: manual — launch with a malformed codec file in `_codecs` and check the warning in the log there.
- ~~**UI-SHELL-112**~~ — Replaced by notifications (UI-SHELL-136).
- **UI-SHELL-136** — When startup records non-fatal issues, the shell shall post, when it is created, one Warning notification per issue with source "Startup", titled with the file name and the first line of its reason, and with the full reason and path as its detail; no alert shall open.
  - Tests: `StartupIssueNotifierTests.Post_EachIssue_OneWarningNamingFileAndFirstLine`; DevTools — put a malformed XML file in the build output's `_codecs`, launch, `search NotificationCount` shows 1 and `search OverlayDialog` (with templates) finds none.
- **UI-SHELL-137** — When a plugin codec instance's decode first fails (LIB-PLUGINS-013), the shell shall post a Warning notification with source "Plugins" naming the codec, with the exception message as its detail, so failures of several instances of one codec collapse into one entry whose count is the number of failed instances, not of failed tiles.
  - Tests: `PluginDecodeNotificationTests.DecodeFailed_TwoAdapters_PostsOneWarningWithCountTwo`; manual — put a throwing plugin in `_plugins` and open an arranger that uses it.
- **UI-SHELL-113** — If bootstrapping throws, then the app shall show a startup error window with the message and the log folder instead of the main window, and exit when it is closed.
  - Tests: DevTools — rename `_palettes/DefaultNes.json` (the NES master palette, an essential) in the build output, launch, find `StartupErrorWindow`, click Close and check the process ends.
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
- An NES override that is ignored at startup (missing file or fewer than 64 colors) is reported in the startup notifications, but the Preferences dialog still shows the ignored override as selected.
- `desktop.Shutdown()` after a confirmed exit re-raises the window's close and may raise the shutdown request; the confirmed exit state lets both through without a second sequence.
- While a dialog animates out (100 ms) the overlay is still in `RootDialogHost`, so `HasOpenDialog` stays true until the awaiting command resumes; the dialog ignores keys and pointer input meanwhile.
- `ExploreWebLocation` throws for a non-HTTP(S) URI or an unsupported OS; only the fixed wiki URL reaches it.

## Threading and lifetime

- All shell view models are DI singletons living for the app; `ShellViewModel` wires itself into `EditorsViewModel.Shell` and `MenuViewModel.Shell`.
- `HotkeyService` attaches a bubbling KeyDown handler to the main window on `Opened`.
- `StatusViewModel` receives `NotifyStatusMessage` through the default Messenger; its `DispatcherTimer` runs on the UI thread.
- `InteractionService` finds the `RootDialogHost` in any open window; dialogs are awaited on the UI thread.
- `NotificationService` is a DI singleton created in `TileShopBootstrapper.ConfigureServices`. `Post` logs on the caller's thread and applies its change through the injected dispatcher (`Dispatcher.UIThread`, run inline when already on it), so collection and property changes are raised on the UI thread. The bootstrapper's `CodecPluginEvents.DecodeFailed` subscription lives for the app.
- `MenuViewModel` subscribes to `ProjectService.ProjectOpened` for the recent list and never unsubscribes (both live for the app).

## Decisions

- **Edit menu commands come from the editor.** Each editor exposes an `EditCommands` record holding the same command instances its `Hotkeys` dispatch, so menu enablement and hotkeys share one can-execute state. Reason: every implemented feature must be reachable without knowing a hotkey, and two copies of the enablement rules would drift. Cut is Copy plus element delete, so it is a menu item only, with no Ctrl+X hotkey or displayed gesture.
- **Menu gestures are display only.** `InputGesture` on a MenuItem only labels it; the key is dispatched by `HotkeyService` from the active editor's hotkeys. Reason: hotkeys must be scoped to the active editor, which a window-level MenuItem gesture cannot express.
- **Hotkeys dispatch window-wide but defer to text boxes.** Plain-key hotkeys are skipped while a TextBox has focus; chords still run. Reason: tool hotkeys must work without clicking the canvas, but typing a number into a toolbar box must not trigger tools.
- **No Plugins menu for 1.0.** Loaded plugin codecs are listed in About instead, so users can confirm a plugin loaded. Rejected: a plugin manager UI (non-goal for 1.0).
- **Preferences dialog covers only persisted settings.** Theme, grid colors, numeric base, symmetry tools, default palette and NES master palette. Reason: settings reachable only through files users have never seen are not really user-facing. The NES palette is a nullable `UserPreferences.NesPalette` override of appsettings.json applied at bootstrap, so it needs a restart; the list shows every JSON palette in `_palettes`, not only 64-entry ones. The default palette works the same way through `UserPreferences.DefaultPalette`, listing only the global palettes, because projects and codecs resolve the default among them. View → Set Theme was removed because Preferences already holds the theme; Show Gridlines and Grid Settings stay in View because they act on the open editor, while Preferences only sets defaults for new editors. Rejected: applying a new default without restart (codecs, project loading and saving read the default once at bootstrap; a reload mechanism is in the backlog).
- **Status bar uses one reusable 2 s timer.** Short restarts it, Indefinite stops it, Reset clears the message. Reason: the earlier `async void` handler dropped Indefinite and Reset messages.
- **No debug load button.** The Debug-only "Load FF2" title-bar button was removed; the welcome screen's Recent Projects list (UI-WELCOME-021) reopens a test project in one click, and DevTools can click it. Rejected: keeping a hardcoded `D:\` path in the source for a shortcut the recent list already gives.
- **Standalone files are not remembered.** The recent list follows `ProjectOpened` and skips standalone trees; standalone files are not restored on next launch.
- **Dialogs are in-window overlays.** All dialogs go through `IInteractionService` and the `RootDialogHost`, so view models never touch windows and hotkeys can be suspended while one is open.
- **Cancel first, then ask.** The window's `Closing` handler and the lifetime's `ShutdownRequested` handler both set `Cancel = true` synchronously unless the exit was already confirmed, then start the exit sequence; on success the sequence marks the exit confirmed and calls `Shutdown`, which also closes floating editor windows. Reason: Avalonia reads `Cancel` when the handler returns, so no awaited answer can reach it (the earlier `async void` handler lost Cancel). Rejected: blocking on the prompt with a nested `Dispatcher.UIThread.MainLoop` inside `Closing` (nested loops during shutdown re-enter close and dialog handlers); `ShutdownMode.OnExplicitShutdown` alone (still needs the `Closing` cancel).
- **The exit state is a plain, tested class.** `ExitGate` holds idle / running / confirmed and answers whether a close may proceed and whether to start the sequence; `ShellView` and `App` consult it. Reason: the re-entrancy rules are the bug-prone part and cannot be exercised without a window otherwise. Rejected: flags spread over `App`, `ShellView` and `MenuView`.
- **One exit path.** File → Exit asks the main window to close. Rejected: keeping `Exit_Click`'s own sequence plus `Shutdown(0)`.
- **Escape dismisses like the close button.** With no usable cancel option, Escape raises `Dismiss`, which cancels the request (UI-SHELL-095); with a cancel option that cannot execute, it does nothing. An alert dismissed this way returns as acknowledged; a prompt without a Cancel choice returns `PromptResult.Cancel`. Reason: the close button already has these semantics, and users expect Escape to close a message box. Rejected: Escape doing nothing on an alert; Escape running an alert's Ok (wrong for a future prompt whose only option is destructive). The key rule lives in `DialogKeyAction` so it is unit tested.
- **The host animates out, not the key handler.** `OverlayDialog` only raises requests (run an option, `Dismiss`); `DialogHost` animates the overlay out and removes it when the request reports `Closed`, then resumes the caller. Reason: closing is the request's decision, and the earlier bugs were all the view closing without it. Rejected: patching each branch of `OnKeyDown`. Dialogs do not animate in (the in-animation was reachable only from the removed `ShowAsync`; backlog).
- **Save All has no hotkey.** Window-wide hotkeys belong to the active editor (docs/ARCHITECTURE.md), and Ctrl+Shift+S is not in any editor's set; a window-level hotkey category is out of scope.
- **Save Project As stays on the project node.** With several projects open a File-menu item has no unambiguous target (UI-PROJECT-TREE). Recommended by the proposal; pending the user's confirmation.
- **A notification area, not more status messages or alerts.** Events the user did not directly trigger (startup issues, plugin decode failures) go to a count-and-list button at the right end of the status bar. Reason: a 2 s status message is missed and a modal interrupts; a count with a list on demand keeps the information without demanding attention. Status messages stay for immediate feedback on the user's own actions. Rejected: toasts (they interrupt and need positioning across docked or floating windows); a dock pane (layout space for something rarely read).
- **Startup issues become notifications; the alert went away.** One Warning per skipped file, posted when the shell view model is built, replacing the earlier one-alert-per-launch report. That alert rejected the status bar because its text cleared after 2 s; notifications persist for the session, and the warning icon and count reappear every launch until the file is fixed, so the issue stays visible without blocking. Rejected: keeping the alert alongside (two reports of the same thing); showing the alert only for errors (startup issues are non-fatal by definition); log only; remembering seen issues in preferences (more state for a file that is still broken).
- **Collapse repeats by source and title.** A repeat increments the count, refreshes the time and keeps the first detail. Reason: a failing plugin codec fails on every tile it decodes. Each plugin adapter raises its failure once (LIB-PLUGINS-013), so a plugin entry's count is the number of codec instances that failed.
- **Session only, capped at 200.** Notifications live in memory, not preferences, and every post is also logged at its severity, so the log keeps every Warning and Error (the UI log's minimum level is Warning, UI-SHELL-104, so Info posts are not kept). Reason: startup issues repost anyway, and old runtime events go stale. `BootstrapService` already logs each startup issue and decode failure, so those are logged twice; accepted because both are rare. Rejected: persisting across launches.
- **Three severities: Info, Warning, Error.** Only unread notifications count and color the icon. Reason: enough to rank the icon; more levels add nothing the user would act on differently.
- **New Error entries flash the icon once.** The view model sets `IsFlashing` when a new Error entry is added (not a collapsed repeat), and a style animation fades a highlight behind the button in and out over about 1 s; opening the flyout clears the flag. Reason: a runtime error usually explains something the user is looking at, such as a blank arranger, so it is worth a glance but not a modal. It keys on new Error entries rather than tracking a startup phase because startup only posts Warnings. Opacity is animated on a pre-colored border because animation keyframes may not resolve a DynamicResource brush. Rejected: fully silent errors; flashing on warnings (a failing plugin would flash before collapsing).
- **Lucide icons through the existing generator.** `bell`, `bell-dot`, `info`, `triangle-alert` and `circle-x` are entries in `tools/Convert-LucideIcons.ps1`. Reason: the toolbar icons come from Lucide the same way, so style and themes match. Rejected: an icon font or package (a second icon source).
- **The button is a Button with a Flyout.** Reason: DevTools can click Buttons (CLAUDE.md); the flyout is a popup DevTools cannot see, so its contents are verified through bound view model state.
- **Thread-safe posting, UI-thread state.** `NotificationService` lives in TileShop.Shared without Avalonia types and takes an `Action<Action>` dispatcher. Reason: plugin decode failures can come from render work off the UI thread, and the injected dispatcher keeps the service unit-testable.
- **The library raises events; the UI owns notifications.** `ImageMagitek` has no logger and no UI types, so plugin adapters raise `CodecPluginEvents.DecodeFailed` (LIB-PLUGINS) and the bootstrapper subscribes and posts through `TileShopBootstrapper.PostDecodeFailure`. Rejected: a notification abstraction in the core library (UI policy in the library).
- **A fatal startup error shows its own window.** When bootstrap throws (any exception, not only `BootstrapException`), `App` sets the main window to a minimal `StartupErrorWindow` (message, log folder, Close) created directly, not through DI or `ViewLocator`, since neither exists yet; closing it exits. Reason: the overlay dialog host needs the shell, which cannot be built without the bootstrapped services. Rejected: a third-party message-box package; exiting with only a log line (the earlier behavior).
- **The app icon is a 16x16 pixel shopfront, generated with the demo art.** A shop with a blue and white awning, an orange wall, a tiled window and a door. `tools/DemoArtGenerator` (`Logo`) holds the pixel map and writes `Assets/tsicon.ico` (PNG frames from 16 to 256 px, whole-factor upscales), `tslogo.svg` and the 32 px `tslogo.png` shown in the title bar with nearest-neighbor sampling. Reason: the earlier T and S built from 16-unit tiles blurred into a smudge at 16 px, and the shop reads as "Tile Shop" at taskbar size. One source keeps the window icon, title bar mark and SVG in step. Rejected: keeping the T/S tiles redrawn for small sizes, a TS monogram badge, a tile grid with a lifted tile, and shop and grid hybrids (chosen from a concept round); a hand-drawn Avalonia rectangle view (the earlier `AppIconView`, a second copy that drifted from the `.ico`).
- **The UI log lives in `%LOCALAPPDATA%/TileShop/` and logs Warning and above.** Next to `preferences.json`. Reason: the application directory may be read-only under Program Files, and the working directory scattered log files wherever the app was launched; skipped files are warnings, so an Error minimum would hide the full reasons behind the startup notifications. The monthly rolling name carries a date suffix, so the error window names the folder. Rejected: the application directory; keeping Error.

## Non-goals

- Customizable keybindings, saving and restoring the dock layout, localization (backlog).
- A plugin manager.
- Opening files from the command line or by dropping them on the window (backlog).

## Open items

- UI-SHELL-104 changed an `(inherited)` requirement (Warning instead of Error, `%LOCALAPPDATA%/TileShop/` instead of the working directory) as recommended when startup tolerance was decided; the marker stays until the user confirms it.
- Overlay dialogs do not animate in.
- No code sends `Indefinite` or `Reset` status messages; UI-SHELL-073/074 are reachable only in principle.
- Command-line and OS integration is open (backlog): `Program.Main` passes `args` to Avalonia and nothing reads them; there is no window drop handler.
- Dead code: `ShellView.LoadLayout`, `SyncDialogExtensions` (unused), `MenuViewModel.ExportArrangerToImage`/`ImportArrangerFromImage` (bound only from a commented-out Arranger menu), the commented-out Arranger and Plugins menus, the commented-out `OnUnhandledException` in `Bootstrapper`, `IStateViewDriver` (no implementer found), and the `gridLineBrush`/`separatorBrush` resources (unreferenced).
- Dead styling: `Styles/Arranger.axaml` styles `Rectangle.selection`, `Rectangle.paste`, `Rectangle.animatedBorder` and `.arrangerDrag` have no users (only `.arrangerDrop` is used), so `editSelectionFillBrush` and `pasteSelectionFillBrush` are dead too.
- Unhandled exceptions after startup are not caught or shown; the app logs them as fatal at `Program.Main` and terminates. Exceptions during startup show the startup error window (UI-SHELL-113).
