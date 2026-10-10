# Ship 1.0

## Why

1.0 is published only after the artifact users download has been checked by hand. DevTools cannot attach to a Release build, so a Release-only regression (trimming in the CLI, the Debug-only code paths, single-file `Assembly.Location`) is caught only by a manual pass on the published zips. The backlog's "Manual checks" section also holds checks DevTools cannot drive (gestures, drag and drop, popups), pending from earlier milestones.

This proposal is the last step: it lands after every other **1.0** item, including [project-format-freeze](project-format-freeze.md) and [docs-1-0](docs-1-0.md).

## What

- **`docs/release-checklist.md`**, a permanent smoke checklist run on every release's draft zips.
- **The 1.0 QA pass**: the release checklist plus every line in the backlog's "Manual checks" section, run on the draft release's win-x64 zips on a machine without the .NET SDK. Then the draft is published.

## Decisions

- **A permanent smoke checklist plus one-off checks.** What differs in a Release single-file build (startup without .NET, About's version, the trimmed CLI, the missing debug button) needs checking on every release, so it lives in `docs/release-checklist.md`. The pending manual checks are one-off and already listed in the backlog, so the pass runs them from there instead of copying them here: a passing check deletes its backlog line, a failing one becomes a bug line in the backlog, tagged **1.0** when it meets the ARCHITECTURE §1 bar, and blocks publishing the draft until fixed and rechecked. Rejected: the whole checklist in this proposal (future releases would rewrite the smoke list), a GitHub issue (not versioned with the code), and running every `manual —` requirement in the specs (about 180, most verified when built).
- **The pass runs on win-x64 only.** linux-x64 and osx-arm64 ship labeled "untested" ([CLI-PUBLISH](../specs/cli/publish.md)).

## Spec changes

- **docs/ARCHITECTURE.md** §6: amend the existing version/release bullet to name `docs/release-checklist.md` as the release QA pass.

## Tasks

1. **`docs/release-checklist.md`.** On the draft's `TileShop-win-x64` and `TileShopCLI-win-x64` zips, extracted to a fresh folder on a machine or VM without the .NET SDK:
   - Both executables start with no .NET installed.
   - Help → About shows `<Version>+<7-char hash>` matching the draft's commit, lists plugin codecs with a sample plugin in `_plugins`, and shows "No plugin codecs loaded" without one (UI-SHELL-043).
   - `TileShopCLI --version` prints the same version; `--help`, `export --help` and `--version` exit 0.
   - `print`, `export`, `exportall`, `import`, `importall` each run against the FF2 test project and return the documented exit codes (the CLI is trimmed only in this build).
   - With the sample plugin under `_plugins`, an arranger using "Last Armageddon Font" exports, and `importall` skips it as read-only.
   - The title bar has no "Load FF2" button (UI-SHELL-103).
   - linux-x64 and osx-arm64 zips are attached to the draft and named for the version.
2. **1.0 QA pass** (manual, after every other **1.0** item is closed). Set `VersionPrefix` to `1.0.0` and remove `VersionSuffix`, run the release workflow, then run `docs/release-checklist.md` and every line of the backlog's "Manual checks" section on the draft's win-x64 zips. Record outcomes in the backlog as described in Decisions.
3. **Publish.** Publish the draft once every check passes. Move `VersionPrefix` to the next version with `VersionSuffix` `preview`.
4. **Close.** Update ARCHITECTURE as listed. Delete the backlog's "Manual checks" section (empty by now) and this proposal.

## Open questions

None.
