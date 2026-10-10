# User documentation for 1.0

## Why

- README: says .NET 6, lists Autofac and Jot (neither is referenced any more; DI is `Microsoft.Extensions.DependencyInjection`), shows the old app, and does not document the CLI. Toolchain users find the CLI from the repository page.
- Release notes: the codec behavior changes recorded in LIB-CODECS ("Codec rework behavior changes"), the format changes from [project-format-freeze](project-format-freeze.md) and the CLI rework reach users nowhere. The release workflow ([CLI-PUBLISH](../specs/cli/publish.md)) needs `docs/release-notes/<Version>.md` as the release body.

## What

- **README** is current: what TileShop is, a screenshot of the Avalonia app, .NET 10, the dependency list, build and test commands, the supported and untested platforms, and a short CLI paragraph linking to `docs/cli.md`.
- **`docs/cli.md`** is the CLI reference for users: each verb with its options, the exit-code table, the export folder layout (mirrors the project paths), plugins and `--log`.
- **`docs/release-notes/1.0.md`** is the 1.0 release body.

## Decisions

- **The CLI reference lives in `docs/cli.md`, linked from the README.** It is versioned with the code and keeps the README short. It is written from [CLI-COMMANDS](../specs/cli/cli.md) and checked against `TileShopCLI --help` from the published build. To keep it from drifting, CLI-COMMANDS names it as the user documentation a CLI change must update. Rejected: the full reference in the README (crowds the page), a lean README section deferring options to `--help` (no browsable reference), and wiki only (not versioned with the code).
- **Release notes as one file per version** (`docs/release-notes/<version>.md`), used verbatim as the GitHub release body. Rejected: a growing `CHANGELOG.md` and notes written only on GitHub (see [CLI-PUBLISH](../specs/cli/publish.md) Decisions).
- **No authoring guides at 1.0.** The codec XML and plugin authoring guides stay in the backlog. Rejected for now: a codec XML guide alone for 1.0.

## Spec changes

- **CLI-COMMANDS**: `sources` adds `docs/cli.md`. Add to Decisions: "`docs/cli.md` is the user-facing reference; a change to a verb, option or exit code updates it in the same change."

## Tasks

1. **`docs/cli.md`.** Verbs, options, exit codes, export layout, plugin loading, `--log`, read-only skip, written from CLI-COMMANDS. Check every verb and option against `TileShopCLI --help` and `<verb> --help` from the published build.
2. **README.** .NET 10, current dependencies (drop Autofac, Jot; add Microsoft.Extensions.DependencyInjection), a current screenshot, build and test commands, platforms (win-x64 supported; linux-x64 and osx-arm64 untested), a CLI paragraph linking `docs/cli.md`. Check the wiki links resolve.
3. **Release notes** `docs/release-notes/1.0.md`, written last, after the other 1.0 proposals land:
   - Requirements: self-contained, no .NET install. Platforms: win-x64 supported; linux-x64 and osx-arm64 untested.
   - Codec behavior changes from LIB-CODECS "Codec rework behavior changes": Genesis 4bpp, SNES Mode 7, Virtual Boy 2bpp and NGPC 2bpp now show correct colors that differ from earlier versions; N64 RGBA32 reads `.z64` order; N64 RGBA16 reports 16 bpp; PSX 16bpp keeps STP; all codec XMLs now ship.
   - Project format changes from [project-format-freeze](project-format-freeze.md) (format 1.0; 0.9 projects open as 1.0; newer formats are refused) and scattered color sources from [scattered-color-sources](scattered-color-sources.md).
   - The new default palette and alternates from [sequential-palettes](sequential-palettes.md): elements on the global default palette look different.
   - The welcome screen from [welcome-screen](welcome-screen.md).
   - CLI changes from CLI-COMMANDS (System.CommandLine, awaited exit codes, `--log`, import options, plugins, read-only skip), linking `docs/cli.md`.
   - Read-only data files now open read-only (LIB-DATASOURCE-021).
   - Plugins built for earlier versions must be rebuilt against `ImageMagitek.Plugins.Contracts` (LIB-PLUGINS); TileShop reports them at startup.
4. **Close.** Update CLI-COMMANDS as listed. Delete the backlog's README and release-notes lines under "Release engineering and documentation". Delete this proposal.

## Open questions

None.
