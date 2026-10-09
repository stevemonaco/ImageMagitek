---
id: CLI-PUBLISH
title: Release packaging
project: TileShop.CLI, TileShop.UI
sources:
  - publish.ps1
  - TileShop.CLI/TileShop.CLI.csproj
  - TileShop.CLI/Properties/PublishProfiles
types: []
tests: []
depends:
  - CLI-COMMANDS
---

# Release packaging

## Purpose

`publish.ps1` builds, tests and packages TileShop and TileShopCLI as self-contained single-file builds for one runtime per run. It lives with the CLI specs because the CLI is the part toolchains download; the UI is packaged the same way.

## Requirements

### publish.ps1

- **CLI-PUBLISH-001** — The script shall require `-Version`, `-CliVersion` and `-Rid`, with `-Rid` one of `win-x64`, `osx-arm64`, `osx-x64` or `linux-x64`.
  - Tests: untested
- **CLI-PUBLISH-002** — When run, the script shall clean and build the solution in Release and run the unit tests.
  - Tests: untested
- **CLI-PUBLISH-003** — If the build or tests fail, then the script shall still go on to publish.
  - Tests: untested
- **CLI-PUBLISH-004** — The script shall publish TileShop.UI self-contained and single-file for the runtime into `publish/<rid>/TileShop`, and zip it as `publish/<rid>/TileShop-<rid>.v<Version>.zip`.
  - Tests: untested
- **CLI-PUBLISH-005** — The script shall publish TileShop.CLI self-contained and single-file for the runtime into `publish/<rid>/TileShopCLI`, and zip it as `publish/<rid>/TileShopCLI-<rid>.v<CliVersion>.zip`.
  - Tests: untested
- **CLI-PUBLISH-006** — The script shall accept `-ReadyToRun` and ignore it.
  - Tests: untested

### CLI build

- **CLI-PUBLISH-010** — The CLI shall build as an executable named `TileShopCLI`.
  - Tests: untested
- **CLI-PUBLISH-011** — A CLI publish shall be trimmed, keeping System.Text.Json, CommandLine, ImageMagitek, ImageMagitek.Services and TileShopCLI whole.
  - Tests: untested
- **CLI-PUBLISH-012** (inherited) — Built without a runtime identifier, the CLI shall target `win-x64`.
  - Tests: untested
- **CLI-PUBLISH-013** — The Visual Studio publish profiles ("TileShop.CLI portable", "TileShop.CLI win-x64-single") shall target `net7.0`, framework-dependent single-file `win-x64`, which the project no longer builds.
  - Tests: untested

## Invariants

- One runtime per run; outputs for different runtimes never share a folder.

## Edge cases

- A second run for the same runtime and versions fails at `Compress-Archive`, which refuses to overwrite an existing zip; the publish folders are overwritten.
- `-CliVersion` is mandatory, so the script's fallback to `-Version` when it is empty never runs, and it runs after the zip name is built anyway.

## Threading and lifetime

No runtime state; a build script.

## Decisions

- **Self-contained single-file builds.** Reason: users of a toolchain tool should not need a .NET runtime installed.
- **Trimmed CLI with whole roots for reflection users.** Verbs are discovered by reflection and settings use reflection-based JSON, so those assemblies are kept whole. Reason: a small download without breaking reflection.

## Non-goals

- CI. The workflow that should run `publish.ps1` on tags is not part of this spec (backlog).
- `dotnet tool` packaging.

## Open items

- The `.pubxml` profiles target `net7.0` (backlog): update them or delete them in favor of `publish.ps1`. `FolderProfile.pubxml.user` has no matching profile.
- The script does not stop on a failed build or test (CLI-PUBLISH-003).
- `-ReadyToRun` is unused (CLI-PUBLISH-006).
