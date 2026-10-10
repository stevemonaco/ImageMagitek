# Welcome screen and demo file

## Why

TileShop starts on an empty grey document area. A first-time user sees no graphics, no hint of what the app is for, and no next step except the File menu. 1.0 is the first release most people will try, so the first screen should show what TileShop does and offer the three ways in: open a file, open a project, or make a project from a file.

The welcome screen is also a dogfooding check that runs on every launch. Its art is real retro data, an SNES 4bpp tile file with a BGR15 palette, decoded at startup by the same codec, palette and arranger code users rely on. A broken SNES 4bpp codec or palette path would show on the first screen.

## What

**Welcome screen.** While the document area has no open editor, it shows the welcome screen in place of the empty area (draft spec: [UI-WELCOME](../specs/ui/welcome.md)). No editor is opened for it, and there is no tab to close. It disappears when an editor opens and returns when the last one closes. It shows, top to bottom:

- **Banner.** The demo art, scaled by the largest whole factor (up to 4x) that fits the area's width, nearest-neighbor, centered.
- **Actions.** Three buttons: Open File..., Open Project..., New Project from Existing File.... They run the same commands as the File menu.
- **Recent projects.** Up to 8 entries from the recent list, one button each, showing the project name with its path as a tooltip. The section is hidden when the list is empty.
- **Version.** The informational version, as in About, in small text at the bottom.

**Demo file.** `TileShop.UI/_demo/TileShopDemo.bin` ships in the app folder beside `_codecs`, in the build output and the published zips.

- It holds 4bpp SNES planar tiles in row-major screen order, followed by one 16-color BGR15 little-endian palette in its last 32 bytes. Index 0 is transparent.
- The banner is 256×96 pixels (32×12 tiles, 12,288 bytes of tiles), so the file is 12,320 bytes.
- The welcome screen decodes it as a sequential arranger with the `SNES 4bpp` codec, 32×12 elements, and a palette of 16 file colors at the file's end. Because index 0 is transparent, the banner sits on the theme background in light and dark themes.

**The art** is original, made for TileShop, and limited to 15 colors plus transparency:

- the word "TileShop" in chunky outlined pixel letters;
- a small scene: a dithered sunset sky in bands, a perspective grid floor, and two or three small original sprites, such as a pixel pencil and a palette swatch with faces.

It reads well at 1x and in both themes: the letters have a dark outline and the scene has its own opaque sky behind the letters.

**Generator.** The art is produced by a checked-in generator, `Tools/DemoArtGenerator` (a console project). The pixels are authored in its source: text pixel maps for the letters and sprites, where each character is a hex palette index, plus procedural code for the sky dither and the grid. It encodes the tiles through ImageMagitek's `SNES 4bpp` XML codec and writes the file. A unit test reruns the generator in memory and requires the shipped file to match byte for byte, so the art can only change through the generator.

**If the demo file is missing or fails to decode** (deleted, a broken `_codecs`), the welcome screen shows the text "TileShop" in place of the banner, and the failure is logged as a warning. No alert is shown, because the screen is cosmetic and an alert on every launch would be worse than a missing picture.

**What does not change.** The File menu, the recent list's rules (UI-SHELL-020–024), startup issue reporting for essential and optional resources (LIB-SERVICES), and the Debug "Load FF2" button.

## Decisions

- **An empty-state view, not a document tab.** The welcome screen is what the document area shows while it holds no editor. Reason: there is nothing to close, save, float or restore, and no "show on startup" preference is needed, because it gets out of the way by itself. Rejected:
  - A closable Welcome tab. It needs dock lifetime handling, a preference to suppress it, and a way to bring it back.
  - A modal start dialog. It blocks the app, and DevTools drive overlays poorly.
- **Decode the art at runtime from a real SNES 4bpp file.** Reason: the first screen exercises the data source, XML codec, palette file colors and arranger rendering on every launch, and the shipped file doubles as a small demo users can open (Open File..., codec SNES 4bpp). Rejected: a pre-rendered PNG asset (it tests nothing and is not a demo of the format); embedding the bytes as an Avalonia resource (users could not open it).
- **One 16-color palette, stored in the file.** Reason: a single sequential arranger with one palette is the simplest decode, and the 15-color limit is authentic to a single SNES background palette. Storing the palette at the end of the file, as CGRAM-style BGR15, also exercises file color sources. Rejected: several palettes (needs a scattered arranger or a tilemap); a separate palette file (two files to keep in step).
- **A checked-in generator, enforced by a byte-equality test.** Reason: the art is reviewable and reproducible as source, and the test stops the binary and the generator from drifting. Encoding through the XML codec also tests encode. Rejected: hand-editing the binary in TileShop as the source of truth (not reviewable in diffs); a generator run by the build (adds a build step to every build for a file that rarely changes).
- **A missing or broken demo file degrades silently to a text title.** Reason: the art is not essential (ARCHITECTURE §6, "Startup degrades; it stops only for essentials"), and its loss does not affect any user data or workflow. It is logged, so a broken install still leaves a trace. Rejected: a startup alert (noise for a cosmetic file); making it essential (fails startup over a picture).
- **Scale by whole factors only.** Reason: pixel art at fractional scales shimmers and blurs; whole-number nearest-neighbor scaling keeps every pixel square.

## Spec changes

**UI-WELCOME** (new, draft): [docs/specs/ui/welcome.md](../specs/ui/welcome.md). Becomes current when this change lands.

**UI-SHELL**

- Added (Layout): While the document area holds no editor, it shall show the welcome screen (UI-WELCOME).
- `depends`: add UI-WELCOME.

**CLI-PUBLISH**

- Added: The TileShop zips shall contain `_demo/TileShopDemo.bin`.

**ARCHITECTURE.md**

- §2 Projects table: add `Tools/DemoArtGenerator`, which generates the welcome screen's demo file, with no spec.

## Tasks

1. **Generator and demo file.**
   - Code:
     - Add `Tools/DemoArtGenerator` (console, references `ImageMagitek` and `ImageMagitek.Services` for the codec factory) to the solution. Its `DemoArt.Generate()` returns the file's bytes; `Main` writes them to `TileShop.UI/_demo/TileShopDemo.bin`.
     - Author the art, with the palette authored for both themes.
     - Copy `_demo` to the TileShop.UI output.
   - Tests: `DemoArtTests` (new): `ShippedFile_MatchesGenerator`, `ShippedFile_Is12320Bytes`, `Palette_IsLast32Bytes_Bgr15_ZeroTransparent`, `Decodes_WithSnes4bpp_NoIndexAbove15`.
   - Manual: look at the art with the user at 1x, 2x and 4x in both themes before landing; taste is the user's call.
2. **Welcome screen.**
   - Code:
     - `WelcomeViewModel`: banner decode on a background thread at startup, actions, the recent list from `MenuViewModel`'s preferences, and the version.
     - `WelcomeView`, registered in `ConfigureViewLocator`.
     - The document area shows it while the document dock has no visible documents.
     - All actions are `Button`s (ARCHITECTURE §6, "Build UI as automatable controls").
   - Tests: `WelcomeViewModelTests` (new, `Shell/`): `Banner_DecodesDemoFile`, `MissingDemoFile_NoBannerAndLogs`, `BrokenCodec_NoBanner`, `Recent_MaxEightHiddenWhenEmpty`, `Actions_RunProjectTreeCommands` (with a fake project tree command target), `ScaleFactor_LargestWholeFitUpToFour` (theory over widths).
   - Verify in the running app with DevTools:
     - Launch, and screenshot the welcome screen in the dark theme and in the light theme.
     - Click Open Project... (the file picker is a popup; check the command ran through bound state).
     - Load FF2 and open an arranger: the welcome screen hides. Close the editor: it returns.
     - Click a recent project button.
     - Delete the build output's `_demo` and relaunch: the text title shows and the log has the warning.
   - Manual: resize the window across the scale thresholds and check the banner snaps between whole factors.
3. **Release.**
   - Add `_demo/TileShopDemo.bin` to the published zip check in `docs/release-checklist.md` ([release-1-0](release-1-0.md)).
   - Mention the welcome screen in the 1.0 release notes ([docs-1-0](docs-1-0.md)), and use it for the README screenshot.
4. **Specs and backlog.**
   - Remove `status: draft` from UI-WELCOME, fill its `Tests:` lines with real test names, and apply the other spec changes.
   - Add UI-WELCOME to `docs/specs/ui/index.md` (done with the draft).
   - Delete this proposal. Run `dotnet test ImageMagitek.UnitTests`.

## Open questions

None.
