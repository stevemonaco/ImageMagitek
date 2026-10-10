---
id: CLI-PUBLISH
title: Release packaging and CI
project: TileShop.CLI, TileShop.UI
sources:
  - publish.ps1
  - TileShop.CLI/TileShop.CLI.csproj
  - Directory.Build.props
  - Directory.Build.targets
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
types: []
tests: []
depends:
  - CLI-COMMANDS
---

# Release packaging and CI

## Purpose

`publish.ps1` builds, tests and packages TileShop and TileShopCLI as self-contained single-file builds for one runtime per run. CI builds and tests every push and pull request, and a hand-run release workflow runs `publish.ps1` per platform and creates a draft GitHub release. It lives with the CLI specs because the CLI is the part toolchains download; the UI is packaged the same way.

## Requirements

### publish.ps1

- **CLI-PUBLISH-001** — The script shall require `-Rid`, one of `win-x64`, `osx-arm64`, `osx-x64` or `linux-x64`, and read the version from MSBuild's `Version` property.
  - Tests: untested
- **CLI-PUBLISH-002** — When run, the script shall delete `publish/<rid>`, then clean and build the solution in Release and run the unit tests.
  - Tests: untested
- **CLI-PUBLISH-003** — If any build, test, publish or archive step fails, then the script shall stop with a non-zero exit code and produce no zip.
  - Tests: manual — run `pwsh ./publish.ps1 -Rid win-x64` with a deliberately failing unit test; it exits non-zero and `publish/win-x64` holds no zip.
- **CLI-PUBLISH-004** — The script shall publish TileShop.UI self-contained and single-file for the runtime into `publish/<rid>/TileShop`, and zip it as `publish/<rid>/TileShop-<rid>-v<Version>.zip`.
  - Tests: manual — run `pwsh ./publish.ps1 -Rid win-x64` twice in a row; both runs succeed.
- **CLI-PUBLISH-005** — The script shall publish TileShop.CLI self-contained and single-file for the runtime into `publish/<rid>/TileShopCLI`, and zip it as `publish/<rid>/TileShopCLI-<rid>-v<Version>.zip`.
  - Tests: manual — as CLI-PUBLISH-004.
- **CLI-PUBLISH-006** — Where `-ReadyToRun` is passed, the script shall build and publish both executables with ReadyToRun compilation.
  - Tests: manual — run `pwsh ./publish.ps1 -Rid win-x64 -ReadyToRun`; it succeeds and both executables start.

### CLI build

- **CLI-PUBLISH-010** — The CLI shall build as an executable named `TileShopCLI`.
  - Tests: untested
- **CLI-PUBLISH-011** — A CLI publish shall be trimmed, keeping System.Text.Json, ImageMagitek, ImageMagitek.Plugins.Contracts, ImageMagitek.Services and TileShopCLI whole.
  - Tests: manual — publish single-file for `win-x64`, run `--help`, `export --help` and `--version` (exit 0), and export an arranger that uses a sample plugin codec placed under `_plugins`
- **CLI-PUBLISH-012** (inherited) — Built without a runtime identifier, the CLI shall target `win-x64`.
  - Tests: untested
- ~~**CLI-PUBLISH-013**~~ — Removed: the Visual Studio publish profiles are deleted; `publish.ps1` is the only way to package.

### Versioning

- **CLI-PUBLISH-014** — The UI and CLI builds shall carry the `Directory.Build.props` version as their assembly and file versions, and that version plus `+` and the 7-character commit hash as their informational version.
  - Tests: manual — a Debug build's Help → About and `TileShopCLI --version` show `0.993-preview+<7 chars>`; the published build is in the release checklist.

### CI

- **CLI-PUBLISH-015** — When a commit is pushed or a pull request is opened or updated, CI shall build the solution and run the unit tests in Release on Windows, and fail when either fails.
  - Tests: manual — a pushed branch goes green on GitHub, and a deliberately failing test turns it red.
- **CLI-PUBLISH-016** — When the release workflow is run, CI shall run `publish.ps1` for win-x64, linux-x64 and osx-arm64 on runners of those operating systems, and create a draft GitHub release `v<Version>` targeting the run's commit with the zips attached and `docs/release-notes/<Version>.md` as its body. It shall fail, creating no release, when that file is missing, a release `v<Version>` already exists, or any publish fails.
  - Tests: manual — run the workflow on a branch with a placeholder `docs/release-notes/<Version>.md`; it produces a draft with six zips. Delete the draft and the placeholder afterwards.

## Invariants

- One runtime per run; outputs for different runtimes never share a folder.
- UI, CLI and every zip name carry the same version.

## Edge cases

- Without git or a `.git` folder (a source zip), `SourceRevisionId` is empty and the informational version is the version without a hash.
- In unit tests the entry assembly is the test host, so the CLI banner shows the test host's version; no test checks the banner.

## Threading and lifetime

No runtime state; build scripts and workflows.

## Decisions

- **Self-contained single-file builds.** Reason: users of a toolchain tool should not need a .NET runtime installed.
- **Trimmed CLI with whole roots for reflection users.** Settings, palettes and the transaction journal use reflection-based JSON, and codecs (built-in and plugin) are created by reflection, so System.Text.Json and the libraries are kept whole. Plugin codecs are created by `Activator`, and plugins call members of ImageMagitek.Plugins.Contracts (the `PluginColor` constructor, `CodecInfo` setters) that the host never calls, so that assembly is kept whole too; the UI publish roots it for the same reason. Verbs are built in code by System.CommandLine (CLI-COMMANDS), so the parser is trimmed normally. Reason: a small download without breaking reflection.
- **One version for UI and CLI, in `Directory.Build.props`.** They ship from one commit and share the library and the project format, so separate numbers only raise "which CLI goes with which UI". Rejected: a separate `-CliVersion`, and passing the version on the command line (it let the zip name and the binaries disagree).
- **Release procedure.** The release commit sets `VersionPrefix` to the release version and removes `VersionSuffix`. The owner runs the release workflow on that commit. It reads MSBuild's `Version` and creates the draft with `gh release create v<Version> --draft --target <sha>`. GitHub creates the tag only when the draft is published, so a failed QA pass leaves no tag behind. After the release, `VersionPrefix` moves to the next version with `VersionSuffix` `preview` again. Reason: the version is in source, so any checkout builds what it says, and the workflow derives the tag from it, so the two cannot disagree. Rejected: a tag push checked against `Version` (a check that only exists because the tag is typed by hand), deriving the version from the tag (local builds would not know their version), and MinVer/Nerdbank (a dependency for one number).
- **The informational version carries a 7-character commit hash.** A target in `Directory.Build.targets` shortens `SourceRevisionId` before the SDK appends it, so `AssemblyInformationalVersionAttribute` is `1.0.0+8ee6c6e` everywhere, including System.CommandLine's `--version`. About and the CLI banner read the entry assembly's attribute inline; each is one line, so there is no shared helper. Reason: the hash identifies a build in bug reports; the full 40 characters only make it unreadable. Rejected: `FileVersionInfo` on `Assembly.Location` (empty in single-file), `Assembly.GetName().Version` (drops the suffix and the commit), truncating at each call site (`--version` would still print the full hash), and no hash at all.
- **CI builds and tests in Release.** That is the configuration that ships: the Debug-only debug load button and DevTools are compiled out, and the license-keyed `AvaloniaUI.DiagnosticsSupport` packages are Debug-only, so CI needs no Avalonia license secret. Rejected: Debug CI (it tests a build nobody downloads).
- **win-x64 is the supported platform; linux-x64 and osx-arm64 ship untested.** Push and PR CI runs on `windows-latest` only, where the project is developed and checked. The release workflow builds linux-x64 and osx-arm64 on native runners, because macOS refuses the unsigned arm64 binaries a Windows build produces. The release notes label them "untested". osx-x64 is not released (`publish.ps1` still accepts it for local builds). Rejected: win-x64 only (gives up two platforms that cost one matrix entry each), and a Linux test leg in push CI (can be added later).
- **Draft release, published by hand.** The release workflow uploads to a draft. The owner publishes it after the QA pass on those exact zips. Rejected: publishing from the workflow (the QA pass would run after users can download), and Actions artifacts with a hand-made release (the QA'd zips and the published ones could differ).
- **`publish.ps1` fails fast.** `#Requires -Version 7.3` with `$ErrorActionPreference = 'Stop'` and `$PSNativeCommandUseErrorActionPreference = $true`, so a failing `dotnet` call stops the script. The script already needs PowerShell 7 (multi-argument `Join-Path`). Rejected: checking `$LASTEXITCODE` after each call (easy to miss one).
- **`-ReadyToRun` is wired up, off by default.** It is a `[switch]` passed as `-p:PublishReadyToRun` to both `dotnet build` and `dotnet publish`: the publish runs `--no-restore`, so the Crossgen2 pack must be restored by the build or publish fails with NETSDK1094. Whether release zips use it is not yet measured (Open items). Rejected: deleting the switch (the option is wanted, just unverified).
- **Delete the `.pubxml` profiles.** They targeted a framework the project no longer builds and duplicated `publish.ps1` with different settings (framework-dependent). Rejected: updating them (two packaging paths that can drift).
- **The release workflow requires `docs/release-notes/<Version>.md`** and uses it verbatim as the release body. Rejected: a growing `CHANGELOG.md` (the body then needs extracting) and notes written only on GitHub (not reviewed with the code).

## Non-goals

- `dotnet tool` packaging.

## Open items

- Whether release zips use ReadyToRun: measure zip size and cold and warm startup on win-x64 with and without `-ReadyToRun`, record the numbers in the `-ReadyToRun` decision, and settle whether the release workflow passes it (backlog).
