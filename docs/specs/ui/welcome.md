---
id: UI-WELCOME
title: Welcome screen
project: TileShop.UI
sources:
  - TileShop.UI/Features/Shell/WelcomeViewModel.cs
  - TileShop.UI/Features/Shell/WelcomeView.axaml
  - TileShop.UI/Features/Shell/WelcomeView.axaml.cs
  - TileShop.UI/ViewExtenders/Docking/DockFactory.cs
  - TileShop.UI/_demo
  - tools/DemoArtGenerator
types:
  - WelcomeViewModel
  - DemoArt
  - Scene
tests:
  - WelcomeViewModelTests
  - DemoArtTests
depends:
  - UI-SHELL
  - UI-PROJECT-TREE
  - LIB-ARRANGERS
  - LIB-CODECS
  - LIB-PALETTES
---

# Welcome screen

## Purpose

The welcome screen fills the document area while no editor is open. It shows one of TileShop's demo scenes, a different one on each launch, and offers the ways into the app: open a file, open a project, create a project from a file, or reopen a recent project. Each scene is decoded at runtime from a shipped SNES 4bpp file, so the first screen also exercises the codec, palette and arranger paths.

## Requirements

### Visibility

- **UI-WELCOME-001** — While the document area holds no editor, docked or floating, the shell shall show the welcome screen in the document area.
  - Tests: DevTools — launch and `search` `WelcomeView` with `searchTemplates`; `IsVisible` is true.
- **UI-WELCOME-002** — When an editor opens, the welcome screen shall hide; when the last editor closes, it shall show again.
  - Tests: DevTools — open FF2 from Recent Projects, open a resource: `WelcomeView` `IsVisible` is false; File → Close the editor: it is true again.
- **UI-WELCOME-003** — The welcome screen shall not be a document: it shall have no tab, and it shall never be modified, saved, closed or floated.
  - Tests: DevTools — on launch `search` `DocumentTabStripItem` finds none while `WelcomeView` shows.

### Banner

- **UI-WELCOME-010** — At startup, the welcome screen shall decode the demo file chosen by UI-WELCOME-014 as a sequential arranger: `SNES 4bpp` codec, 32×12 elements from address 0, and a Bgr15 palette of 16 little-endian file colors in the file's last 32 bytes, with index 0 transparent.
  - Tests: `WelcomeViewModelTests.Banner_DecodesDemoFile`, `DemoArtTests.Decodes_WithSnes4bpp_NoIndexAbove15`
- **UI-WELCOME-011** — The banner shall be drawn at the largest whole scale factor from 1 to 4 at which it fits the welcome screen's width, with nearest-neighbor sampling, centered horizontally; when even 1x does not fit, it shall be drawn at 1x and clipped.
  - Tests: `WelcomeViewModelTests.ScaleFactor_LargestWholeFitUpToFour`; manual — resize the window across 256, 512, 768 and 1024 px of document width and check the banner snaps between whole factors.
- **UI-WELCOME-012** — If `_demo` holds no demo file, or the chosen one is missing, unreadable, or fails to decode, then the welcome screen shall show the text "TileShop" in place of the banner and log a warning naming the file (or the folder) and the reason, without an alert.
  - Tests: `WelcomeViewModelTests.MissingDemoFile_NoBannerAndLogs`, `WelcomeViewModelTests.BrokenCodec_NoBanner`, `WelcomeViewModelTests.PickDemoFile_NoFiles_NullAndRotationKept`; DevTools — delete the build output's `_demo`, relaunch: the text title shows and `errorlog` has the warning.
- **UI-WELCOME-013** — Decoding the banner shall not delay the main window: the welcome screen shall show without the banner until decoding completes.
  - Tests: manual
- **UI-WELCOME-014** — Each launch shall show the next `*.bin` file in the application directory's `_demo` folder, in name order, wrapping to the first after the last. The position is the `WelcomeRotation` user preference, saved with the other preferences at exit.
  - Tests: `WelcomeViewModelTests.PickDemoFile_RotatesThroughBinFilesByName`; DevTools — close the main window and relaunch: the banner shows the next scene.

### Actions

- **UI-WELCOME-020** — The welcome screen shall offer the buttons Open File..., Open Project... and New Project from Existing File..., in that order, each running the File menu command of the same name (UI-SHELL-011).
  - Tests: DevTools — `search` `Button` in `WelcomeView`: `OpenFileButton`, `OpenProjectButton`, `NewProjectFromFileButton` in that order, each with its `Command` bound to the `Menu` command of the same name (`props`).
- **UI-WELCOME-021** — While the recent project list is not empty, the welcome screen shall show a Recent Projects section with one button per entry, in recent-list order, labeled with the project file's name without its extension, with the full path as its tooltip. Choosing one shall open that project as UI-SHELL-020 does.
  - Tests: DevTools — with FF2 in the recent list, close all projects, `input Click` the `FF2project` button: the project tree loads. The list and its 8-entry cap are UI-SHELL's (UI-SHELL-023).
- **UI-WELCOME-022** — The welcome screen shall show the app's informational version, as Help → About does (UI-SHELL-043).
  - Tests: DevTools — `props` `Text` of `VersionText` matches About's version.

### Demo file

- **UI-WELCOME-030** — `_demo` shall hold exactly one file per `DemoArt.Scenes` entry, named `NN-Name.bin` by its position, and each shall equal the output of `DemoArt.Generate` for its scene byte for byte.
  - Tests: `DemoArtTests.ShippedFile_MatchesGenerator`, `DemoArtTests.ShippedFiles_AreExactlyTheScenes`
- **UI-WELCOME-031** — Each demo file shall be 12,320 bytes: 384 SNES 4bpp tiles in row-major order for a 256×96 image, then 16 Bgr15 little-endian colors.
  - Tests: `DemoArtTests.ShippedFile_Is12320Bytes`, `DemoArtTests.Palette_IsLast32Bytes_Bgr15_ZeroTransparent`
- **UI-WELCOME-032** — The TileShop build output and published zips shall contain every `_demo/*.bin` file.
  - Tests: manual — check `bin/Debug/net10.0/_demo` after a build and list the published zip (CLI-PUBLISH-017).
- **UI-WELCOME-033** — The first scene in rotation shall be the shopfront street, built on the app icon's shop (UI-SHELL), so a fresh install opens on it. The other scenes need not share the icon's theme.
  - Tests: manual — remove `WelcomeRotation` from `preferences.json` and launch: the shop street shows.

## Invariants

- The welcome screen never holds or opens a project resource; the demo file is read once at startup into memory (`MemoryDataSource`), so no file handle stays open.

## Edge cases

- The recent list drops entries whose file is missing at startup (UI-SHELL-024); an entry deleted after startup shows the open error when chosen, as in the File menu.
- Opening the demo file itself through Open File... works like any binary: it opens as a standalone file with the codec chosen by extension, which defaults to `NES 1bpp` for `.bin` until the user picks `SNES 4bpp`.
- Any 12,320-byte SNES 4bpp file a user drops into `_demo` joins the rotation in name order; a file of another length shows the text title on its turn (UI-WELCOME-012). Adding or removing files shifts which scene comes next.
- A crash or forced kill skips the preference save, so the next launch repeats the same scene.
- A window narrower than 256 pixels clips the banner at 1x (UI-WELCOME-011).
- While only floating editors exist, the docked document area is empty but blank: the welcome screen stays hidden (UI-WELCOME-001).

## Threading and lifetime

- `WelcomeViewModel` is a singleton for the app's lifetime, created with the shell. The banner decodes on a background thread at startup; the bitmap is created and published on the UI thread. The recent list and commands are the shell's (`MenuViewModel`), bound directly, not copied.
- The background decode uses the shared `IColorFactory` and creates its own codec instance.

## Decisions

- **An empty-state view, not a document tab.** The welcome screen is what the document area shows while it holds no editor. Reason: there is nothing to close, save, float or restore, and no "show on startup" preference is needed, because it gets out of the way by itself. Rejected:
  - A closable Welcome tab. It needs dock lifetime handling, a preference to suppress it, and a way to bring it back.
  - A modal start dialog. It blocks the app, and DevTools drive overlays poorly.
- **Shown through Dock's `DocumentDock.EmptyContent`, hidden by the editor count.** The document dock's built-in empty content is set to the `WelcomeViewModel`, which `ViewLocator` resolves to `WelcomeView`; the view hides itself while `EditorsViewModel.Editors` is not empty, which covers floating editors. Reason: no dock lifetime or overlay code. Rejected: a custom overlay over the dock control (duplicates what Dock already does and must track the document area's bounds).
- **Decode the art at runtime from a real SNES 4bpp file.** Reason: the first screen exercises the data source, XML codec, palette file colors and arranger rendering on every launch, and the shipped file doubles as a small demo users can open (Open File..., codec SNES 4bpp). Rejected: a pre-rendered PNG asset (it tests nothing and is not a demo of the format); embedding the bytes as an Avalonia resource (users could not open it).
- **One 16-color palette per scene, stored in its file.** Reason: a single sequential arranger with one palette is the simplest decode, and the 15-color limit is authentic to a single SNES background palette. Storing the palette at the end of the file, as CGRAM-style BGR15, also exercises file color sources. Rejected: several palettes (needs a scattered arranger or a tilemap); a separate palette file (two files to keep in step).
- **A checked-in generator, enforced by a byte-equality test.** `tools/DemoArtGenerator` holds each scene as a `Scene` (name, palette, draw code) built from text pixel maps and procedural drawing on a shared `Canvas`, with the `Title` wordmark common to all, and encodes them through the XML `SNES 4bpp` codec. Reason: the art is reviewable and reproducible as source, and the test stops the binary and the generator from drifting. Encoding through the XML codec also tests encode; a change to that codec's encode fails the test on purpose. Rejected: hand-editing the binary in TileShop as the source of truth (not reviewable in diffs); a generator run by the build (adds a build step to every build for a file that rarely changes).
- **Several scenes, one per launch, in file name order.** Seven scenes ship: Shopfront, Sunset, Seaside, Forest, Peaks, Dungeon and Space. The view model lists `_demo/*.bin` rather than a fixed list, and the generator prefixes each name with its position so name order is rotation order. Reason: a varied first screen without animation, still one decode per launch; the folder listing lets users add their own SNES 4bpp banners. Rejected: a random pick (repeats, and needs a seed to test); all banners in one file (fixed count, and users cannot add one); a scene per platform codec (each platform needs its own palette format and file layout, which multiplies the decode paths for a cosmetic screen).
- **A missing or broken demo file degrades silently to a text title.** Reason: the art is not essential (ARCHITECTURE §6, "Startup degrades; it stops only for essentials"), and its loss does not affect any user data or workflow. It is logged, so a broken install still leaves a trace. Rejected: a startup alert (noise for a cosmetic file); making it essential (fails startup over a picture).
- **Scale by whole factors only.** Reason: pixel art at fractional scales shimmers and blurs; whole-number nearest-neighbor scaling keeps every pixel square. The DIP factor is rounded down to whole physical pixels per source pixel using the window's render scaling, so at 125% display scaling 1x draws 1 physical pixel per source pixel rather than an uneven 1.25. The factor comes from the width the document area offers, because Dock centers empty content at its own size.
- **Bind the shell's commands and recent list.** The view binds `MenuViewModel`'s File commands and `RecentProjectFiles`. Reason: the File menu and the welcome screen cannot drift, and there is no parallel command interface to fake. Recent entries show the file name, not the project root's name, so startup opens no project file. Rejected: copying the list into `WelcomeViewModel` with its own commands (two sources of truth).

## Non-goals

- A "show on startup" preference, tips, news, or links beyond the three actions and the recent list.
- Opening the demo file in an editor from the welcome screen.
- Animation.
- Choosing or pinning a scene from Preferences.

## Open items

- None.
