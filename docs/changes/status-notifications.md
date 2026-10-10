# Status bar notifications

## Why

TileShop has two ways to tell the user something: a status message that clears after 2 s (UI-SHELL-071), and a modal alert. Some events fit neither:

- Startup issues (a malformed codec XML, a plugin that failed to load or was built for another contract) show a modal alert at every launch until the file is fixed (UI-SHELL-112). They are worth keeping, but not worth interrupting the user for every time.
- Runtime problems the user didn't trigger directly, such as a plugin codec failing to decode ([LIB-PLUGINS](../specs/lib/plugins.md)). A 2 s message is missed, a modal per failed tile is unusable, and logging alone leaves the view blank with no explanation.
- Low-importance events worth a record, such as a background reload after a data file changed.

## What

A notification area at the right end of the status bar. It is a button with an icon and a count of unread notifications. The icon reflects the most severe unread one (info, warning, error) and is neutral when nothing is unread. Clicking it opens a flyout listing notifications newest first. Each entry shows a severity icon, title, time and source, and expands to its detail. A repeated notification shows a ×N count. The flyout has "Clear all" and a per-entry dismiss. Opening it marks everything read, which resets the count, but entries remain until cleared.

Anything in the UI can post a notification through `INotificationService` (TileShop.Shared) from any thread. Posting is silent: no focus change, no sound, no popup. The one exception is an Error posted at runtime, which flashes the icon once. Library code without a UI dependency raises events, and the UI bootstrapper routes them into the service.

The first senders are:

- **Startup issues:** one warning per skipped file, replacing the startup alert. The fatal startup error window (UI-SHELL-113) is unchanged.
- **Plugin codec decode failures:** one warning per codec, collapsed, naming the plugin and the first exception message.

Status messages stay as they are, for immediate feedback on the user's own actions ("Saved", "Press [Enter] to Apply Paste"). The CLI is unchanged: it prints startup issues to the console and logs everything else.

## Decisions

- **A notification area, not more status messages or alerts.** Reason: a 2 s message is missed, and a modal interrupts. A count with a list on demand keeps the information without demanding attention. Rejected: toasts (they interrupt and need positioning across docked or floating windows); a dock pane (takes layout space for something rarely read).
- **Startup issues become notifications; the alert goes away.** This replaces the UI-SHELL decision "Startup issues are reported in one alert per launch". That alert's reason for rejecting the status bar was that the text cleared after 2 s, and notifications persist for the session. The warning icon and count show on every launch until the file is fixed, so the issue stays visible without blocking. Rejected: keeping the alert alongside (two reports of the same thing); showing the alert only when a notification is an error (startup issues are all non-fatal by definition).
- **Collapse repeats.** A notification with the same source and title as an existing one increments its count and refreshes its time instead of adding an entry. Reason: a failing plugin codec fails on every tile it decodes. The collapsed entry keeps the first detail.
- **Session only, capped.** Notifications live in memory and are not written to preferences. The list keeps the newest 200 entries. Everything posted is also logged at its severity, so the log keeps the full history. Rejected: persisting across launches (startup issues repost anyway, and old runtime events go stale).
- **Three severities: Info, Warning, Error.** Only unread notifications count and color the icon. Reason: enough to rank the icon; more levels add nothing the user would act on differently.
- **Runtime errors flash the icon once.** An Error posted after startup pulses the icon (about 1 s, a brief accent-color highlight) and then leaves it in its error state. Startup notifications, Info and Warning never flash. Reason: an error at runtime usually explains something the user is looking at, such as a blank arranger, so it is worth a glance but not a modal. Rejected: fully silent errors; flashing on warnings (a failing plugin would flash constantly before collapsing).
- **Lucide icons through the existing generator.** `bell` (nothing unread), `bell-dot` (unread Info), `triangle-alert` (Warning) and `circle-x` (Error) for the button, and `info`, `triangle-alert` and `circle-x` for the entries. They are added as entries in `tools/Convert-LucideIcons.ps1`, which regenerates `TileShop.UI.Controls/Resources/AppIcons.Lucide.cs`. Reason: the toolbar icons already come from Lucide this way, so the style and themes match. Rejected: an icon font or package (a second icon source).
- **The button is a `Button`, the list a flyout.** Reason: DevTools can click Buttons (CLAUDE.md), and the flyout's contents are verified through bound ViewModel state, since popups are invisible to DevTools.
- **Thread-safe posting, UI-thread state.** `Post` can be called from any thread and marshals to the dispatcher. Reason: plugin decode failures can come from render work off the UI thread.
- **The library raises events; the UI owns notifications.** `ImageMagitek` has no logger and no UI types. Plugin adapters raise a static failure event (LIB-PLUGINS), and the bootstrapper subscribes and posts. Rejected: a notification abstraction in the core library (UI policy in the library).

## Spec changes

- **UI-SHELL:**
  - Add a "Notifications" requirements group: the button's icon by highest unread severity, the unread count (hidden at zero), the newest-first list, expanding an entry to its detail, ×N on collapsed repeats, Clear all, dismiss, opening marks everything read, posting from any thread, the 200-entry cap, collapse by source and title, logging every post, and the one-time flash on a runtime Error.
  - Strike UI-SHELL-112 ("Replaced by notifications"). Add: "When startup records non-fatal issues, the shell shall post one warning notification per issue naming the file and the first line of its reason."
  - UI-SHELL-070 adds the notification area at the right end.
  - Replace the decision "Startup issues are reported in one alert per launch" with the startup decision above, and add the other decisions.
  - `types` add `INotificationService`, `NotificationService`, `Notification`, `NotificationSeverity`, `NotificationsViewModel`; `sources` add their files.
  - `StartupIssueFormatter` is removed, or reduced to formatting a single issue.
- **LIB-PLUGINS:** the decode-failure event: raised once per adapter on its first decode failure, carrying the codec name and exception.
- **CLI-COMMANDS:** unchanged; a note that the CLI has no notification area.

## Tasks

1. **Service and model.** `Notification` (severity, title, detail, source, first and last time, count, read), `NotificationSeverity`, `INotificationService` in TileShop.Shared, and `NotificationService` (collapse, cap, logging, dispatcher marshalling, `Changed` notifications). Registered in DI. Tests: collapse increments count and keeps the first detail; the cap drops the oldest; unread count and highest unread severity; mark read; clear; dismiss; posting from a background thread lands on the dispatcher.
2. **View.** `NotificationsViewModel` and the status bar button with icon, count and flyout (list, expand, dismiss, Clear all). Add the Lucide icons to `tools/Convert-LucideIcons.ps1` and regenerate. The runtime-Error flash is a style animation keyed on a ViewModel flag that resets after it plays. Tests: ViewModel state for icon, count, visibility, and the flash flag (set by a runtime Error, not by startup or by Info/Warning). DevTools: after a post, the button shows the count, and clicking it zeroes the count. Manual: the flyout's contents, expand and dismiss, and the flash, in light and dark themes.
3. **Startup issues.** Post startup issues as notifications and remove the alert. Tests: each issue becomes one warning with file and reason. DevTools: a malformed XML in `_codecs` shows a warning count at launch and no `OverlayDialog`.
4. **Plugin decode failures.** Subscribe to the LIB-PLUGINS decode-failure event in `Bootstrapper` and post a warning per codec. Tests: a throwing sample plugin decoded twice posts one notification with count 2. Manual: a throwing plugin in `_plugins` shows a warning when its arranger is opened.
5. **Close.** Make UI-SHELL and LIB-PLUGINS current, remove the backlog lines resolved, and delete this proposal.

## Open questions

None.
