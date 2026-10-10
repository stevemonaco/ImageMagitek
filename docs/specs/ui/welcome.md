---
id: UI-WELCOME
title: Welcome screen
status: draft
project: TileShop.UI
sources:
  - TileShop.UI/Features/Shell/WelcomeViewModel.cs
  - TileShop.UI/Features/Shell/WelcomeView.axaml
  - TileShop.UI/_demo/TileShopDemo.bin
  - Tools/DemoArtGenerator
types:
  - WelcomeViewModel
  - DemoArt
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

The welcome screen fills the document area while no editor is open. It shows TileShop's demo art and offers the ways into the app: open a file, open a project, create a project from a file, or reopen a recent project. The art is decoded at runtime from a shipped SNES 4bpp file, so the first screen also exercises the codec, palette and arranger paths. The change proposal is [welcome-screen](../../changes/welcome-screen.md).

## Requirements

### Visibility

- **UI-WELCOME-001** — While the document area holds no editor, docked or floating, the shell shall show the welcome screen in the document area.
  - Tests: untested
- **UI-WELCOME-002** — When an editor opens, the welcome screen shall hide; when the last editor closes, it shall show again.
  - Tests: untested
- **UI-WELCOME-003** — The welcome screen shall not be a document: it shall have no tab, and it shall never be modified, saved, closed or floated.
  - Tests: untested

### Banner

- **UI-WELCOME-010** — At startup, the welcome screen shall decode `_demo/TileShopDemo.bin` from the application directory as a sequential arranger: `SNES 4bpp` codec, 32×12 elements from address 0, and a Bgr15 palette of 16 little-endian file colors in the file's last 32 bytes, with index 0 transparent.
  - Tests: untested
- **UI-WELCOME-011** — The banner shall be drawn at the largest whole scale factor from 1 to 4 at which it fits the welcome screen's width, with nearest-neighbor sampling, centered horizontally; when even 1x does not fit, it shall be drawn at 1x and clipped.
  - Tests: untested
- **UI-WELCOME-012** — If the demo file is missing, unreadable, or fails to decode, then the welcome screen shall show the text "TileShop" in place of the banner and log a warning naming the file and the reason, without an alert.
  - Tests: untested
- **UI-WELCOME-013** — Decoding the banner shall not delay the main window: the welcome screen shall show without the banner until decoding completes.
  - Tests: untested

### Actions

- **UI-WELCOME-020** — The welcome screen shall offer the buttons Open File..., Open Project... and New Project from Existing File..., in that order, each running the File menu command of the same name (UI-SHELL-011).
  - Tests: untested
- **UI-WELCOME-021** — While the recent project list is not empty, the welcome screen shall show a Recent Projects section with one button per entry, in recent-list order, labeled with the project name, with the full path as its tooltip. Choosing one shall open that project as UI-SHELL-020 does.
  - Tests: untested
- **UI-WELCOME-022** — The welcome screen shall show the app's informational version, as Help → About does (UI-SHELL-043).
  - Tests: untested

### Demo file

- **UI-WELCOME-030** — The shipped demo file shall equal the output of `DemoArt.Generate()` byte for byte.
  - Tests: untested
- **UI-WELCOME-031** — The demo file shall be 12,320 bytes: 384 SNES 4bpp tiles in row-major order for a 256×96 image, then 16 Bgr15 little-endian colors.
  - Tests: untested
- **UI-WELCOME-032** — The TileShop build output and published zips shall contain `_demo/TileShopDemo.bin`.
  - Tests: untested

## Invariants

- The welcome screen never holds or opens a project resource; the demo file is read once at startup through its own data source and closed.

## Edge cases

- The recent list drops entries whose file is missing at startup (UI-SHELL-024); an entry deleted after startup shows the open error when chosen, as in the File menu.
- Opening the demo file itself through Open File... works like any binary: it opens as a standalone file with the codec chosen by extension, which defaults to `NES 1bpp` for `.bin` until the user picks `SNES 4bpp`.
- A window narrower than 256 pixels clips the banner at 1x (UI-WELCOME-011).

## Threading and lifetime

- `WelcomeViewModel` is a singleton for the app's lifetime. The banner decodes on a background thread at startup and is published to the UI thread. The recent list is the shell's (`MenuViewModel`), observed, not copied.

## Decisions

- **An empty-state view, not a document tab.** See [welcome-screen](../../changes/welcome-screen.md) Decisions; moves here when the change lands.
- **Decode the art at runtime from a real SNES 4bpp file.** As above.
- **One 16-color palette, stored in the file.** As above.
- **A checked-in generator, enforced by a byte-equality test.** As above.
- **A missing or broken demo file degrades silently to a text title.** As above.
- **Scale by whole factors only.** As above.

## Non-goals

- A "show on startup" preference, tips, news, or links beyond the three actions and the recent list.
- Opening the demo file in an editor from the welcome screen.
- Animation.

## Open items

- Draft: nothing is implemented yet. The `sources` paths do not exist until the change lands.
